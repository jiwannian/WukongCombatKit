using System;
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
                if (checkDodgeState == null)
                {
                    ModLog.Error("DodgeCancel CheckDodgeState not found");
                    return;
                }

                harmony.Patch(
                    checkDodgeState,
                    postfix: new HarmonyMethod(typeof(DodgeCancel), nameof(CheckDodgeStatePostfix)));
                ModLog.Info("Patch registered: GSSkillCastChecker.CheckDodgeState");
                Available = true;
            }
            catch (Exception ex)
            {
                ModLog.Error("DodgeCancel CheckDodgeState patch failed: " + ex.Message);
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

                OpenDodgeWindow(player);
                ESkillDirection direction = ReadDodgeDirection(player);
                if (_triggerDodge != null)
                {
                    _triggerDodge.Invoke(__instance, new object[] { direction });
                }

                if (_tryTriggerRealDodge != null)
                {
                    _tryTriggerRealDodge.Invoke(__instance, null);
                }
                else
                {
                    BUS_GSEventCollection events = BUS_EventCollectionCS.Get(player);
                    if (events != null)
                    {
                        events.Evt_BeginPreciseDodge.Invoke(direction);
                    }
                }

                ModLog.Debug("Same-frame dodge cancel from light attack");
                return false;
            }
            catch (Exception ex)
            {
                ModLog.Error("DodgeCancel DoAttackLogic: " + ex.Message);
                return true;
            }
        }

        public static void CheckDodgeStatePostfix(object __instance, ref bool __result)
        {
            try
            {
                if (__result || !ConfigStore.Current.EnableDodgeCancel)
                {
                    return;
                }

                BGUCharacterCS player = MyMod.GetPlayerCharacter();
                if (ShouldAllow(player))
                {
                    __result = true;
                    ModLog.Debug("CheckDodgeState allowed for light-attack cancel");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("DodgeCancel CheckDodgeState: " + ex.Message);
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
                IBUC_ChargeSkillData charge = BGU_DataUtil.GetReadOnlyData<IBUC_ChargeSkillData, BUC_ChargeSkillData>(player);
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

                bool transforming = false;
                try
                {
                    AController controller = player.GetController();
                    if (controller != null)
                    {
                        IBPC_PlayerTagData tags = BGU_DataUtil.GetReadOnlyData<IBPC_PlayerTagData, BPC_PlayerTagData>(controller);
                        transforming = tags != null && tags.HasTag(EBGPPlayerTag.Transforming);
                    }
                }
                catch
                {
                    transforming = false;
                }

                bool alreadyDodging = DodgeCancelRules.IsRollSkill(skillType) || unitState.HasState(EBGUUnitState.InDodgeWindow);
                return DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
                {
                    Enabled = ConfigStore.Current.EnableDodgeCancel,
                    IsLocalPlayer = player is BGUPlayerCharacterCS,
                    IsAttacking = unitState.HasState(EBGUUnitState.Attacking),
                    IsBeatback = unitState.HasState(EBGUUnitState.Beatback),
                    IsDeadOrDying = unitState.HasState(EBGUUnitState.Dead) || unitState.HasState(EBGUUnitState.LifeSavingHair_FakeDead),
                    IsCharging = charge != null && charge.IsCastingChargeSkill,
                    IsTransforming = transforming,
                    IsAlreadyDodging = alreadyDodging,
                    AllowCancelCurrentDodge = true,
                    IsCastingMagic = unitState.HasState(EBGUUnitState.InMagicWindow) && !string.IsNullOrEmpty(skillType) && !DodgeCancelRules.IsNormalStaffAttack(skillType) && !DodgeCancelRules.IsRollSkill(skillType),
                    IsCastingVigor = unitState.HasState(EBGUUnitState.InVigorWindow),
                    SkillType = skillType,
                    LastAction = lastAction
                });
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void OpenDodgeWindow(BGUCharacterCS player)
        {
            BUS_GSEventCollection events = BUS_EventCollectionCS.Get(player);
            if (events == null)
            {
                return;
            }

            events.Evt_UnitStateTrigger.Invoke(EBUStateTrigger.EnterDodgeWindow, 0.25f, true);
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
