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
                if (doAttackLogic == null)
                {
                    ModLog.Error("DodgeCancel disabled: DoAttackLogic not found");
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
                    ModLog.Error("DodgeCancel disabled: CheckDodgeState not found");
                    Available = false;
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
                Available = false;
                ModLog.Error("DodgeCancel CheckDodgeState patch failed: " + ex.Message);
            }
        }

        public static void DoAttackLogicPrefix(object __instance, EInputActionType InputActionType)
        {
            try
            {
                if (!ConfigStore.Current.EnableDodgeCancel || InputActionType != EInputActionType.Dodge)
                {
                    return;
                }

                BGUCharacterCS player = ResolveOwner(__instance) as BGUCharacterCS;
                if (!ShouldAllow(player))
                {
                    return;
                }

                IBUC_UnitStateData unitState = BGU_DataUtil.GetReadOnlyData<IBUC_UnitStateData, BUC_UnitStateData>(player);
                if (unitState == null || unitState.HasState(EBGUUnitState.InDodgeWindow))
                {
                    return;
                }

                BUS_GSEventCollection events = BUS_EventCollectionCS.Get(player);
                if (events == null)
                {
                    return;
                }

                events.Evt_UnitStateTrigger.Invoke(EBUStateTrigger.EnterDodgeWindow, 0.2f, true);
                ModLog.Debug("Opened dodge window for light-attack cancel");
            }
            catch (Exception ex)
            {
                ModLog.Error("DodgeCancel DoAttackLogic: " + ex.Message);
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

                return DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
                {
                    Enabled = ConfigStore.Current.EnableDodgeCancel,
                    IsLocalPlayer = player is BGUPlayerCharacterCS,
                    IsAttacking = unitState.HasState(EBGUUnitState.Attacking),
                    IsBeatback = unitState.HasState(EBGUUnitState.Beatback),
                    IsDeadOrDying = unitState.HasState(EBGUUnitState.Dead) || unitState.HasState(EBGUUnitState.LifeSavingHair_FakeDead),
                    IsCharging = charge != null && charge.IsCastingChargeSkill,
                    IsTransforming = transforming,
                    IsAlreadyDodging = unitState.HasState(EBGUUnitState.InDodgeWindow) && skillType == "RollSkill",
                    IsCastingMagic = unitState.HasState(EBGUUnitState.InMagicWindow) && !DodgeCancelRules.IsNormalStaffAttack(skillType),
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
