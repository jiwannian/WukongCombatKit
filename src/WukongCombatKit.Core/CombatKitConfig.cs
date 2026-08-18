using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WukongCombatKit.Core
{
    public sealed class CombatKitConfig
    {
        public const float DefaultMaxAttackRange = 2500f;

        public bool EnableDodgeCancel { get; set; } = true;
        public bool EnableOmniHit { get; set; } = true;
        public float MaxAttackRange { get; set; } = DefaultMaxAttackRange;
        public bool DebugLog { get; set; }

        public static CombatKitConfig CreateDefault()
        {
            return new CombatKitConfig();
        }

        public static CombatKitConfig Parse(string json)
        {
            CombatKitConfig config = CreateDefault();
            if (string.IsNullOrWhiteSpace(json))
            {
                return config;
            }

            bool enableDodgeCancel;
            if (TryReadBool(json, "EnableImmediateDodge", out enableDodgeCancel) ||
                TryReadBool(json, "EnableDodgeCancel", out enableDodgeCancel))
            {
                config.EnableDodgeCancel = enableDodgeCancel;
            }

            bool enableOmniHit;
            if (TryReadBool(json, "EnableOmniHit", out enableOmniHit))
            {
                config.EnableOmniHit = enableOmniHit;
            }

            float maxAttackRange;
            if (TryReadFloat(json, "MaxAttackRange", out maxAttackRange))
            {
                config.MaxAttackRange = maxAttackRange;
            }

            bool debugLog;
            if (TryReadBool(json, "DebugLog", out debugLog))
            {
                config.DebugLog = debugLog;
            }

            return config;
        }

        private static bool TryReadBool(string json, string key, out bool value)
        {
            value = false;
            Match match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            value = string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        private static bool TryReadFloat(string json, string key, out float value)
        {
            value = 0f;
            Match match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            return float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public string ToJson()
        {
            return "{" + Environment.NewLine +
                   "  \"EnableImmediateDodge\": " + (EnableDodgeCancel ? "true" : "false") + "," + Environment.NewLine +
                   "  \"EnableOmniHit\": " + (EnableOmniHit ? "true" : "false") + "," + Environment.NewLine +
                   "  \"MaxAttackRange\": " + MaxAttackRange.ToString(CultureInfo.InvariantCulture) + "," + Environment.NewLine +
                   "  \"DebugLog\": " + (DebugLog ? "true" : "false") + Environment.NewLine +
                   "}" + Environment.NewLine;
        }
    }
}
