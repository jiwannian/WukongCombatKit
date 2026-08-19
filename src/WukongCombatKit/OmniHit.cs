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
            List<OmniHitCandidate> candidates = new List<OmniHitCandidate>();
            Dictionary<string, AActor> byId = new Dictionary<string, AActor>();

            CollectCharacters(world, player, origin, candidates, byId);
            CollectSceneItems(world, player, origin, candidates, byId);

            List<string> selected = OmniHitRules.SelectVisibleTargets(
                candidates,
                ConfigStore.Current.MaxAttackRange,
                true);

            HashSet<string> injectedThisSweep = new HashSet<string>();
            int injected = 0;
            foreach (string id in selected)
            {
                AActor victim;
                if (!byId.TryGetValue(id, out victim) || victim == null)
                {
                    continue;
                }

                if (!injectedThisSweep.Add(id))
                {
                    continue;
                }

                if (AlreadyHitByOriginalSweep(victim, notifyId, config))
                {
                    continue;
                }

                if (ApplyHitToActor(sweepComp, player, config, notifyId, origin, victim))
                {
                    injected++;
                }
            }

            if (injected > 0)
            {
                ModLog.Debug("OmniHit injected " + injected + " targets");
            }
        }

        private static void CollectCharacters(UWorld world, BGUPlayerCharacterCS player, FVector origin, List<OmniHitCandidate> candidates, Dictionary<string, AActor> byId)
        {
            float maxRange = ConfigStore.Current.MaxAttackRange;
            List<BGUCharacterCS> characters = new List<BGUCharacterCS>(UGameplayStatics.GetAllActorsOfClass<BGUCharacterCS>(world));
            for (int i = 0; i < characters.Count; i++)
            {
                BGUCharacterCS character = characters[i];
                if (character == null || character == player)
                {
                    continue;
                }

                FVector target = AimPoint(character);
                if (DistanceToActor(origin, character, target) > maxRange)
                {
                    continue;
                }

                bool isEnemy = false;
                try
                {
                    isEnemy = BGUFunctionLibraryCS.BGUIsEnemyTeam(player, character);
                }
                catch
                {
                    isEnemy = false;
                }

                AddCandidate(candidates, byId, player, character, origin, target, isEnemy, false);
            }
        }

        private static void CollectSceneItems(UWorld world, BGUPlayerCharacterCS player, FVector origin, List<OmniHitCandidate> candidates, Dictionary<string, AActor> byId)
        {
            AddSceneActors(player, UGameplayStatics.GetAllActorsOfClass<BGUDestructibleActorBase>(world), origin, candidates, byId);
            AddSceneActors(player, UGameplayStatics.GetAllActorsOfClass<BGUDroppableDestructionActorBase>(world), origin, candidates, byId);
            AddSceneActors(player, UGameplayStatics.GetAllActorsOfClass<BGUFXActorBase>(world), origin, candidates, byId);
            AddSceneActors(player, UGameplayStatics.GetAllActorsOfClass<BGUInteractiveActorBase>(world), origin, candidates, byId);
            AddSceneActors(player, UGameplayStatics.GetAllActorsOfClass<BGUSceneItemBase>(world), origin, candidates, byId);
        }

        private static void AddSceneActors<T>(BGUPlayerCharacterCS player, IEnumerable<T> actors, FVector origin, List<OmniHitCandidate> candidates, Dictionary<string, AActor> byId) where T : AActor
        {
            if (actors == null)
            {
                return;
            }

            float maxRange = ConfigStore.Current.MaxAttackRange;
            foreach (T actor in actors)
            {
                if (actor == null)
                {
                    continue;
                }

                FVector target = AimPoint(actor);
                if (DistanceToActor(origin, actor, target) > maxRange)
                {
                    continue;
                }

                AddCandidate(candidates, byId, player, actor, origin, target, false, true);
            }
        }

        private static void AddCandidate(List<OmniHitCandidate> candidates, Dictionary<string, AActor> byId, AActor attacker, AActor actor, FVector origin, FVector target, bool isEnemy, bool isSceneObject)
        {
            string id = actor.GetUniqueID().ToString();
            if (byId.ContainsKey(id))
            {
                return;
            }

            float surfaceDistance = DistanceToActor(origin, actor, target);
            bool skipWallCheck = isEnemy && EstimateRadius(actor) >= 400f;
            candidates.Add(new OmniHitCandidate
            {
                Id = id,
                Distance = surfaceDistance,
                IsSelf = false,
                IsAlly = !isEnemy && !isSceneObject,
                IsEnemy = isEnemy,
                IsSceneObject = isSceneObject,
                WallBlocked = skipWallCheck ? false : IsWallBlocked(actor.World, attacker, actor, origin, target)
            });
            byId[id] = actor;
        }

        private static bool ApplyHitToActor(object sweepComp, BGUPlayerCharacterCS player, FSweepCheckUnitConfig config, string notifyId, FVector origin, AActor victim)
        {
            try
            {
                FEffectInstReq req = new FEffectInstReq(player);
                req.TriggerSkillId = config.TriggerSkillID;
                req.HitLocation = BGUFuncLibActorTransformCS.BGUGetActorLocation(victim);
                req.HitDiretionRealDir = (req.HitLocation - origin).GetSafeNormal();

                if (victim is BGUCharacterCS)
                {
                    if (_onSweepCheckHit == null)
                    {
                        return false;
                    }

                    object[] args = new object[]
                    {
                        victim,
                        config.SweepCheckProtectTime,
                        notifyId + "_omni",
                        req,
                        config.AbnormalStateEffectList,
                        config.EffectsWithCondition_Before,
                        config.EffectIDList,
                        config.EffectsWithCondition_After,
                        -1,
                        config.FromInstanceID
                    };
                    _onSweepCheckHit.Invoke(sweepComp, args);
                    return true;
                }

                BUS_GSEventCollection victimEvents = BUS_EventCollectionCS.Get(victim);
                if (victimEvents != null && victimEvents.Evt_HitDestructible != null)
                {
                    FHitDestructibleActorConfig hitConfig = config.HitDestructibleActorConfig;
                    EGSHitDestructibleStrengthLevel strength = hitConfig.HitStrengthLevel;
                    if (strength == EGSHitDestructibleStrengthLevel.None)
                    {
                        strength = EGSHitDestructibleStrengthLevel.Heavy;
                    }

                    EGSHitDestructibleDirection direction = hitConfig.HitDirection;
                    float impulse = BGUFunctionLibraryCS.GetDestructibleImpulse(player, strength);
                    victimEvents.Evt_HitDestructible.Invoke(player, strength, direction, req, impulse);
                    return true;
                }

                BUS_GSEventCollection playerEvents = BUS_EventCollectionCS.Get(player);
                if (playerEvents != null && config.EffectIDListForSceneItem != null)
                {
                    foreach (int effectId in config.EffectIDListForSceneItem)
                    {
                        playerEvents.Evt_TriggerSkillEffect.Invoke(effectId, req, victim);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("OmniHit inject failed: " + ex.Message);
            }

            return false;
        }

        private static bool AlreadyHitByOriginalSweep(AActor victim, string notifyId, FSweepCheckUnitConfig config)
        {
            try
            {
                BUC_UnitBeAttackedFequenceData data = BGU_DataUtil.GetReadOnlyData<BUC_UnitBeAttackedFequenceData>(victim);
                if (data == null)
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(notifyId) && !data.CheckBeAttackedFequenceData(notifyId))
                {
                    return true;
                }

                if (config != null && !data.CheckBeAttackedGroupInfo(config.SweepCheckGroupID, config.FromInstanceID))
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsWallBlocked(UWorld world, AActor attacker, AActor victim, FVector origin, FVector target)
        {
            try
            {
                FVector from = attacker != null ? AimPoint(attacker) : origin;
                FVector to = victim != null ? AimPoint(victim) : target;
                if (!IsSingleRayBlocked(world, from, to, victim))
                {
                    return false;
                }

                FVector highFrom = from + FVector.UpVector * 180f;
                FVector highTo = to + FVector.UpVector * 220f;
                return IsSingleRayBlocked(world, highFrom, highTo, victim);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsSingleRayBlocked(UWorld world, FVector origin, FVector target, AActor victim)
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
            float hitZ = target.Z;
            if (hitSomething && hit != null)
            {
                if (IsSameActorOrAttached(hit.HitActor, victim))
                {
                    hitTarget = true;
                }

                hitDistance = (float)(hit.HitLocation - origin).Size();
                hitZ = (float)hit.HitLocation.Z;
            }

            float radius = EstimateRadius(victim);

            return OmniHitRules.IsTerrainBlocking(
                hitSomething,
                hitTarget,
                hitDistance,
                targetDistance,
                radius,
                (float)origin.Z,
                (float)target.Z,
                hitZ);
        }

        private static FVector RaisedPoint(AActor actor)
        {
            return AimPoint(actor);
        }

        private static FVector AimPoint(AActor actor)
        {
            FVector location = BGUFuncLibActorTransformCS.BGUGetActorLocation(actor);
            float raise = Math.Max(EstimateHalfHeight(actor) * 0.55f, 80f);
            return location + FVector.UpVector * raise;
        }

        private static float DistanceToActor(FVector origin, AActor actor, FVector fallbackTarget)
        {
            float centerDistance = (float)(fallbackTarget - origin).Size();
            float radius = EstimateRadius(actor);
            return Math.Max(0f, centerDistance - radius);
        }

        private static float EstimateRadius(AActor actor)
        {
            float radius = 160f;
            try
            {
                FVector origin;
                FVector boxExtent;
                actor.GetActorBounds(false, out origin, out boxExtent);
                float horizontal = (float)Math.Max(boxExtent.X, boxExtent.Y);
                float vertical = (float)boxExtent.Z;
                radius = Math.Max(Math.Max(horizontal, vertical * 0.6f), 160f);
            }
            catch
            {
                BGUCharacterCS character = actor as BGUCharacterCS;
                if (character != null && character.CapsuleComponent != null)
                {
                    radius = Math.Max((float)character.CapsuleComponent.GetScaledCapsuleRadius(), 160f);
                }
            }

            return Math.Min(radius, 8000f);
        }

        private static float EstimateHalfHeight(AActor actor)
        {
            try
            {
                FVector origin;
                FVector boxExtent;
                actor.GetActorBounds(false, out origin, out boxExtent);
                return Math.Max((float)boxExtent.Z, 80f);
            }
            catch
            {
                BGUCharacterCS character = actor as BGUCharacterCS;
                if (character != null && character.CapsuleComponent != null)
                {
                    return Math.Max((float)character.CapsuleComponent.GetScaledCapsuleHalfHeight(), 80f);
                }
            }

            return 80f;
        }

        private static bool IsSameActorOrAttached(AActor hitActor, AActor victim)
        {
            if (hitActor == null || victim == null)
            {
                return false;
            }

            if (hitActor == victim || hitActor == victim.GetAttachParentActor())
            {
                return true;
            }

            AActor parent = hitActor.GetAttachParentActor();
            int guard = 0;
            while (parent != null && guard < 6)
            {
                if (parent == victim)
                {
                    return true;
                }

                parent = parent.GetAttachParentActor();
                guard++;
            }

            return string.Equals(hitActor.GetName(), victim.GetName(), StringComparison.Ordinal);
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
