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
    }
}
