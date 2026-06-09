using System.Collections.Generic;
using UnityEngine;

namespace CyberMinefield.Levels
{
    public sealed class LevelManager : MonoBehaviour
    {
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();
        [SerializeField] private int currentLevelIndex;

        public int CurrentLevelIndex => currentLevelIndex;
        public int LevelCount => levels.Count;
        public LevelDefinition CurrentLevel => levels[Mathf.Clamp(currentLevelIndex, 0, levels.Count - 1)];

        private void Awake()
        {
            EnsureDefaultLevels();
        }

        private void Reset()
        {
            EnsureDefaultLevels();
        }

        public void EnsureDefaultLevels()
        {
            if (levels.Count > 0 && !HasOldDefaultLevels())
            {
                return;
            }

            levels.Clear();

            levels.Add(new LevelDefinition(
                "Tutorial: Firewall Basics",
                5,
                5,
                3,
                3,
                0f,
                new Vector2Int(2, 2),
                new Vector2Int(4, 4),
                WinConditionType.ClearSafeTiles,
                9));

            levels.Add(new LevelDefinition(
                "Level 1: Breach Map",
                6,
                6,
                5,
                5,
                0f,
                new Vector2Int(3, 3),
                new Vector2Int(5, 5),
                WinConditionType.ClearSafeTiles,
                19));

            levels.Add(new LevelDefinition(
                "Level 2: Nexus Core",
                7,
                7,
                11,
                11,
                0f,
                new Vector2Int(3, 3),
                new Vector2Int(6, 6),
                WinConditionType.ClearSafeTiles,
                29));

            levels.Add(new LevelDefinition(
                "Level 3: Packet Storm",
                8,
                8,
                14,
                14,
                0f,
                new Vector2Int(4, 4),
                new Vector2Int(7, 7),
                WinConditionType.ClearSafeTiles,
                39));

            levels.Add(new LevelDefinition(
                "Level 4: Blackout Grid",
                9,
                9,
                18,
                18,
                0f,
                new Vector2Int(4, 4),
                new Vector2Int(8, 8),
                WinConditionType.ClearSafeTiles,
                49));
        }

        private bool HasOldDefaultLevels()
        {
            if (levels.Count != 3 && levels.Count != 5)
            {
                return false;
            }

            bool hasKnownDefaultNames = levels[0].LevelName == "Tutorial: Firewall Basics"
                && levels[1].LevelName == "Level 1: Breach Map"
                && levels[2].LevelName == "Level 2: Nexus Core";

            if (!hasKnownDefaultNames)
            {
                return false;
            }

            if (levels.Count == 3)
            {
                return true;
            }

            return levels[3].LevelName == "Level 3: Packet Storm"
                && levels[4].LevelName == "Level 4: Blackout Grid";
        }

        public LevelDefinition SetCurrentLevel(int index)
        {
            EnsureDefaultLevels();
            currentLevelIndex = Mathf.Clamp(index, 0, levels.Count - 1);
            return CurrentLevel;
        }

        public LevelDefinition AdvanceLevel()
        {
            return SetCurrentLevel(currentLevelIndex + 1);
        }

        public bool HasNextLevel()
        {
            return currentLevelIndex + 1 < levels.Count;
        }
    }
}
