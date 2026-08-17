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
        private static readonly HashSet<string> InjectedKeys = new HashSet<string>();
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

                MethodInfo sweepInternal = AccessTools.Method(_sweepCompType, "SweepCheckInternal");
                _onSweepCheckHit = AccessTools.Method(_sweepCompType, "OnSweepCheckHit");
                if (sweepInternal == null)
                {
                    ModLog.Error("OmniHit disabled: SweepCheckInternal not found");
                    return;
                }

                harmony.Patch(
                    sweepInternal,
                    postfix: new HarmonyMethod(typeof(OmniHit), nameof(SweepCheckInternalPostfix)));
                Available = true;
                ModLog.Info("Patch registered: BUS_SweepCheckHitComp.SweepCheckInternal");
            }
            catch (Exception ex)
            {
                Available = false;
                ModLog.Error("OmniHit SweepCheckInternal patch failed: " + ex.Message);
            }
        }

        public static void SweepCheckInternalPostfix(object __instance, string NotifyInstID)
        {
            try
            {
                if (!ConfigStore.Current.EnableOmniHit || __instance == null)
                {
                    return;
                }

                BGUPlayerCharacterCS player = ResolveOwner(__instance) as BGUPlayerCharacterCS;
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
                if (!string.IsNullOrEmpty(NotifyInstID))
                {
                    hitData.SweepCheckConfigMap.TryGetValue(NotifyInstID, out config);
                }

                if (config == null)
                {
                    foreach (KeyValuePair<string, FSweepCheckUnitConfig> pair in hitData.SweepCheckConfigMap)
                    {
                        NotifyInstID = pair.Key;
                        config = pair.Value;
                    }
                }

                if (config == null)
                {
                    return;
                }

                ApplyOmniHits(__instance, player, config, NotifyInstID);
            }
            catch (Exception ex)
            {
                ModLog.Error("OmniHit SweepCheckInternal: " + ex.Message);
            }
        }

        private static void ApplyOmniHits(object sweepComp, BGUPlayerCharacterCS player, FSweepCheckUnitConfig config, string notifyId)
        {
            UWorld world = player.World;
            if (world == null)
            {
                return;
            }

            FVector origin = BGUFuncLibActorTransformCS.BGUGetActorLocation(player);
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

                FVector target = BGUFuncLibActorTransformCS.BGUGetActorLocation(character);
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

            int injected = 0;
            foreach (string id in selected)
            {
                BGUCharacterCS victim;
                if (!byId.TryGetValue(id, out victim) || victim == null)
                {
                    continue;
                }

                string key = notifyId + ":" + victim.GetUniqueID();
                if (!InjectedKeys.Add(key))
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
                if (direction.IsNearlyZero())
                {
                    return false;
                }

                FHitResultSimple hit = new FHitResultSimple();
                if (!BGUFuncLibSelectTargetsCS.LineTraceForHitWorldItem(world, origin, target, out hit))
                {
                    return false;
                }

                if (hit == null || hit.HitActor == null)
                {
                    return true;
                }

                if (hit.HitActor == victim || hit.HitActor == victim.GetAttachParentActor())
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                return true;
            }
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
