using WukongCombatKit.Core;
using Xunit;

namespace WukongCombatKit.Tests
{
    public class DodgeCancelRulesTests
    {
        [Fact]
        public void LightAttackCombo_AllowsDodgeCancel()
        {
            Assert.True(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
                IsAttacking = true,
                SkillType = "NormalSkill",
                LastAction = "LightAttack"
            }));
        }

        [Fact]
        public void UnknownSkillDuringAttack_AllowsDodgeCancel()
        {
            Assert.True(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
                IsAttacking = true,
                SkillType = null,
                LastAction = null
            }));
            Assert.True(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
                IsAttacking = true,
                SkillType = "",
                LastAction = "EAttackLight"
            }));
        }

        [Fact]
        public void ChargeOrSpellOrTransform_DoesNotAllowDodgeCancel()
        {
            Assert.False(DodgeCancelRules.ShouldAllowDodgeCancel(CreateBlocked("ChargeSkillBegin", "HeavyAttack")));
            Assert.False(DodgeCancelRules.ShouldAllowDodgeCancel(CreateBlocked("NormalSkill", "SpellCast") ));
            Assert.False(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
                IsAttacking = true,
                IsCharging = true,
                SkillType = "ChargeSkillBegin",
                LastAction = "HeavyAttack"
            }));
            Assert.False(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
                IsAttacking = true,
                IsTransforming = true,
                SkillType = "NormalSkill",
                LastAction = "Transform"
            }));
            Assert.False(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
                IsAttacking = true,
                IsCastingMagic = true,
                SkillType = "NormalSkill",
                LastAction = "Magic"
            }));
        }

        [Fact]
        public void DisabledOrNonPlayer_DoesNotAllowDodgeCancel()
        {
            Assert.False(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = false,
                IsLocalPlayer = true,
                IsAttacking = true,
                SkillType = "NormalSkill",
                LastAction = "LightAttack"
            }));
            Assert.False(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = false,
                IsAttacking = true,
                SkillType = "NormalSkill",
                LastAction = "LightAttack"
            }));
        }

        [Fact]
        public void Beatback_DoesNotAllowDodgeCancel()
        {
            Assert.False(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
                IsAttacking = true,
                IsBeatback = true,
                SkillType = "NormalSkill",
                LastAction = "LightAttack"
            }));
        }

        [Fact]
        public void AlreadyDodging_AllowsImmediateNextDodge()
        {
            Assert.True(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
                IsAttacking = true,
                IsAlreadyDodging = true,
                AllowCancelCurrentDodge = true,
                SkillType = "RollSkill",
                LastAction = "Dodge"
            }));
        }

        private static DodgeCancelContext CreateBlocked(string skillType, string lastAction)
        {
            return new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
                IsAttacking = true,
                SkillType = skillType,
                LastAction = lastAction
            };
        }
    }
}
