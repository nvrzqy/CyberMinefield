using System;
using System.Collections.Generic;
using CyberMinefield.Levels;
using UnityEngine;

namespace CyberMinefield.Grid
{
    public sealed class GridManager : MonoBehaviour
    {
        private const string GridParentName = "Grid";
        private const string BoardFloorName = "BoardFloor";

        [SerializeField] private int width = 5;
        [SerializeField] private int height = 5;
        [SerializeField] private int dangerCount = 3;
        [SerializeField] private int defuserLimit = 3;
        [SerializeField] private float tileSpacing = 1.15f;
        [SerializeField] private float tileScale = 1f;
        [SerializeField] private bool generateOnStart = false;
        [SerializeField] private bool useRandomSeed = false;
        [SerializeField] private int randomSeed = 9;
        [SerializeField] private Vector2Int startPosition = Vector2Int.zero;
        [SerializeField] private Vector2Int exitPosition = new Vector2Int(4, 4);
        [SerializeField] private int maxGenerationAttempts = 500;
        [SerializeField] private int minimumInitialRevealTiles = 9;
        [SerializeField] private float maxZeroTileRatio = 0.38f;
        [SerializeField] private bool showTutorialHints;

        private readonly Dictionary<Vector2Int, TileNode> tilesByCoordinate = new Dictionary<Vector2Int, TileNode>();
        private Transform gridParent;
        private int placedDefuserCount;
        private int safeTilesRemaining;

        public int Width => width;
        public int Height => height;
        public int DangerCount => dangerCount;
        public int DefuserLimit => defuserLimit;
        public int PlacedDefuserCount => placedDefuserCount;
        public int RemainingDefusers => Mathf.Max(0, defuserLimit - placedDefuserCount);
        public int SafeTilesRemaining => safeTilesRemaining;
        public float TileSpacing => tileSpacing;
        public Vector2Int StartPosition => startPosition;
        public Vector2Int ExitPosition => exitPosition;
        public Vector2Int TutorialTargetCoordinates { get; private set; } = new Vector2Int(int.MinValue, int.MinValue);
        public IReadOnlyDictionary<Vector2Int, TileNode> TilesByCoordinate => tilesByCoordinate;

        public event Action<TileNode> TileRevealed;
        public event Action<TileNode> DangerTriggered;
        public event Action SafeTilesCleared;
        public event Action DefuserCountChanged;

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateGrid();
            }
        }

        public void Configure(LevelDefinition level)
        {
            width = Mathf.Max(2, level.Width);
            height = Mathf.Max(2, level.Height);
            dangerCount = Mathf.Max(0, level.DangerCount);
            defuserLimit = Mathf.Max(0, level.DefuserLimit);
            startPosition = ClampToBoard(level.StartPosition);
            if (width >= 5 && height >= 5 && IsOnBoardEdge(startPosition))
            {
                startPosition = new Vector2Int(width / 2, height / 2);
            }

            exitPosition = ClampToBoard(level.ExitPosition);
            minimumInitialRevealTiles = Mathf.Max(
                minimumInitialRevealTiles,
                Mathf.Min(9, Mathf.Max(1, width * height - dangerCount)));
            useRandomSeed = false;
            showTutorialHints = level.LevelName.StartsWith("Tutorial");
        }

        [ContextMenu("Generate Grid")]
        public void GenerateGrid()
        {
            ClearGrid();
            gridParent = CreateGridParent();
            tilesByCoordinate.Clear();
            placedDefuserCount = 0;
            safeTilesRemaining = 0;

            if (useRandomSeed)
            {
                UnityEngine.Random.InitState(randomSeed);
            }

            startPosition = ClampToBoard(startPosition);
            exitPosition = ClampToBoard(exitPosition);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    CreateTile(x, y);
                }
            }

            CreateBoardFloor();
            GeneratePlayableDangerLayout();
            CountSafeTiles();
            ClearTutorialHints();
            DefuserCountChanged?.Invoke();
        }

        public void SetTutorialStep(int stepIndex)
        {
            ClearTutorialHints();
            TutorialTargetCoordinates = new Vector2Int(int.MinValue, int.MinValue);

            if (!showTutorialHints)
            {
                return;
            }

            TileNode target = stepIndex == 1
                ? FindDefuseTutorialTile()
                : FindClearTutorialTile();

            if (target == null)
            {
                return;
            }

            TutorialTargetCoordinates = target.Coordinates;

            if (stepIndex == 1)
            {
                target.SetTutorialHint("DEFUSE", new Color(0.45f, 1f, 1f));
            }
            else
            {
                target.SetTutorialHint("CLEAR", new Color(0.7f, 1f, 0.75f));
            }
        }

        [ContextMenu("Clear Grid")]
        public void ClearGrid()
        {
            tilesByCoordinate.Clear();

            Transform existingGrid = transform.Find(GridParentName);
            if (existingGrid == null)
            {
                gridParent = null;
                return;
            }

            for (int i = existingGrid.childCount - 1; i >= 0; i--)
            {
                GameObject child = existingGrid.GetChild(i).gameObject;

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            gridParent = existingGrid;
        }

        public bool TryGetTile(Vector2Int coordinates, out TileNode tile)
        {
            return tilesByCoordinate.TryGetValue(coordinates, out tile);
        }

        public bool IsInsideBoard(Vector2Int coordinates)
        {
            return coordinates.x >= 0
                && coordinates.y >= 0
                && coordinates.x < width
                && coordinates.y < height;
        }

        public Vector3 GetWorldPosition(Vector2Int coordinates)
        {
            return new Vector3(coordinates.x * tileSpacing, 0f, coordinates.y * tileSpacing);
        }

        public bool ToggleDefuser(Vector2Int coordinates)
        {
            if (!tilesByCoordinate.TryGetValue(coordinates, out TileNode tile) || tile.IsRevealed)
            {
                return false;
            }

            if (!tile.HasDefuser && placedDefuserCount >= defuserLimit)
            {
                Debug.Log("No defusers remaining.", this);
                return false;
            }

            bool hasDefuser = tile.ToggleDefuser();
            placedDefuserCount += hasDefuser ? 1 : -1;
            DefuserCountChanged?.Invoke();
            return true;
        }

        public TileRevealResult RevealTile(Vector2Int coordinates)
        {
            if (!tilesByCoordinate.TryGetValue(coordinates, out TileNode tile))
            {
                return TileRevealResult.Invalid;
            }

            if (!tile.TryReveal())
            {
                return TileRevealResult.NoChange;
            }

            TileRevealed?.Invoke(tile);

            if (tile.HasDanger && !tile.HasDefuser)
            {
                DangerTriggered?.Invoke(tile);
                return TileRevealResult.DangerTriggered;
            }

            if (tile.HasDanger && tile.HasDefuser)
            {
                return TileRevealResult.DangerNeutralized;
            }

            int revealedSafeTiles = RevealConnectedSafeTiles(tile);
            safeTilesRemaining = Mathf.Max(0, safeTilesRemaining - revealedSafeTiles);

            if (safeTilesRemaining == 0)
            {
                SafeTilesCleared?.Invoke();
            }

            return TileRevealResult.SafeRevealed;
        }

        public int RevealStartingArea(Vector2Int coordinates)
        {
            HashSet<Vector2Int> revealSet = BuildFloodRevealSet(coordinates);
            int revealedCount = 0;

            foreach (Vector2Int revealCoordinates in revealSet)
            {
                TileRevealResult result = RevealTile(revealCoordinates);
                if (result == TileRevealResult.SafeRevealed || result == TileRevealResult.DangerNeutralized)
                {
                    revealedCount++;
                }
            }

            return revealedCount;
        }

        public void RevealAllDangers()
        {
            RevealResultTiles();
        }

        public void RevealResultTiles()
        {
            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (tile.HasDanger)
                {
                    tile.RevealDangerForResult();
                }
                else if (tile.HasDefuser)
                {
                    tile.RevealMisflagForResult();
                }
            }
        }

        public void PlayWinEffect()
        {
            int index = 0;
            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                tile.PlayWinEffect(index);
                index++;
            }
        }

        private Transform CreateGridParent()
        {
            Transform existingGrid = transform.Find(GridParentName);
            if (existingGrid != null)
            {
                return existingGrid;
            }

            GameObject parent = new GameObject(GridParentName);
            parent.transform.SetParent(transform, false);
            return parent.transform;
        }

        private void CreateTile(int x, int y)
        {
            GameObject tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObject.name = $"Tile_{x}_{y}";
            tileObject.transform.SetParent(gridParent, false);
            tileObject.transform.localPosition = new Vector3(x * tileSpacing, 0f, y * tileSpacing);
            tileObject.transform.localScale = new Vector3(tileScale, 0.18f, tileScale);

            Collider collider = tileObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            TileNode tile = tileObject.AddComponent<TileNode>();
            tile.Initialize(x, y, this);

            tilesByCoordinate.Add(tile.Coordinates, tile);
        }

        private void CreateBoardFloor()
        {
            GameObject floor = new GameObject(BoardFloorName);
            floor.transform.SetParent(gridParent, false);
            floor.transform.localPosition = new Vector3(
                (width - 1) * tileSpacing * 0.5f,
                -0.08f,
                (height - 1) * tileSpacing * 0.5f);

            BoxCollider floorCollider = floor.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(width * tileSpacing, 0.08f, height * tileSpacing);
        }

        private void GeneratePlayableDangerLayout()
        {
            bool generated = false;
            int bestInitialRevealCount = -1;
            HashSet<Vector2Int> bestDangerCoordinates = new HashSet<Vector2Int>();
            int bestSolvableInitialRevealCount = -1;
            HashSet<Vector2Int> bestSolvableDangerCoordinates = new HashSet<Vector2Int>();

            int attemptLimit = maxGenerationAttempts;

            for (int attempt = 0; attempt < attemptLimit; attempt++)
            {
                ResetTileGameplayState();
                PlaceDangers();
                CalculateAdjacentDangerCounts();

                HashSet<Vector2Int> initialRevealSet = BuildFloodRevealSet(startPosition);
                int zeroCount = CountZeroSafeTiles();
                bool hasEnoughOpening = initialRevealSet.Count >= Mathf.Min(minimumInitialRevealTiles, width * height - dangerCount);
                bool hasControlledZeroCount = zeroCount <= Mathf.CeilToInt((width * height - dangerCount) * maxZeroTileRatio);
                bool isSolvable = CanSolveWithoutGuessing(initialRevealSet);

                if (initialRevealSet.Count > bestInitialRevealCount)
                {
                    bestInitialRevealCount = initialRevealSet.Count;
                    bestDangerCoordinates = CaptureDangerCoordinates();
                }

                if (hasEnoughOpening && isSolvable && initialRevealSet.Count > bestSolvableInitialRevealCount)
                {
                    bestSolvableInitialRevealCount = initialRevealSet.Count;
                    bestSolvableDangerCoordinates = CaptureDangerCoordinates();
                }

                if (hasEnoughOpening && hasControlledZeroCount && isSolvable)
                {
                    generated = true;
                    break;
                }
            }

            if (!generated)
            {
                ResetTileGameplayState();
                if (bestSolvableInitialRevealCount >= 0)
                {
                    RestoreDangerCoordinates(bestSolvableDangerCoordinates);
                    Debug.LogWarning("Using a solver-verified board with a larger opening; zero-tile ratio target was relaxed.", this);
                }
                else
                {
                    RestoreDangerCoordinates(bestDangerCoordinates);
                    Debug.LogWarning("Could not find a fully solver-verified board in time; using the best opening found.", this);
                }

                CalculateAdjacentDangerCounts();
            }

            ClearExitMarkers();
            EnsureSpawnOpening();
        }

        private void EnsureSpawnOpening()
        {
            HashSet<Vector2Int> revealSet = BuildFloodRevealSet(startPosition);
            int desiredRevealCount = Mathf.Min(minimumInitialRevealTiles, width * height - dangerCount);
            if (IsSpawnOpeningAcceptable(revealSet, desiredRevealCount))
            {
                return;
            }

            HashSet<Vector2Int> originalDangers = CaptureDangerCoordinates();
            HashSet<Vector2Int> bestDangers = new HashSet<Vector2Int>(originalDangers);
            int bestRevealCount = revealSet.Count;
            bool bestIsSolvable = CanSolveWithoutGuessing(revealSet);
            int maxRadius = Mathf.Max(width, height);

            for (int radius = 1; radius <= maxRadius; radius++)
            {
                ResetTileGameplayState();
                RestoreDangerCoordinates(originalDangers);

                HashSet<Vector2Int> protectedOpening = GetCoordinatesWithinRadius(startPosition, radius);
                if (width * height - protectedOpening.Count < dangerCount)
                {
                    continue;
                }

                RelocateDangersAwayFrom(protectedOpening);
                CalculateAdjacentDangerCounts();

                revealSet = BuildFloodRevealSet(startPosition);
                bool isSolvable = CanSolveWithoutGuessing(revealSet);
                if ((isSolvable && !bestIsSolvable) || (isSolvable == bestIsSolvable && revealSet.Count > bestRevealCount))
                {
                    bestRevealCount = revealSet.Count;
                    bestIsSolvable = isSolvable;
                    bestDangers = CaptureDangerCoordinates();
                }

                if (IsSpawnOpeningAcceptable(revealSet, desiredRevealCount))
                {
                    return;
                }
            }

            ResetTileGameplayState();
            RestoreDangerCoordinates(bestDangers);
            CalculateAdjacentDangerCounts();
            Debug.LogWarning("Spawn opening was adjusted to avoid a one-tile start.", this);
        }

        private bool IsSpawnOpeningAcceptable(HashSet<Vector2Int> revealSet, int desiredRevealCount)
        {
            return revealSet.Count >= desiredRevealCount
                && tilesByCoordinate.TryGetValue(startPosition, out TileNode startTile)
                && !startTile.HasDanger
                && startTile.AdjacentDangerCount == 0
                && CanSolveWithoutGuessing(revealSet);
        }

        private void RelocateDangersAwayFrom(HashSet<Vector2Int> protectedCoordinates)
        {
            int removedDangers = 0;
            foreach (Vector2Int coordinate in protectedCoordinates)
            {
                if (tilesByCoordinate.TryGetValue(coordinate, out TileNode tile) && tile.HasDanger)
                {
                    tile.SetDanger(false);
                    removedDangers++;
                }
            }

            if (removedDangers == 0)
            {
                return;
            }

            List<Vector2Int> candidates = new List<Vector2Int>();
            foreach (Vector2Int coordinate in tilesByCoordinate.Keys)
            {
                if (protectedCoordinates.Contains(coordinate))
                {
                    continue;
                }

                TileNode tile = tilesByCoordinate[coordinate];
                if (!tile.HasDanger)
                {
                    candidates.Add(coordinate);
                }
            }

            candidates.Sort((left, right) =>
                SquaredDistance(right, startPosition).CompareTo(SquaredDistance(left, startPosition)));

            for (int i = 0; i < removedDangers && i < candidates.Count; i++)
            {
                tilesByCoordinate[candidates[i]].SetDanger(true);
            }
        }

        private HashSet<Vector2Int> GetCoordinatesWithinRadius(Vector2Int center, int radius)
        {
            HashSet<Vector2Int> coordinates = new HashSet<Vector2Int>();
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    Vector2Int coordinate = new Vector2Int(x, y);
                    if (IsInsideBoard(coordinate))
                    {
                        coordinates.Add(coordinate);
                    }
                }
            }

            return coordinates;
        }

        private static int SquaredDistance(Vector2Int a, Vector2Int b)
        {
            int x = a.x - b.x;
            int y = a.y - b.y;
            return x * x + y * y;
        }

        private int RevealConnectedSafeTiles(TileNode startTile)
        {
            int revealedSafeTiles = 1;

            if (startTile.AdjacentDangerCount != 0)
            {
                return revealedSafeTiles;
            }

            HashSet<Vector2Int> revealSet = BuildFloodRevealSet(startTile.Coordinates);
            foreach (Vector2Int revealCoordinates in revealSet)
            {
                if (revealCoordinates == startTile.Coordinates)
                {
                    continue;
                }

                if (!tilesByCoordinate.TryGetValue(revealCoordinates, out TileNode tile)
                    || tile.HasDanger
                    || tile.IsRevealed)
                {
                    continue;
                }

                if (tile.TryReveal())
                {
                    TileRevealed?.Invoke(tile);
                    revealedSafeTiles++;
                }
            }

            return revealedSafeTiles;
        }

        private void ResetTileGameplayState()
        {
            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                tile.ResetGameplayState();
            }
        }

        private void PlaceDangers()
        {
            HashSet<Vector2Int> protectedCoordinates = GetProtectedStartCoordinates();

            int availableTileCount = Mathf.Max(0, width * height - protectedCoordinates.Count);
            int dangersToPlace = Mathf.Clamp(dangerCount, 0, availableTileCount);
            List<Vector2Int> availableCoordinates = new List<Vector2Int>(tilesByCoordinate.Keys);

            foreach (Vector2Int protectedCoordinate in protectedCoordinates)
            {
                availableCoordinates.Remove(protectedCoordinate);
            }

            for (int i = 0; i < dangersToPlace; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, availableCoordinates.Count);
                Vector2Int dangerCoordinates = availableCoordinates[randomIndex];
                availableCoordinates.RemoveAt(randomIndex);

                TileNode tile = tilesByCoordinate[dangerCoordinates];
                tile.SetDanger(true);
            }
        }

        private void ClearExitMarkers()
        {
            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                tile.SetExit(false);
            }
        }

        private void ClearTutorialHints()
        {
            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                tile.ClearTutorialHint();
            }
        }

        private TileNode FindClearTutorialTile()
        {
            foreach (TileNode revealedTile in tilesByCoordinate.Values)
            {
                if (!revealedTile.IsRevealed || revealedTile.HasDanger)
                {
                    continue;
                }

                foreach (Vector2Int neighbor in GetNeighborCoordinates(revealedTile.Coordinates))
                {
                    TileNode tile = tilesByCoordinate[neighbor];
                    if (!tile.IsRevealed && !tile.HasDanger && tile.AdjacentDangerCount > 0)
                    {
                        return tile;
                    }
                }
            }

            foreach (TileNode revealedTile in tilesByCoordinate.Values)
            {
                if (!revealedTile.IsRevealed || revealedTile.HasDanger)
                {
                    continue;
                }

                foreach (Vector2Int neighbor in GetNeighborCoordinates(revealedTile.Coordinates))
                {
                    TileNode tile = tilesByCoordinate[neighbor];
                    if (!tile.IsRevealed && !tile.HasDanger)
                    {
                        return tile;
                    }
                }
            }

            return null;
        }

        private TileNode FindDefuseTutorialTile()
        {
            foreach (TileNode revealedTile in tilesByCoordinate.Values)
            {
                if (!revealedTile.IsRevealed || revealedTile.HasDanger)
                {
                    continue;
                }

                foreach (Vector2Int neighbor in GetNeighborCoordinates(revealedTile.Coordinates))
                {
                    TileNode tile = tilesByCoordinate[neighbor];
                    if (tile.HasDanger && !tile.HasDefuser)
                    {
                        return tile;
                    }
                }
            }

            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (tile.HasDanger && !tile.HasDefuser)
                {
                    return tile;
                }
            }

            return null;
        }

        private void CalculateAdjacentDangerCounts()
        {
            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                int adjacentDangers = CountAdjacentDangers(tile.Coordinates);
                tile.SetAdjacentDangerCount(adjacentDangers);
            }
        }

        private int CountAdjacentDangers(Vector2Int coordinates)
        {
            int adjacentDangers = 0;

            for (int yOffset = -1; yOffset <= 1; yOffset++)
            {
                for (int xOffset = -1; xOffset <= 1; xOffset++)
                {
                    if (xOffset == 0 && yOffset == 0)
                    {
                        continue;
                    }

                    Vector2Int neighborCoordinates = new Vector2Int(
                        coordinates.x + xOffset,
                        coordinates.y + yOffset);

                    if (tilesByCoordinate.TryGetValue(neighborCoordinates, out TileNode neighbor) && neighbor.HasDanger)
                    {
                        adjacentDangers++;
                    }
                }
            }

            return adjacentDangers;
        }

        private List<Vector2Int> GetNeighborCoordinates(Vector2Int coordinates)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();

            for (int yOffset = -1; yOffset <= 1; yOffset++)
            {
                for (int xOffset = -1; xOffset <= 1; xOffset++)
                {
                    if (xOffset == 0 && yOffset == 0)
                    {
                        continue;
                    }

                    Vector2Int neighbor = new Vector2Int(coordinates.x + xOffset, coordinates.y + yOffset);
                    if (IsInsideBoard(neighbor))
                    {
                        neighbors.Add(neighbor);
                    }
                }
            }

            return neighbors;
        }

        private HashSet<Vector2Int> GetProtectedStartCoordinates()
        {
            HashSet<Vector2Int> protectedCoordinates = new HashSet<Vector2Int> { startPosition };

            foreach (Vector2Int neighbor in GetNeighborCoordinates(startPosition))
            {
                protectedCoordinates.Add(neighbor);
            }

            return protectedCoordinates;
        }

        private HashSet<Vector2Int> BuildFloodRevealSet(Vector2Int coordinates)
        {
            HashSet<Vector2Int> revealSet = new HashSet<Vector2Int>();

            if (!tilesByCoordinate.TryGetValue(coordinates, out TileNode startTile) || startTile.HasDanger)
            {
                return revealSet;
            }

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(coordinates);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (!revealSet.Add(current))
                {
                    continue;
                }

                TileNode tile = tilesByCoordinate[current];
                if (tile.AdjacentDangerCount != 0)
                {
                    continue;
                }

                foreach (Vector2Int neighbor in GetNeighborCoordinates(current))
                {
                    if (!tilesByCoordinate[neighbor].HasDanger && !revealSet.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return revealSet;
        }

        private bool CanSolveWithoutGuessing(HashSet<Vector2Int> initialRevealSet)
        {
            HashSet<Vector2Int> revealed = new HashSet<Vector2Int>(initialRevealSet);
            HashSet<Vector2Int> flaggedDangers = new HashSet<Vector2Int>();
            bool changed = true;

            while (changed)
            {
                changed = false;
                List<Vector2Int> revealedSnapshot = new List<Vector2Int>(revealed);

                foreach (Vector2Int coordinate in revealedSnapshot)
                {
                    TileNode tile = tilesByCoordinate[coordinate];
                    if (tile.HasDanger)
                    {
                        continue;
                    }

                    List<Vector2Int> hiddenNeighbors = new List<Vector2Int>();
                    int flaggedNeighborCount = 0;

                    foreach (Vector2Int neighbor in GetNeighborCoordinates(coordinate))
                    {
                        if (flaggedDangers.Contains(neighbor))
                        {
                            flaggedNeighborCount++;
                        }
                        else if (!revealed.Contains(neighbor))
                        {
                            hiddenNeighbors.Add(neighbor);
                        }
                    }

                    int remainingDangers = tile.AdjacentDangerCount - flaggedNeighborCount;

                    if (remainingDangers == hiddenNeighbors.Count && hiddenNeighbors.Count > 0)
                    {
                        foreach (Vector2Int hiddenNeighbor in hiddenNeighbors)
                        {
                            if (flaggedDangers.Add(hiddenNeighbor))
                            {
                                changed = true;
                            }
                        }
                    }
                    else if (remainingDangers == 0 && hiddenNeighbors.Count > 0)
                    {
                        foreach (Vector2Int hiddenNeighbor in hiddenNeighbors)
                        {
                            if (!tilesByCoordinate[hiddenNeighbor].HasDanger && revealed.Add(hiddenNeighbor))
                            {
                                if (tilesByCoordinate[hiddenNeighbor].AdjacentDangerCount == 0)
                                {
                                    foreach (Vector2Int floodCoordinate in BuildFloodRevealSet(hiddenNeighbor))
                                    {
                                        revealed.Add(floodCoordinate);
                                    }
                                }

                                changed = true;
                            }
                        }
                    }
                }
            }

            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (!tile.HasDanger && !revealed.Contains(tile.Coordinates))
                {
                    return false;
                }
            }

            return true;
        }

        private int CountZeroSafeTiles()
        {
            int zeroCount = 0;

            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (!tile.HasDanger && tile.AdjacentDangerCount == 0)
                {
                    zeroCount++;
                }
            }

            return zeroCount;
        }

        private HashSet<Vector2Int> CaptureDangerCoordinates()
        {
            HashSet<Vector2Int> dangerCoordinates = new HashSet<Vector2Int>();

            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (tile.HasDanger)
                {
                    dangerCoordinates.Add(tile.Coordinates);
                }
            }

            return dangerCoordinates;
        }

        private void RestoreDangerCoordinates(HashSet<Vector2Int> dangerCoordinates)
        {
            foreach (Vector2Int dangerCoordinate in dangerCoordinates)
            {
                if (tilesByCoordinate.TryGetValue(dangerCoordinate, out TileNode tile))
                {
                    tile.SetDanger(true);
                }
            }
        }

        private void CountSafeTiles()
        {
            safeTilesRemaining = 0;

            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (!tile.HasDanger)
                {
                    safeTilesRemaining++;
                }
            }
        }

        private Vector2Int ClampToBoard(Vector2Int value)
        {
            return new Vector2Int(
                Mathf.Clamp(value.x, 0, Mathf.Max(0, width - 1)),
                Mathf.Clamp(value.y, 0, Mathf.Max(0, height - 1)));
        }

        private bool IsOnBoardEdge(Vector2Int coordinates)
        {
            return coordinates.x <= 0
                || coordinates.y <= 0
                || coordinates.x >= width - 1
                || coordinates.y >= height - 1;
        }
    }

    public enum TileRevealResult
    {
        Invalid,
        NoChange,
        SafeRevealed,
        DangerNeutralized,
        DangerTriggered
    }
}
