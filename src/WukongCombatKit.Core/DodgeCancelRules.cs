using System;

namespace WukongCombatKit.Core
{
    public sealed class DodgeCancelContext
    {
        public bool Enabled { get; set; }
        public bool IsLocalPlayer { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsBeatback { get; set; }
        public bool IsDeadOrDying { get; set; }
        public bool IsCharging { get; set; }
        public bool IsTransforming { get; set; }
        public bool IsAlreadyDodging { get; set; }
        public bool IsCastingMagic { get; set; }
        public bool IsCastingVigor { get; set; }
        public string SkillType { get; set; }
        public string LastAction { get; set; }
    }

    public static class DodgeCancelRules
    {
        public static bool ShouldAllowDodgeCancel(DodgeCancelContext context)
        {
            if (context == null || !context.Enabled || !context.IsLocalPlayer)
            {
                return false;
            }

            if (!context.IsAttacking)
            {
                return false;
            }

            if (context.IsBeatback || context.IsDeadOrDying || context.IsAlreadyDodging)
            {
                return false;
            }

            if (context.IsCharging || context.IsTransforming || context.IsCastingMagic || context.IsCastingVigor)
            {
                return false;
            }

            if (!IsLightAttackCombo(context.SkillType, context.LastAction))
            {
                return false;
            }

            return true;
        }

        public static bool IsLightAttackCombo(string skillType, string lastAction)
        {
            if (!string.IsNullOrEmpty(skillType) && !IsNormalStaffAttack(skillType))
            {
                return false;
            }

            if (string.IsNullOrEmpty(lastAction))
            {
                return true;
            }

            if (ContainsToken(lastAction, "Heavy") ||
                ContainsToken(lastAction, "Charge") ||
                ContainsToken(lastAction, "Spell") ||
                ContainsToken(lastAction, "Magic") ||
                ContainsToken(lastAction, "Vigor") ||
                ContainsToken(lastAction, "Transform") ||
                ContainsToken(lastAction, "BianShen") ||
                ContainsToken(lastAction, "ShenFa"))
            {
                return false;
            }

            if (ContainsToken(lastAction, "Light") ||
                ContainsToken(lastAction, "Combo") ||
                ContainsToken(lastAction, "Normal") ||
                ContainsToken(lastAction, "Attack"))
            {
                return true;
            }

            return string.IsNullOrEmpty(skillType) || IsNormalStaffAttack(skillType);
        }

        public static bool IsNormalStaffAttack(string skillType)
        {
            if (string.IsNullOrEmpty(skillType))
            {
                return false;
            }

            return string.Equals(skillType, "NormalSkill", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsToken(string value, string token)
        {
            return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
