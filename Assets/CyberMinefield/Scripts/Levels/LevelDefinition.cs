using System;
using UnityEngine;

namespace CyberMinefield.Levels
{
    [Serializable]
    public sealed class LevelDefinition
    {
        [SerializeField] private string levelName = "Tutorial";
        [SerializeField] private int width = 5;
        [SerializeField] private int height = 5;
        [SerializeField] private int dangerCount = 3;
        [SerializeField] private int defuserLimit = 3;
        [SerializeField] private float timeLimit;
        [SerializeField] private Vector2Int startPosition = Vector2Int.zero;
        [SerializeField] private Vector2Int exitPosition = new Vector2Int(4, 4);
        [SerializeField] private WinConditionType winCondition = WinConditionType.ClearSafeTiles;
        [SerializeField] private int seed = 9;

        public string LevelName => levelName;
        public int Width => width;
        public int Height => height;
        public int DangerCount => dangerCount;
        public int DefuserLimit => defuserLimit;
        public float TimeLimit => timeLimit;
        public Vector2Int StartPosition => startPosition;
        public Vector2Int ExitPosition => exitPosition;
        public WinConditionType WinCondition => winCondition;
        public int Seed => seed;

        public LevelDefinition(
            string levelName,
            int width,
            int height,
            int dangerCount,
            int defuserLimit,
            float timeLimit,
            Vector2Int startPosition,
            Vector2Int exitPosition,
            WinConditionType winCondition,
            int seed)
        {
            this.levelName = levelName;
            this.width = width;
            this.height = height;
            this.dangerCount = dangerCount;
            this.defuserLimit = defuserLimit;
            this.timeLimit = timeLimit;
            this.startPosition = startPosition;
            this.exitPosition = exitPosition;
            this.winCondition = winCondition;
            this.seed = seed;
        }
    }
}
