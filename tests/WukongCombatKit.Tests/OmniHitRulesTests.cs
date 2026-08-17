using System.Collections.Generic;
using WukongCombatKit.Core;
using Xunit;

namespace WukongCombatKit.Tests
{
    public class OmniHitRulesTests
    {
        [Fact]
        public void SelectsUnblockedEnemiesInAnyDirection()
        {
            List<string> hits = OmniHitRules.SelectVisibleTargets(new[]
            {
                new OmniHitCandidate { Id = "front", Distance = 300f, IsEnemy = true },
                new OmniHitCandidate { Id = "behind", Distance = 90000f, IsEnemy = true },
                new OmniHitCandidate { Id = "side", Distance = 12f, IsEnemy = true }
            }, CombatKitConfig.DefaultMaxAttackRange, false);

            Assert.Contains("front", hits);
            Assert.Contains("behind", hits);
            Assert.Contains("side", hits);
        }

        [Fact]
        public void SkipsWallBlockedAndOutOfRangeAndAllies()
        {
            List<string> hits = OmniHitRules.SelectVisibleTargets(new[]
            {
                new OmniHitCandidate { Id = "walled", Distance = 10f, IsEnemy = true, WallBlocked = true },
                new OmniHitCandidate { Id = "far", Distance = 100001f, IsEnemy = true },
                new OmniHitCandidate { Id = "self", Distance = 0f, IsSelf = true, IsEnemy = false },
                new OmniHitCandidate { Id = "ally", Distance = 20f, IsAlly = true },
                new OmniHitCandidate { Id = "ok", Distance = 20f, IsEnemy = true }
            }, CombatKitConfig.DefaultMaxAttackRange, false);

            Assert.Equal(new[] { "ok" }, hits);
        }

        [Fact]
        public void SceneObjectsOnlyWhenOriginalAttackWouldHitThem()
        {
            OmniHitCandidate crate = new OmniHitCandidate
            {
                Id = "crate",
                Distance = 30f,
                IsSceneObject = true
            };

            Assert.Empty(OmniHitRules.SelectVisibleTargets(new[] { crate }, CombatKitConfig.DefaultMaxAttackRange, false));
            Assert.Equal(new[] { "crate" }, OmniHitRules.SelectVisibleTargets(new[] { crate }, CombatKitConfig.DefaultMaxAttackRange, true));
        }
    }
}
