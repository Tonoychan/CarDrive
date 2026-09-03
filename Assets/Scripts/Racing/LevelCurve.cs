using UnityEngine;

namespace Racing
{
    /// Formula-based XP/level curve -- two tunable numbers instead of a hand-authored
    /// table, so it scales to any level without ever running out of entries. XP to
    /// reach the next level grows geometrically: xpToNext(level) = baseXP *
    /// growthRate^(level-1). Designers tune feel via baseXP/growthRate in the
    /// Inspector; nothing about leveling is hardcoded in PlayerProgress.
    [CreateAssetMenu(fileName = "LevelCurve", menuName = "Racing/Level Curve")]
    public class LevelCurve : ScriptableObject
    {
        [Tooltip("XP required to go from level 1 to level 2.")]
        [Min(1)] public int baseXP = 100;

        [Tooltip("Multiplier applied to the XP requirement per level after that " +
                 "(1.0 = flat, >1.0 = each level takes progressively more XP).")]
        [Min(1f)] public float growthRate = 1.15f;

        [Tooltip("Upper bound only for LevelForXP's search loop -- not a hard level " +
                 "cap, just a safety limit against runaway XP values.")]
        [Min(1)] public int maxLevel = 999;

        /// XP required to advance from `level` to `level + 1`.
        public int XPToNextLevel(int level)
        {
            level = Mathf.Max(1, level);
            double xp = baseXP * System.Math.Pow(growthRate, level - 1);
            return Mathf.Max(1, Mathf.RoundToInt((float)xp));
        }

        /// Total cumulative XP needed to reach `level` starting from level 1.
        public int TotalXPForLevel(int level)
        {
            int total = 0;
            for (int l = 1; l < level; l++)
                total += XPToNextLevel(l);
            return total;
        }

        /// The level reached given a total accumulated XP amount.
        public int LevelForXP(int totalXP)
        {
            int level = 1;
            int spent = 0;
            while (level < maxLevel)
            {
                int need = XPToNextLevel(level);
                if (spent + need > totalXP) break;
                spent += need;
                level++;
            }
            return level;
        }
    }
}
