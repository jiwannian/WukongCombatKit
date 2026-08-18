using System;
using b1;
using CSharpModBase;
using HarmonyLib;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace WukongCombatKit
{
    public class MyMod : ICSharpMod
    {
        public string Name => "WukongCombatKit";
        public string Version => "1.0.0";

        private Harmony _harmony;

        public void Init()
        {
            try
            {
                ConfigStore.Load();
                ModLog.Info("WukongCombatKit C# mod Init v" + Version);
                _harmony = new Harmony("WukongCombatKit");
                DodgeCancel.Register(_harmony);
                OmniHit.Register(_harmony);
                CSharpModBase.Utils.RegisterKeyBind(CSharpModBase.Input.Key.F8, ReloadConfig);
                CSharpModBase.Utils.RegisterKeyBind(CSharpModBase.Input.Key.F7, ToggleImmediateDodge);
                ModLog.Info("Init finished. ImmediateDodge=" + ConfigStore.Current.EnableDodgeCancel +
                            " OmniHit=" + ConfigStore.Current.EnableOmniHit +
                            " MaxAttackRange=" + ConfigStore.Current.MaxAttackRange);
            }
            catch (Exception ex)
            {
                ModLog.Error("Init failed: " + ex);
            }
        }

        public void DeInit()
        {
            try
            {
                if (_harmony != null)
                {
                    _harmony.UnpatchAll("WukongCombatKit");
                }

                ModLog.Info("WukongCombatKit DeInit");
            }
            catch (Exception ex)
            {
                ModLog.Error("DeInit failed: " + ex.Message);
            }
        }

        private static void ReloadConfig()
        {
            ConfigStore.Reload();
            ModLog.Info("Config reloaded. ImmediateDodge=" + ConfigStore.Current.EnableDodgeCancel +
                        " OmniHit=" + ConfigStore.Current.EnableOmniHit);
        }

        private static void ToggleImmediateDodge()
        {
            ConfigStore.ToggleImmediateDodge();
            ModLog.Info("ImmediateDodge=" + (ConfigStore.Current.EnableDodgeCancel ? "ON" : "OFF"));
        }

        public static BGUCharacterCS GetPlayerCharacter()
        {
            try
            {
                UWorld world = GetWorld();
                if (world == null)
                {
                    return null;
                }

                APlayerController controller = UGSE_EngineFuncLib.GetFirstLocalPlayerController(world);
                if (controller == null)
                {
                    return null;
                }

                return controller.GetControlledPawn() as BGUCharacterCS;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static UWorld GetWorld()
        {
            try
            {
                UObjectRef uobjectRef = GCHelper.FindRef(FGlobals.GWorld);
                return uobjectRef != null ? uobjectRef.Managed as UWorld : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
