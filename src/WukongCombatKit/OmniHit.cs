using System;
using System.Collections.Generic;
using System.Reflection;
using b1;
using HarmonyLib;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;
using WukongCombatKit.Core;

namespace WukongCombatKit
{
    public static class OmniHit
    {
        public static bool Available { get; private set; }
        private static Type _sweepCompType;
        private static MethodInfo _onSweepCheckHit;

        public static void Register(Harmony harmony)
        {
            Available = false;
            if (harmony == null)
            {
                return;
            }

            try
            {
                _sweepCompType = AccessTools.TypeByName("b1.BUS_SweepCheckHitComp");
                if (_sweepCompType == null)
                {
                    ModLog.Error("OmniHit disabled: BUS_SweepCheckHitComp not found");
                    return;
                }

                _onSweepCheckHit = AccessTools.Method(_sweepCompType, "OnSweepCheckHit");
                MethodInfo sweepInternal = AccessTools.Method(_sweepCompType, "SweepCheckInternal");
                MethodInfo combineSingle = AccessTools.Method(_sweepCompType, "CombineSweepCheckInternal_Single");
                if (sweepInternal == null && combineSingle == null)
                {
                    ModLog.Error("OmniHit disabled: sweep methods not found");
                    return;
                }

                if (sweepInternal != null)
                {
                    harmony.Patch(
                        sweepInternal,
                        postfix: new HarmonyMethod(typeof(OmniHit), nameof(SweepCheckInternalPostfix)));
                    ModLog.Info("Patch registered: BUS_SweepCheckHitComp.SweepCheckInternal");
                    Available = true;
                }

                if (combineSingle != null)
                {
                    harmony.Patch(
                        combineSingle,
                        postfix: new HarmonyMethod(typeof(OmniHit), nameof(CombineSweepCheckInternalPostfix)));
                    ModLog.Info("Patch registered: BUS_SweepCheckHitComp.CombineSweepCheckInternal_Single");
                    Available = true;
                }
            }
            catch (Exception ex)
            {
                Available = false;
                ModLog.Error("OmniHit sweep patch failed: " + ex.Message);
            }
        }

        public static void SweepCheckInternalPostfix(object __instance, string NotifyInstID)
        {
            try
            {
                ApplyFromSweep(__instance, NotifyInstID, null);
            }
            catch (Exception ex)
            {
                ModLog.Error("OmniHit SweepCheckInternal: " + ex.Message);
            }
        }

        public static void CombineSweepCheckInternalPostfix(object __instance, FSweepCheckCombineInfo CombineInfo, string EndNotifyID)
        {
            try
            {
                ApplyFromSweep(__instance, EndNotifyID, CombineInfo);
            }
            catch (Exception ex)
            {
                ModLog.Error("OmniHit CombineSweepCheck: " + ex.Message);
            }
        }

        private static void ApplyFromSweep(object sweepComp, string notifyId, FSweepCheckCombineInfo combineInfo)
        {
            if (!ConfigStore.Current.EnableOmniHit || sweepComp == null)
            {
                return;
            }

            BGUPlayerCharacterCS player = ResolveOwner(sweepComp) as BGUPlayerCharacterCS;
            if (player == null)
            {
                return;
            }

            BUC_SweepCheckHitData hitData = BGU_DataUtil.GetUnPersistentReadOnlyData<BUC_SweepCheckHitData>(player);
            if (hitData == null || hitData.SweepCheckConfigMap == null || hitData.SweepCheckConfigMap.Count == 0)
            {
                return;
            }

            FSweepCheckUnitConfig config = null;
            if (!string.IsNullOrEmpty(notifyId))
            {
                hitData.SweepCheckConfigMap.TryGetValue(notifyId, out config);
            }

            if (config == null && combineInfo != null && combineInfo.CombinedConfigShapeNotifyIDSet != null)
            {
                for (int i = 0; i < combineInfo.CombinedConfigShapeNotifyIDSet.Count; i++)
                {
                    string combinedId = combineInfo.CombinedConfigShapeNotifyIDSet[i];
                    if (hitData.SweepCheckConfigMap.TryGetValue(combinedId, out config))
                    {
                        notifyId = combinedId;
                        break;
                    }
                }
            }

            if (config == null)
            {
                foreach (KeyValuePair<string, FSweepCheckUnitConfig> pair in hitData.SweepCheckConfigMap)
                {
                    notifyId = pair.Key;
                    config = pair.Value;
                    break;
                }
            }

            if (config == null)
            {
                return;
            }

            ApplyOmniHits(sweepComp, player, config, notifyId);
        }

        private static void ApplyOmniHits(object sweepComp, BGUPlayerCharacterCS player, FSweepCheckUnitConfig config, string notifyId)
        {
            UWorld world = player.World;
            if (world == null)
            {
                return;
            }

            FVector origin = RaisedPoint(player);
            List<BGUCharacterCS> characters = new List<BGUCharacterCS>(UGameplayStatics.GetAllActorsOfClass<BGUCharacterCS>(world));
            if (characters.Count == 0)
            {
                return;
            }

            List<OmniHitCandidate> candidates = new List<OmniHitCandidate>();
            Dictionary<string, BGUCharacterCS> byId = new Dictionary<string, BGUCharacterCS>();
            bool originalHitsSceneObjects = config.EffectIDListForSceneItem != null && config.EffectIDListForSceneItem.Count > 0;

            for (int i = 0; i < characters.Count; i++)
            {
                BGUCharacterCS character = characters[i];
                if (character == null || character == player)
                {
                    continue;
                }

                FVector target = RaisedPoint(character);
                float distance = (float)(target - origin).Size();
                bool isEnemy = false;
                try
                {
                    isEnemy = BGUFunctionLibraryCS.BGUIsEnemyTeam(player, character);
                }
                catch
                {
                    isEnemy = false;
                }

                string id = character.GetUniqueID().ToString();
                candidates.Add(new OmniHitCandidate
                {
                    Id = id,
                    Distance = distance,
                    IsSelf = false,
                    IsAlly = !isEnemy,
                    IsEnemy = isEnemy,
                    IsSceneObject = false,
                    WallBlocked = IsWallBlocked(world, origin, target, character)
                });
                byId[id] = character;
            }

            List<string> selected = OmniHitRules.SelectVisibleTargets(
                candidates,
                ConfigStore.Current.MaxAttackRange,
                originalHitsSceneObjects);

            if (_onSweepCheckHit == null || sweepComp == null)
            {
                ModLog.Error("OmniHit disabled: OnSweepCheckHit not found");
                return;
            }

            HashSet<string> injectedThisSweep = new HashSet<string>();
            int injected = 0;
            foreach (string id in selected)
            {
                BGUCharacterCS victim;
                if (!byId.TryGetValue(id, out victim) || victim == null)
                {
                    continue;
                }

                if (!injectedThisSweep.Add(id))
                {
                    continue;
                }

                try
                {
                    FEffectInstReq req = new FEffectInstReq(player);
                    req.TriggerSkillId = config.TriggerSkillID;
                    req.HitLocation = BGUFuncLibActorTransformCS.BGUGetActorLocation(victim);
                    req.HitDiretionRealDir = (req.HitLocation - origin).GetSafeNormal();
                    object[] args = new object[]
                    {
                        victim,
                        config.SweepCheckProtectTime,
                        notifyId,
                        req,
                        config.AbnormalStateEffectList,
                        config.EffectsWithCondition_Before,
                        config.EffectIDList,
                        config.EffectsWithCondition_After,
                        config.SweepCheckGroupID,
                        config.FromInstanceID
                    };
                    _onSweepCheckHit.Invoke(sweepComp, args);
                    injected++;
                }
                catch (Exception ex)
                {
                    ModLog.Error("OmniHit inject failed: " + ex.Message);
                }
            }

            if (injected > 0)
            {
                ModLog.Debug("OmniHit injected " + injected + " targets");
            }
        }

        private static bool IsWallBlocked(UWorld world, FVector origin, FVector target, AActor victim)
        {
            try
            {
                FVector direction = target - origin;
                float targetDistance = (float)direction.Size();
                if (targetDistance <= 1f)
                {
                    return false;
                }

                FHitResultSimple hit = new FHitResultSimple();
                bool hitSomething = BGUFuncLibSelectTargetsCS.LineTraceForHitWorldItem(world, origin, target, out hit);
                bool hitTarget = false;
                float hitDistance = targetDistance;
                if (hitSomething && hit != null)
                {
                    if (hit.HitActor == victim || (victim != null && hit.HitActor == victim.GetAttachParentActor()))
                    {
                        hitTarget = true;
                    }

                    hitDistance = (float)(hit.HitLocation - origin).Size();
                }

                float radius = 120f;
                BGUCharacterCS character = victim as BGUCharacterCS;
                if (character != null && character.CapsuleComponent != null)
                {
                    radius = Math.Max((float)character.CapsuleComponent.GetScaledCapsuleRadius(), 80f);
                }

                return OmniHitRules.IsTerrainBlocking(hitSomething, hitTarget, hitDistance, targetDistance, radius);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static FVector RaisedPoint(AActor actor)
        {
            FVector location = BGUFuncLibActorTransformCS.BGUGetActorLocation(actor);
            float raise = 80f;
            BGUCharacterCS character = actor as BGUCharacterCS;
            if (character != null && character.CapsuleComponent != null)
            {
                raise = Math.Max((float)character.CapsuleComponent.GetScaledCapsuleHalfHeight() * 0.6f, 60f);
            }

            return location + FVector.UpVector * raise;
        }

        private static AActor ResolveOwner(object instance)
        {
            if (instance == null)
            {
                return null;
            }

            MethodInfo getOwner = AccessTools.Method(instance.GetType(), "GetOwner");
            if (getOwner != null)
            {
                return getOwner.Invoke(instance, null) as AActor;
            }

            PropertyInfo owner = AccessTools.Property(instance.GetType(), "Owner");
            return owner != null ? owner.GetValue(instance, null) as AActor : null;
        }
    }
}
