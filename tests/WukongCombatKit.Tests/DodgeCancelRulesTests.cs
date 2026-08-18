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
        public void ChargeOrSpellOrTransform_AllowsDodgeWhenAttacking()
        {
            Assert.True(DodgeCancelRules.ShouldAllowDodgeCancel(CreateBlocked("ChargeSkillBegin", "HeavyAttack")));
            Assert.True(DodgeCancelRules.ShouldAllowDodgeCancel(CreateBlocked("NormalSkill", "SpellCast")));
            Assert.True(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
                IsAttacking = true,
                IsCharging = true,
                SkillType = "ChargeSkillBegin",
                LastAction = "HeavyAttack"
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
        public void BeatbackDuringAttack_AllowsDodgeCancel()
        {
            Assert.True(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
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
        public void AttackOrDodge_AllowsImmediateDodge()
        {
            Assert.True(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
                IsAttacking = true,
                SkillType = "ChargeSkillBegin",
                LastAction = "HeavyAttack"
            }));
            Assert.True(DodgeCancelRules.ShouldAllowDodgeCancel(new DodgeCancelContext
            {
                Enabled = true,
                IsLocalPlayer = true,
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
