using WukongCombatKit.Core;
using Xunit;

namespace WukongCombatKit.Tests
{
    public class CombatKitConfigTests
    {
        [Fact]
        public void MissingJsonUsesDefaults()
        {
            CombatKitConfig config = CombatKitConfig.Parse(null);
            Assert.True(config.EnableDodgeCancel);
            Assert.True(config.EnableOmniHit);
            Assert.Equal(2500f, config.MaxAttackRange);
            Assert.False(config.DebugLog);
        }

        [Fact]
        public void PartialJsonKeepsMissingDefaults()
        {
            CombatKitConfig config = CombatKitConfig.Parse("{\"EnableOmniHit\": false, \"DebugLog\": true}");
            Assert.True(config.EnableDodgeCancel);
            Assert.False(config.EnableOmniHit);
            Assert.Equal(2500f, config.MaxAttackRange);
            Assert.True(config.DebugLog);
        }

        [Fact]
        public void InvalidJsonDoesNotThrowAndKeepsDefaults()
        {
            CombatKitConfig config = CombatKitConfig.Parse("{not-json");
            Assert.True(config.EnableDodgeCancel);
            Assert.True(config.EnableOmniHit);
            Assert.Equal(2500f, config.MaxAttackRange);
        }

        [Fact]
        public void ImmediateDodgeAliasOverridesLegacyKey()
        {
            CombatKitConfig config = CombatKitConfig.Parse("{\"EnableDodgeCancel\": true, \"EnableImmediateDodge\": false}");
            Assert.False(config.EnableDodgeCancel);
        }
    }
}
