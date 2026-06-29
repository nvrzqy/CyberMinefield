using System;
using System.Collections;
using System.Collections.Generic;
using CyberMinefield.Levels;
using UnityEngine;

namespace CyberMinefield.Grid
{
    public sealed class GridManager : MonoBehaviour
    {
        private const string GridParentName = "Grid";
        private const string BoardFloorName = "BoardFloor";
        private const int LargeBoardArea = 225;
        private const int LargeBoardDangerMarkerBudget = 48;
        private const int FastLargeBoardGenerationAttempts = 32;
        private const long AsyncGenerationFrameBudgetMs = 12L;

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
        [SerializeField] private int maxGenerationAttempts = 1200;
        [SerializeField] private int minimumInitialRevealTiles = 9;
        [SerializeField] private float maxZeroTileRatio = 0.08f;
        [SerializeField] private float minimumNumberedSafeRatio = 0.74f;
        [SerializeField] private bool showTutorialHints;
        [SerializeField] private bool randomizeStartPosition;

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
        public float TileSurfaceHeight => tileScale * 0.09f;
        public Vector2Int StartPosition => startPosition;
        public Vector2Int ExitPosition => exitPosition;
        public Vector2Int TutorialTargetCoordinates { get; private set; } = new Vector2Int(int.MinValue, int.MinValue);
        public IReadOnlyDictionary<Vector2Int, TileNode> TilesByCoordinate => tilesByCoordinate;

        public event Action<TileNode> TileRevealed;
        public event Action<TileNode> TileEntered;
        public event Action<TileNode> DangerTriggered;
        public event Action<TileNode> DefuserPlaced;
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
            showTutorialHints = level.LevelName.StartsWith("Tutorial");
            randomizeStartPosition = !showTutorialHints;
            startPosition = ClampToBoard(level.StartPosition);
            if (width >= 5 && height >= 5 && IsOnBoardEdge(startPosition))
            {
                startPosition = new Vector2Int(width / 2, height / 2);
            }

            exitPosition = ClampToBoard(level.ExitPosition);
            minimumInitialRevealTiles = Mathf.Max(
                minimumInitialRevealTiles,
                Mathf.Min(9, Mathf.Max(1, width * height - dangerCount)));
            maxZeroTileRatio = Mathf.Min(maxZeroTileRatio, 0.08f);
            minimumNumberedSafeRatio = Mathf.Max(minimumNumberedSafeRatio, 0.74f);
            useRandomSeed = false;
        }

        [ContextMenu("Generate Grid")]
        public void GenerateGrid()
        {
            RunToCompletion(GenerateGridRoutine(false));
        }

        public IEnumerator GenerateGridAsync()
        {
            yield return GenerateGridRoutine(true);
        }

        private IEnumerator GenerateGridRoutine(bool allowYield)
        {
            ClearGrid();
            gridParent = CreateGridParent();
            tilesByCoordinate.Clear();
            placedDefuserCount = 0;
            safeTilesRemaining = 0;
            System.Diagnostics.Stopwatch generationBudget = System.Diagnostics.Stopwatch.StartNew();

            if (useRandomSeed)
            {
                UnityEngine.Random.InitState(randomSeed);
            }

            startPosition = ClampToBoard(startPosition);
            if (randomizeStartPosition)
            {
                startPosition = ChooseRandomStartPosition();
            }

            exitPosition = ClampToBoard(exitPosition);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    CreateTile(x, y);
                }

                if (ShouldYieldForAsyncGeneration(allowYield, generationBudget))
                {
                    yield return null;
                }
            }

            CreateBoardFloor();

            if (allowYield)
            {
                yield return GeneratePlayableDangerLayoutRoutine(true);
            }
            else
            {
                RunToCompletion(GeneratePlayableDangerLayoutRoutine(false));
            }

            CountSafeTiles();
            ClearTutorialHints();
            DefuserCountChanged?.Invoke();
        }

        private static bool ShouldYieldForAsyncGeneration(bool allowYield, System.Diagnostics.Stopwatch budget)
        {
            if (!allowYield || budget.ElapsedMilliseconds < AsyncGenerationFrameBudgetMs)
            {
                return false;
            }

            budget.Restart();
            return true;
        }

        private static void RunToCompletion(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nestedRoutine)
                {
                    RunToCompletion(nestedRoutine);
                }
            }
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
            Vector3 localPosition = new Vector3(coordinates.x * tileSpacing, 0f, coordinates.y * tileSpacing);
            return transform.TransformPoint(localPosition);
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

            bool hadDefuser = tile.HasDefuser;
            bool hasDefuser = tile.ToggleDefuser();
            placedDefuserCount += hasDefuser ? 1 : -1;
            if (hasDefuser)
            {
                DefuserPlaced?.Invoke(tile);
            }

            DefuserCountChanged?.Invoke();

            if (hadDefuser && !hasDefuser && tile.IsPlayerOccupying)
            {
                RevealTile(coordinates);
            }

            return true;
        }

        public void NotifyTileEntered(Vector2Int coordinates)
        {
            if (tilesByCoordinate.TryGetValue(coordinates, out TileNode tile))
            {
                TileEntered?.Invoke(tile);
            }
        }

        private void RemoveDefuserForAutoReveal(TileNode tile)
        {
            if (tile == null || !tile.HasDefuser)
            {
                return;
            }

            tile.ToggleDefuser();
            placedDefuserCount = Mathf.Max(0, placedDefuserCount - 1);
            DefuserCountChanged?.Invoke();
        }

        public TileRevealResult RevealTile(Vector2Int coordinates)
        {
            if (!tilesByCoordinate.TryGetValue(coordinates, out TileNode tile))
            {
                return TileRevealResult.Invalid;
            }

            if (tile.HasDefuser)
            {
                return tile.HasDanger
                    ? TileRevealResult.DangerNeutralized
                    : TileRevealResult.NoChange;
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
            HashSet<Vector2Int> revealSet = BuildInitialRevealSet(coordinates);
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

        public void RevealResultTiles(int maxDangerMarkers = int.MaxValue)
        {
            int shownDangerMarkers = 0;

            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (tile.HasDanger)
                {
                    bool showMarker = shownDangerMarkers < maxDangerMarkers;
                    tile.RevealDangerForResult(showMarker);
                    if (showMarker)
                    {
                        shownDangerMarkers++;
                    }
                }
                else if (tile.HasDefuser)
                {
                    tile.RevealMisflagForResult();
                }
            }
        }

        public IEnumerator PlayLoseVirusSpread()
        {
            bool largeBoard = width * height > LargeBoardArea;
            int dangerMarkerBudget = largeBoard ? LargeBoardDangerMarkerBudget : int.MaxValue;
            RevealResultTiles(dangerMarkerBudget);

            yield return new WaitForSeconds(3f);

            List<Vector2Int> dangerCoordinates = new List<Vector2Int>();
            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (tile.HasDanger)
                {
                    dangerCoordinates.Add(tile.Coordinates);
                }
            }

            List<VirusSpreadEntry> spreadEntries = new List<VirusSpreadEntry>();
            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (tile.HasDanger)
                {
                    continue;
                }

                spreadEntries.Add(new VirusSpreadEntry(tile, GetNearestDangerDistance(tile.Coordinates, dangerCoordinates)));
            }

            spreadEntries.Sort((left, right) =>
            {
                int distanceComparison = left.Distance.CompareTo(right.Distance);
                if (distanceComparison != 0)
                {
                    return distanceComparison;
                }

                int rowComparison = left.Tile.Y.CompareTo(right.Tile.Y);
                return rowComparison != 0 ? rowComparison : left.Tile.X.CompareTo(right.Tile.X);
            });

            int currentDistance = -1;
            for (int i = 0; i < spreadEntries.Count; i++)
            {
                VirusSpreadEntry entry = spreadEntries[i];
                if (currentDistance >= 0 && entry.Distance != currentDistance)
                {
                    yield return new WaitForSeconds(0.055f);
                }

                currentDistance = entry.Distance;
                entry.Tile.InfectWithVirusSpread(false);
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

        private int GetNearestDangerDistance(Vector2Int coordinates, List<Vector2Int> dangerCoordinates)
        {
            if (dangerCoordinates.Count == 0)
            {
                Vector2Int center = new Vector2Int(width / 2, height / 2);
                return Mathf.Abs(coordinates.x - center.x) + Mathf.Abs(coordinates.y - center.y);
            }

            int bestDistance = int.MaxValue;
            for (int i = 0; i < dangerCoordinates.Count; i++)
            {
                Vector2Int danger = dangerCoordinates[i];
                int distance = Mathf.Abs(coordinates.x - danger.x) + Mathf.Abs(coordinates.y - danger.y);
                bestDistance = Mathf.Min(bestDistance, distance);
            }

            return bestDistance;
        }

        private readonly struct VirusSpreadEntry
        {
            public VirusSpreadEntry(TileNode tile, int distance)
            {
                Tile = tile;
                Distance = distance;
            }

            public TileNode Tile { get; }
            public int Distance { get; }
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
            RunToCompletion(GeneratePlayableDangerLayoutRoutine(false));
        }

        private IEnumerator GeneratePlayableDangerLayoutRoutine(bool allowYield)
        {
            if (width * height > LargeBoardArea)
            {
                yield return GenerateFastLargeBoardDangerLayoutRoutine(allowYield);
                yield break;
            }

            int requestedDangerCount = dangerCount;
            int selectedDangerCount = dangerCount;
            bool selectedBoard = false;
            bool relaxedDensityTargets = false;
            bool reducedDangerCount = false;
            HashSet<Vector2Int> selectedDangerCoordinates = new HashSet<Vector2Int>();
            System.Diagnostics.Stopwatch generationBudget = System.Diagnostics.Stopwatch.StartNew();

            for (int candidateDangerCount = requestedDangerCount; candidateDangerCount >= 0 && !selectedBoard; candidateDangerCount--)
            {
                dangerCount = candidateDangerCount;
                int bestSolvableBoardScore = int.MinValue;
                HashSet<Vector2Int> bestSolvableDangerCoordinates = new HashSet<Vector2Int>();
                int attemptLimit = Mathf.Max(maxGenerationAttempts, width * height * 8);

                for (int attempt = 0; attempt < attemptLimit; attempt++)
                {
                    ResetTileGameplayState();
                    PlaceDangers();
                    CalculateAdjacentDangerCounts();

                    HashSet<Vector2Int> initialRevealSet = BuildInitialRevealSet(startPosition);
                    int zeroCount = CountZeroSafeTiles();
                    int numberedCount = CountNumberedSafeTiles();
                    int highNumberCount = CountHighNumberSafeTiles();
                    int safeCount = Mathf.Max(1, width * height - dangerCount);
                    int boardScore = ScoreBoard(initialRevealSet.Count, zeroCount, numberedCount, highNumberCount);
                    bool hasEnoughOpening = initialRevealSet.Count >= Mathf.Min(minimumInitialRevealTiles, width * height - dangerCount);
                    bool hasControlledZeroCount = zeroCount <= Mathf.CeilToInt((width * height - dangerCount) * maxZeroTileRatio);
                    bool hasEnoughNumbers = numberedCount >= Mathf.FloorToInt(safeCount * minimumNumberedSafeRatio);
                    bool isSolvable = CanSolveWithoutGuessing(initialRevealSet);

                    if (hasEnoughOpening && isSolvable && boardScore > bestSolvableBoardScore)
                    {
                        bestSolvableBoardScore = boardScore;
                        bestSolvableDangerCoordinates = CaptureDangerCoordinates();
                    }

                    if (hasEnoughOpening && hasControlledZeroCount && hasEnoughNumbers && isSolvable)
                    {
                        selectedBoard = true;
                        selectedDangerCount = candidateDangerCount;
                        selectedDangerCoordinates = CaptureDangerCoordinates();
                        reducedDangerCount = candidateDangerCount != requestedDangerCount;
                        break;
                    }

                    if (ShouldYieldForAsyncGeneration(allowYield, generationBudget))
                    {
                        yield return null;
                    }
                }

                if (!selectedBoard && bestSolvableBoardScore > int.MinValue)
                {
                    selectedBoard = true;
                    relaxedDensityTargets = true;
                    selectedDangerCount = candidateDangerCount;
                    selectedDangerCoordinates = bestSolvableDangerCoordinates;
                    reducedDangerCount = candidateDangerCount != requestedDangerCount;
                }

                if (ShouldYieldForAsyncGeneration(allowYield, generationBudget))
                {
                    yield return null;
                }
            }

            ResetTileGameplayState();
            dangerCount = selectedBoard ? selectedDangerCount : 0;
            RestoreDangerCoordinates(selectedDangerCoordinates);
            CalculateAdjacentDangerCounts();

            if (!selectedBoard)
            {
                defuserLimit = 0;
                Debug.LogError("Could not find a solver-verified board; generated a safe training board instead.", this);
            }
            else if (reducedDangerCount)
            {
                defuserLimit = Mathf.Min(defuserLimit, dangerCount);
                Debug.LogWarning($"Reduced danger count from {requestedDangerCount} to {dangerCount} to keep this board solver-verified.", this);
            }
            else if (relaxedDensityTargets)
            {
                Debug.LogWarning("Using a solver-verified board with relaxed density targets.", this);
            }

            ClearExitMarkers();
            if (allowYield)
            {
                yield return EnsureSpawnOpeningRoutine(true);
            }
            else
            {
                RunToCompletion(EnsureSpawnOpeningRoutine(false));
            }
        }

        private IEnumerator GenerateFastLargeBoardDangerLayoutRoutine(bool allowYield)
        {
            int requestedDangerCount = dangerCount;
            int bestScore = int.MinValue;
            HashSet<Vector2Int> bestDangerCoordinates = new HashSet<Vector2Int>();
            System.Diagnostics.Stopwatch generationBudget = System.Diagnostics.Stopwatch.StartNew();

            for (int attempt = 0; attempt < FastLargeBoardGenerationAttempts; attempt++)
            {
                ResetTileGameplayState();
                PlaceDangers(GetProtectedStartCoordinates(1));
                CalculateAdjacentDangerCounts();

                HashSet<Vector2Int> initialRevealSet = BuildInitialRevealSet(startPosition);
                int zeroCount = CountZeroSafeTiles();
                int numberedCount = CountNumberedSafeTiles();
                int highNumberCount = CountHighNumberSafeTiles();
                int safeCount = Mathf.Max(1, width * height - dangerCount);
                int desiredOpening = Mathf.Min(minimumInitialRevealTiles, safeCount);
                int score = ScoreBoard(initialRevealSet.Count, zeroCount, numberedCount, highNumberCount);

                if (initialRevealSet.Count < desiredOpening)
                {
                    score -= (desiredOpening - initialRevealSet.Count) * 500;
                }

                if (tilesByCoordinate.TryGetValue(startPosition, out TileNode startTile))
                {
                    score += startTile.AdjacentDangerCount * 40;
                }

                score += CountRevealedFrontierClues(initialRevealSet) * 18;
                score -= Mathf.Abs(requestedDangerCount - dangerCount) * 300;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDangerCoordinates = CaptureDangerCoordinates();
                }

                bool hasEnoughOpening = initialRevealSet.Count >= desiredOpening;
                bool hasEnoughNumbers = numberedCount >= Mathf.FloorToInt(safeCount * 0.68f);
                bool hasControlledZeroCount = zeroCount <= Mathf.CeilToInt(safeCount * 0.14f);
                if (attempt >= 8 && hasEnoughOpening && hasEnoughNumbers && hasControlledZeroCount)
                {
                    break;
                }

                if (ShouldYieldForAsyncGeneration(allowYield, generationBudget))
                {
                    yield return null;
                }
            }

            ResetTileGameplayState();
            dangerCount = requestedDangerCount;
            RestoreDangerCoordinates(bestDangerCoordinates);
            CalculateAdjacentDangerCounts();
            EnsureFastLargeBoardSpawnOpening();
            ClearExitMarkers();
        }

        private void EnsureSpawnOpening()
        {
            RunToCompletion(EnsureSpawnOpeningRoutine(false));
        }

        private void EnsureFastLargeBoardSpawnOpening()
        {
            int desiredRevealCount = Mathf.Min(minimumInitialRevealTiles, width * height - dangerCount);
            HashSet<Vector2Int> revealSet = BuildInitialRevealSet(startPosition);
            if (revealSet.Count >= desiredRevealCount)
            {
                return;
            }

            HashSet<Vector2Int> protectedOpening = GetProtectedStartCoordinates(1);
            RelocateDangersAwayFrom(protectedOpening);
            CalculateAdjacentDangerCounts();

            revealSet = BuildInitialRevealSet(startPosition);
            if (revealSet.Count >= desiredRevealCount)
            {
                return;
            }

            protectedOpening = GetProtectedStartCoordinates(2);
            RelocateDangersAwayFrom(protectedOpening);
            CalculateAdjacentDangerCounts();
        }

        private IEnumerator EnsureSpawnOpeningRoutine(bool allowYield)
        {
            Vector2Int bestStart = startPosition;
            HashSet<Vector2Int> revealSet = BuildInitialRevealSet(startPosition);
            int desiredRevealCount = Mathf.Min(minimumInitialRevealTiles, width * height - dangerCount);
            int bestScore = ScoreStartPosition(startPosition, revealSet);
            System.Diagnostics.Stopwatch generationBudget = System.Diagnostics.Stopwatch.StartNew();

            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (tile.HasDanger || IsOnBoardEdge(tile.Coordinates))
                {
                    continue;
                }

                HashSet<Vector2Int> candidateRevealSet = BuildInitialRevealSet(tile.Coordinates);
                if (!IsSpawnOpeningAcceptable(tile.Coordinates, candidateRevealSet, desiredRevealCount))
                {
                    continue;
                }

                int candidateScore = ScoreStartPosition(tile.Coordinates, candidateRevealSet);
                if (candidateScore > bestScore)
                {
                    bestStart = tile.Coordinates;
                    revealSet = candidateRevealSet;
                    bestScore = candidateScore;
                }

                if (ShouldYieldForAsyncGeneration(allowYield, generationBudget))
                {
                    yield return null;
                }
            }

            if (bestStart != startPosition)
            {
                startPosition = bestStart;
                yield break;
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

                HashSet<Vector2Int> protectedOpening = new HashSet<Vector2Int> { startPosition };
                foreach (Vector2Int candidate in GetCoordinatesWithinRadius(startPosition, radius))
                {
                    if (tilesByCoordinate.TryGetValue(candidate, out TileNode tile)
                        && !tile.HasDanger
                        && protectedOpening.Count < desiredRevealCount)
                    {
                        protectedOpening.Add(candidate);
                    }
                }

                if (width * height - protectedOpening.Count < dangerCount)
                {
                    continue;
                }

                RelocateDangersAwayFrom(protectedOpening);
                CalculateAdjacentDangerCounts();

                revealSet = BuildInitialRevealSet(startPosition);
                bool isSolvable = CanSolveWithoutGuessing(revealSet);
                if ((isSolvable && !bestIsSolvable) || (isSolvable == bestIsSolvable && revealSet.Count > bestRevealCount))
                {
                    bestRevealCount = revealSet.Count;
                    bestIsSolvable = isSolvable;
                    bestDangers = CaptureDangerCoordinates();
                }

                if (IsSpawnOpeningAcceptable(startPosition, revealSet, desiredRevealCount))
                {
                    yield break;
                }

                if (ShouldYieldForAsyncGeneration(allowYield, generationBudget))
                {
                    yield return null;
                }
            }

            ResetTileGameplayState();
            RestoreDangerCoordinates(bestDangers);
            CalculateAdjacentDangerCounts();
            Debug.LogWarning("Spawn opening was adjusted to avoid a one-tile start.", this);
        }

        private bool IsSpawnOpeningAcceptable(Vector2Int candidateStart, HashSet<Vector2Int> revealSet, int desiredRevealCount)
        {
            return revealSet.Count >= desiredRevealCount
                && tilesByCoordinate.TryGetValue(candidateStart, out TileNode startTile)
                && !startTile.HasDanger
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

        private Vector2Int ChooseRandomStartPosition()
        {
            int minX = width > 4 ? 1 : 0;
            int minY = height > 4 ? 1 : 0;
            int maxX = width > 4 ? width - 2 : width - 1;
            int maxY = height > 4 ? height - 2 : height - 1;

            return new Vector2Int(
                UnityEngine.Random.Range(minX, maxX + 1),
                UnityEngine.Random.Range(minY, maxY + 1));
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

                if (tile.HasDefuser)
                {
                    RemoveDefuserForAutoReveal(tile);
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
            PlaceDangers(GetProtectedStartCoordinates());
        }

        private void PlaceDangers(HashSet<Vector2Int> protectedCoordinates)
        {
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

        public void ClearTutorialHints()
        {
            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                tile.ClearTutorialHint();
            }
        }

        public bool SetTutorialHintAt(Vector2Int coordinates, string text, Color color)
        {
            ClearTutorialHints();
            TutorialTargetCoordinates = new Vector2Int(int.MinValue, int.MinValue);

            if (!tilesByCoordinate.TryGetValue(coordinates, out TileNode tile))
            {
                return false;
            }

            TutorialTargetCoordinates = coordinates;
            tile.SetTutorialHint(text, color);
            return true;
        }

        public bool SetTutorialClearHintNear(Vector2Int anchorCoordinates, string text, Color color)
        {
            ClearTutorialHints();
            TutorialTargetCoordinates = new Vector2Int(int.MinValue, int.MinValue);

            TileNode target = FindClearTutorialTileNear(anchorCoordinates);
            if (target == null)
            {
                target = FindClearTutorialTile();
            }

            if (target == null)
            {
                return false;
            }

            TutorialTargetCoordinates = target.Coordinates;
            target.SetTutorialHint(text, color);
            return true;
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

        private TileNode FindClearTutorialTileNear(Vector2Int anchorCoordinates)
        {
            if (!IsInsideBoard(anchorCoordinates))
            {
                return null;
            }

            List<TileNode> clueTiles = new List<TileNode>();
            foreach (Vector2Int clueCoordinates in GetNeighborCoordinates(anchorCoordinates))
            {
                TileNode clueTile = tilesByCoordinate[clueCoordinates];
                if (clueTile.IsRevealed && !clueTile.HasDanger && clueTile.AdjacentDangerCount > 0)
                {
                    clueTiles.Add(clueTile);
                }
            }

            clueTiles.Sort((left, right) =>
            {
                int leftSatisfied = IsClueSatisfied(left) ? 0 : 1;
                int rightSatisfied = IsClueSatisfied(right) ? 0 : 1;
                if (leftSatisfied != rightSatisfied)
                {
                    return leftSatisfied.CompareTo(rightSatisfied);
                }

                int countComparison = left.AdjacentDangerCount.CompareTo(right.AdjacentDangerCount);
                if (countComparison != 0)
                {
                    return countComparison;
                }

                return SquaredDistance(left.Coordinates, anchorCoordinates)
                    .CompareTo(SquaredDistance(right.Coordinates, anchorCoordinates));
            });

            foreach (TileNode clueTile in clueTiles)
            {
                TileNode candidate = FindUnrevealedSafeNeighbor(clueTile.Coordinates, anchorCoordinates);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return FindUnrevealedSafeNeighbor(anchorCoordinates, anchorCoordinates);
        }

        private TileNode FindUnrevealedSafeNeighbor(Vector2Int centerCoordinates, Vector2Int sortAnchor)
        {
            List<TileNode> candidates = new List<TileNode>();
            foreach (Vector2Int neighborCoordinates in GetNeighborCoordinates(centerCoordinates))
            {
                TileNode tile = tilesByCoordinate[neighborCoordinates];
                if (!tile.IsRevealed && !tile.HasDanger)
                {
                    candidates.Add(tile);
                }
            }

            candidates.Sort((left, right) =>
            {
                int leftNumbered = left.AdjacentDangerCount > 0 ? 0 : 1;
                int rightNumbered = right.AdjacentDangerCount > 0 ? 0 : 1;
                if (leftNumbered != rightNumbered)
                {
                    return leftNumbered.CompareTo(rightNumbered);
                }

                return SquaredDistance(left.Coordinates, sortAnchor)
                    .CompareTo(SquaredDistance(right.Coordinates, sortAnchor));
            });

            return candidates.Count > 0 ? candidates[0] : null;
        }

        private bool IsClueSatisfied(TileNode clueTile)
        {
            int adjacentDefusers = 0;
            foreach (Vector2Int neighborCoordinates in GetNeighborCoordinates(clueTile.Coordinates))
            {
                if (tilesByCoordinate[neighborCoordinates].HasDefuser)
                {
                    adjacentDefusers++;
                }
            }

            return adjacentDefusers >= clueTile.AdjacentDangerCount;
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

        private HashSet<Vector2Int> GetProtectedStartCoordinates(int radius = 0)
        {
            HashSet<Vector2Int> protectedCoordinates = new HashSet<Vector2Int> { startPosition };
            if (radius <= 0)
            {
                return protectedCoordinates;
            }

            foreach (Vector2Int coordinate in GetCoordinatesWithinRadius(startPosition, radius))
            {
                protectedCoordinates.Add(coordinate);
            }

            return protectedCoordinates;
        }

        private HashSet<Vector2Int> BuildInitialRevealSet(Vector2Int coordinates)
        {
            HashSet<Vector2Int> revealSet = new HashSet<Vector2Int>();
            if (!tilesByCoordinate.TryGetValue(coordinates, out TileNode startTile) || startTile.HasDanger)
            {
                return revealSet;
            }

            revealSet.Add(coordinates);

            List<TileNode> candidates = new List<TileNode>();
            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (tile.HasDanger || tile.Coordinates == coordinates)
                {
                    continue;
                }

                int distance = SquaredDistance(tile.Coordinates, coordinates);
                if (distance <= 9)
                {
                    candidates.Add(tile);
                }
            }

            candidates.Sort((left, right) =>
            {
                int leftZeroPenalty = left.AdjacentDangerCount == 0 ? 100 : 0;
                int rightZeroPenalty = right.AdjacentDangerCount == 0 ? 100 : 0;
                int leftScore = leftZeroPenalty + SquaredDistance(left.Coordinates, coordinates) * 4 - left.AdjacentDangerCount;
                int rightScore = rightZeroPenalty + SquaredDistance(right.Coordinates, coordinates) * 4 - right.AdjacentDangerCount;
                return leftScore.CompareTo(rightScore);
            });

            int desiredRevealCount = Mathf.Min(minimumInitialRevealTiles, width * height - dangerCount);
            foreach (TileNode tile in candidates)
            {
                if (revealSet.Count >= desiredRevealCount)
                {
                    break;
                }

                revealSet.Add(tile.Coordinates);
            }

            if (startTile.AdjacentDangerCount == 0)
            {
                foreach (Vector2Int floodCoordinate in BuildFloodRevealSet(coordinates))
                {
                    revealSet.Add(floodCoordinate);
                }
            }

            return revealSet;
        }

        private int ScoreStartPosition(Vector2Int coordinates, HashSet<Vector2Int> revealSet)
        {
            if (revealSet.Count == 0 || !tilesByCoordinate.TryGetValue(coordinates, out TileNode startTile))
            {
                return int.MinValue;
            }

            int score = revealSet.Count * 10;
            score += startTile.AdjacentDangerCount * 16;

            foreach (Vector2Int revealCoordinate in revealSet)
            {
                TileNode tile = tilesByCoordinate[revealCoordinate];
                score += tile.AdjacentDangerCount * 8;
                if (tile.AdjacentDangerCount == 0)
                {
                    score -= 35;
                }
            }

            if (startTile.AdjacentDangerCount == 0)
            {
                score -= 80;
            }

            return score;
        }

        private int CountRevealedFrontierClues(HashSet<Vector2Int> revealSet)
        {
            int clueCount = 0;
            foreach (Vector2Int revealCoordinate in revealSet)
            {
                TileNode tile = tilesByCoordinate[revealCoordinate];
                if (tile.AdjacentDangerCount <= 0)
                {
                    continue;
                }

                foreach (Vector2Int neighbor in GetNeighborCoordinates(revealCoordinate))
                {
                    if (!revealSet.Contains(neighbor))
                    {
                        clueCount++;
                        break;
                    }
                }
            }

            return clueCount;
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

        private int CountNumberedSafeTiles()
        {
            int numberedCount = 0;

            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (!tile.HasDanger && tile.AdjacentDangerCount > 0)
                {
                    numberedCount++;
                }
            }

            return numberedCount;
        }

        private int CountHighNumberSafeTiles()
        {
            int highNumberCount = 0;

            foreach (TileNode tile in tilesByCoordinate.Values)
            {
                if (!tile.HasDanger && tile.AdjacentDangerCount >= 4)
                {
                    highNumberCount++;
                }
            }

            return highNumberCount;
        }

        private static int ScoreBoard(int initialRevealCount, int zeroCount, int numberedCount, int highNumberCount)
        {
            return initialRevealCount * 40
                + numberedCount * 24
                + highNumberCount * 36
                - zeroCount * 90;
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
