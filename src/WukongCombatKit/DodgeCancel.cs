using System;
using System.Collections.Generic;
using System.Reflection;
using b1;
using BtlShare;
using HarmonyLib;
using UnrealEngine.Engine;
using WukongCombatKit.Core;

namespace WukongCombatKit
{
    public static class DodgeCancel
    {
        public static bool Available { get; private set; }
        private static MethodInfo _triggerDodge;
        private static MethodInfo _tryTriggerRealDodge;

        public static void Register(Harmony harmony)
        {
            Available = false;
            if (harmony == null)
            {
                return;
            }

            try
            {
                MethodInfo doAttackLogic = AccessTools.Method(typeof(BUS_PlayerInputActionComp), "DoAttackLogic");
                _triggerDodge = AccessTools.Method(typeof(BUS_PlayerInputActionComp), "TriggerDodge");
                _tryTriggerRealDodge = AccessTools.Method(typeof(BUS_PlayerInputActionComp), "TryTriggerRealDodge");
                if (doAttackLogic == null || _triggerDodge == null)
                {
                    ModLog.Error("DodgeCancel disabled: DoAttackLogic/TriggerDodge not found");
                }
                else
                {
                    harmony.Patch(
                        doAttackLogic,
                        prefix: new HarmonyMethod(typeof(DodgeCancel), nameof(DoAttackLogicPrefix)));
                    Available = true;
                    ModLog.Info("Patch registered: BUS_PlayerInputActionComp.DoAttackLogic");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("DodgeCancel DoAttackLogic patch failed: " + ex.Message);
            }

            try
            {
                MethodInfo checkDodgeState = AccessTools.Method(typeof(GSSkillCastChecker), "CheckDodgeState");
                if (checkDodgeState != null)
                {
                    harmony.Patch(checkDodgeState, postfix: new HarmonyMethod(typeof(DodgeCancel), nameof(CheckPassPostfix)));
                    ModLog.Info("Patch registered: GSSkillCastChecker.CheckDodgeState");
                    Available = true;
                }

                MethodInfo checkState = AccessTools.Method(typeof(GSSkillCastChecker), "CheckState");
                if (checkState != null)
                {
                    harmony.Patch(checkState, postfix: new HarmonyMethod(typeof(DodgeCancel), nameof(CheckPassPostfix)));
                    ModLog.Info("Patch registered: GSSkillCastChecker.CheckState");
                }

                MethodInfo checkCoolDown = AccessTools.Method(typeof(GSSkillCastChecker), "CheckCoolDown");
                if (checkCoolDown != null)
                {
                    harmony.Patch(checkCoolDown, postfix: new HarmonyMethod(typeof(DodgeCancel), nameof(CheckPassPostfix)));
                    ModLog.Info("Patch registered: GSSkillCastChecker.CheckCoolDown");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("DodgeCancel checker patch failed: " + ex.Message);
            }
        }

        public static bool DoAttackLogicPrefix(object __instance, EInputActionType InputActionType)
        {
            try
            {
                if (!ConfigStore.Current.EnableDodgeCancel || InputActionType != EInputActionType.Dodge)
                {
                    return true;
                }

                BGUCharacterCS player = ResolveOwner(__instance) as BGUCharacterCS;
                if (!ShouldAllow(player))
                {
                    return true;
                }

                PrepareImmediateDodge(player);
                ESkillDirection direction = ReadDodgeDirection(player);
                if (_triggerDodge != null)
                {
                    _triggerDodge.Invoke(__instance, new object[] { direction });
                }

                if (_tryTriggerRealDodge != null)
                {
                    _tryTriggerRealDodge.Invoke(__instance, null);
                }

                BUS_GSEventCollection events = BUS_EventCollectionCS.Get(player);
                if (events != null)
                {
                    if (events.Evt_BeginPreciseDodge != null)
                    {
                        events.Evt_BeginPreciseDodge.Invoke(direction);
                    }

                    if (events.Evt_TriggerRollSkill != null)
                    {
                        events.Evt_TriggerRollSkill.Invoke(direction);
                    }
                }

                ModLog.Debug("Immediate dodge from attack or roll");
                return false;
            }
            catch (Exception ex)
            {
                ModLog.Error("DodgeCancel DoAttackLogic: " + ex.Message);
                return true;
            }
        }

        public static void CheckPassPostfix(object __instance, ref bool __result)
        {
            try
            {
                if (__result || !ConfigStore.Current.EnableDodgeCancel)
                {
                    return;
                }

                BGUCharacterCS player = MyMod.GetPlayerCharacter();
                if (ShouldAllow(player) && IsCheckingRollSkill(__instance))
                {
                    __result = true;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("DodgeCancel checker: " + ex.Message);
            }
        }

        public static bool ShouldAllow(BGUCharacterCS player)
        {
            if (player == null)
            {
                return false;
            }

            try
            {
                IBUC_UnitStateData unitState = BGU_DataUtil.GetReadOnlyData<IBUC_UnitStateData, BUC_UnitStateData>(player);
                IBUC_SkillInstsData skillInsts = BGU_DataUtil.GetReadOnlyData<IBUC_SkillInstsData, BUC_SkillInstsData>(player);
                if (unitState == null)
                {
                    return false;
                }

                string skillType = null;
                string lastAction = null;
                if (skillInsts != null)
                {
                    lastAction = skillInsts.LastSkillKeyActionMapping;
                    FUStSkillSDesc desc = BGW_GameDB.GetSkillSDesc(skillInsts.CurrentCastingSkillID, player);
                    if (desc != null)
                    {
                        skillType = desc.SkillType.ToString();
                    }
                }

                bool alreadyDodging = DodgeCancelRules.IsRollSkill(skillType) || unitState.HasState(EBGUUnitState.InDodgeWindow);
                return DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
                {
                    Enabled = ConfigStore.Current.EnableDodgeCancel,
                    IsLocalPlayer = player is BGUPlayerCharacterCS,
                    IsAttacking = unitState.HasState(EBGUUnitState.Attacking),
                    IsBeatback = unitState.HasState(EBGUUnitState.Beatback),
                    IsDeadOrDying = unitState.HasState(EBGUUnitState.Dead) || unitState.HasState(EBGUUnitState.LifeSavingHair_FakeDead),
                    IsAlreadyDodging = alreadyDodging,
                    AllowCancelCurrentDodge = true,
                    SkillType = skillType,
                    LastAction = lastAction
                });
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void PrepareImmediateDodge(BGUCharacterCS player)
        {
            BUS_GSEventCollection events = BUS_EventCollectionCS.Get(player);
            if (events == null)
            {
                return;
            }

            try
            {
                if (events.Evt_UnitTryBreakSkill != null)
                {
                    events.Evt_UnitTryBreakSkill.Invoke("WukongCombatKit.ImmediateDodge");
                }
            }
            catch (Exception ex)
            {
                ModLog.Debug("Skill break skipped: " + ex.Message);
            }

            events.Evt_UnitStateTrigger.Invoke(EBUStateTrigger.SkillBreak, 0f, true);
            events.Evt_UnitStateTrigger.Invoke(EBUStateTrigger.EnterDodgeWindow, 0.25f, true);

            ResetRollCombo(player);
            ClearRollCooldown(player);
        }

        private static void ResetRollCombo(BGUCharacterCS player)
        {
            try
            {
                BUC_RollData roll = BGU_DataUtil.GetUnPersistentReadOnlyData<BUC_RollData>(player);
                if (roll != null)
                {
                    roll.CurStateIndex = 0;
                    roll.bCastRollingSkill = false;
                }
            }
            catch (Exception)
            {
            }
        }

        private static void ClearRollCooldown(BGUCharacterCS player)
        {
            try
            {
                BUC_SkillInstsData skillInsts = BGU_DataUtil.GetUnPersistentReadOnlyData<BUC_SkillInstsData>(player);
                if (skillInsts == null)
                {
                    return;
                }

                FieldInfo field = AccessTools.Field(typeof(BUC_SkillInstsData), "SkillCanCastCooldownRemainingTime");
                Dictionary<int, float> cooldown = field != null ? field.GetValue(skillInsts) as Dictionary<int, float> : null;
                if (cooldown == null)
                {
                    return;
                }

                BUC_RollData roll = BGU_DataUtil.GetUnPersistentReadOnlyData<BUC_RollData>(player);
                if (roll != null && roll.RollCombo != null)
                {
                    for (int i = 0; i < roll.RollCombo.Count; i++)
                    {
                        cooldown[roll.RollCombo[i]] = 0f;
                    }
                }

                if (skillInsts.CurrentCastingSkillID > 0)
                {
                    cooldown[skillInsts.CurrentCastingSkillID] = 0f;
                }
            }
            catch (Exception)
            {
            }
        }

        private static bool IsCheckingRollSkill(object checker)
        {
            if (checker == null)
            {
                return false;
            }

            try
            {
                FieldInfo descField = AccessTools.Field(checker.GetType(), "SkillSDesc");
                object desc = descField != null ? descField.GetValue(checker) : null;
                if (desc == null)
                {
                    return true;
                }

                PropertyInfo typeProp = AccessTools.Property(desc.GetType(), "SkillType");
                object skillType = typeProp != null ? typeProp.GetValue(desc, null) : null;
                return skillType != null && string.Equals(skillType.ToString(), "RollSkill", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static ESkillDirection ReadDodgeDirection(BGUCharacterCS player)
        {
            try
            {
                AController controller = player.GetController();
                IBPC_InputData input = controller != null
                    ? BGU_DataUtil.GetReadOnlyData<IBPC_InputData, BPC_InputData>(controller)
                    : null;
                IBUC_PlayerInputConfigData config = BGU_DataUtil.GetUnPersistentReadOnlyData<IBUC_PlayerInputConfigData, BUC_PlayerInputConfigData>(player);
                if (input == null)
                {
                    return ESkillDirection.Forward;
                }

                float sideways = input.GetInputValue(GSBattleActionEn.MoveSideways);
                float forward = input.GetInputValue(GSBattleActionEn.MoveForward);
                float fixLine = config != null ? config.DodgeInputFixLine : 0.4f;
                return BGUFuncLibInput.CalcInputDir(sideways, forward, fixLine);
            }
            catch (Exception)
            {
                return ESkillDirection.Forward;
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
