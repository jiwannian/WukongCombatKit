using System;
using System.Collections.Generic;

namespace WukongCombatKit.Core
{
    public sealed class OmniHitCandidate
    {
        public string Id { get; set; }
        public float Distance { get; set; }
        public bool IsSelf { get; set; }
        public bool IsAlly { get; set; }
        public bool IsEnemy { get; set; }
        public bool IsSceneObject { get; set; }
        public bool WallBlocked { get; set; }
    }

    public static class OmniHitRules
    {
        public static List<string> SelectVisibleTargets(IEnumerable<OmniHitCandidate> candidates, float maxAttackRange, bool originalHitsSceneObjects)
        {
            List<string> selected = new List<string>();
            if (candidates == null)
            {
                return selected;
            }

            foreach (OmniHitCandidate candidate in candidates)
            {
                if (candidate == null || string.IsNullOrEmpty(candidate.Id))
                {
                    continue;
                }

                if (candidate.IsSelf || candidate.IsAlly)
                {
                    continue;
                }

                if (candidate.Distance > maxAttackRange)
                {
                    continue;
                }

                if (candidate.WallBlocked)
                {
                    continue;
                }

                if (candidate.IsSceneObject)
                {
                    if (originalHitsSceneObjects)
                    {
                        selected.Add(candidate.Id);
                    }

                    continue;
                }

                if (candidate.IsEnemy)
                {
                    selected.Add(candidate.Id);
                }
            }

            return selected;
        }

        public static bool IsTerrainBlocking(
            bool hitSomething,
            bool hitTargetOrAttached,
            float hitDistance,
            float targetDistance,
            float targetRadius,
            float originZ,
            float targetZ,
            float hitZ)
        {
            if (!hitSomething || hitTargetOrAttached)
            {
                return false;
            }

            float nearTarget = Math.Max(targetRadius, 150f);
            if (hitDistance >= targetDistance - nearTarget)
            {
                return false;
            }

            float lowerZ = Math.Min(originZ, targetZ);
            float upperZ = Math.Max(originZ, targetZ);
            float verticalGap = upperZ - lowerZ;
            float remaining = targetDistance - hitDistance;
            float bodyBottom = Math.Min(originZ, targetZ) - Math.Max(targetRadius * 0.35f, 80f);
            float bodyTop = Math.Max(originZ, targetZ) + Math.Max(targetRadius, 160f);

            if (hitZ < lowerZ - 40f)
            {
                return false;
            }

            if (verticalGap >= 120f && hitZ <= lowerZ + 80f)
            {
                return false;
            }

            if (remaining <= nearTarget && hitZ >= bodyBottom && hitZ <= bodyTop)
            {
                return false;
            }

            return true;
        }
    }
}
