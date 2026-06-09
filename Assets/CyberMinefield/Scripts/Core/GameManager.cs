using CyberMinefield.Audio;
using CyberMinefield.Grid;
using CyberMinefield.Levels;
using CyberMinefield.Player;
using CyberMinefield.UI;
using UnityEngine;

namespace CyberMinefield.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        private const int FirstCampaignLevelIndex = 1;
        private const string ClassicWinsKey = "CyberMinefield.ClassicWins";
        private const string ClassicAttemptsKey = "CyberMinefield.ClassicAttempts";
        private const string ClassicBestTimeKey = "CyberMinefield.ClassicBestTime";

        [SerializeField] private GridManager gridManager;
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private InputManager inputManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private bool showHomeOnStart = true;

        private float elapsedTime;
        private GameState state = GameState.Home;
        private GameMode currentMode = GameMode.Home;
        private LevelDefinition activeLevel;
        private int activeCampaignLevelIndex = FirstCampaignLevelIndex;
        private int activeTimeLevelIndex;
        private int tutorialStep = -1;
        private string tutorialMessage = string.Empty;
        private bool roundRecorded;
        private int classicWins;
        private int classicAttempts;
        private float classicBestTime;
        private int lastPauseToggleFrame = -1;
        private int lastCameraLockToggleFrame = -1;
        private int lastContinueFrame = -1;

        public GameState State => state;
        public LevelDefinition ActiveLevel => activeLevel;
        public float ElapsedTime => elapsedTime;

        private void Awake()
        {
            ResolveReferences();
            LoadClassicStats();
        }

        private void Start()
        {
            if (showHomeOnStart)
            {
                ShowHomePage();
            }
            else
            {
                StartCampaign();
            }
        }

        private void Update()
        {
            HandleSystemInput();

            if (state == GameState.Playing)
            {
                elapsedTime += Time.deltaTime;

                if (activeLevel.TimeLimit > 0f && elapsedTime >= activeLevel.TimeLimit)
                {
                    Lose("Time expired. Press Replay to try again.");
                }
            }

            RefreshHud();
        }

        public void ShowHomePage()
        {
            ResolveReferences();
            UnsubscribeGridEvents();
            Time.timeScale = 1f;
            state = GameState.Home;
            currentMode = GameMode.Home;
            activeLevel = null;
            tutorialStep = -1;
            tutorialMessage = string.Empty;

            if (playerController != null)
            {
                playerController.SetInputEnabled(false);
                playerController.gameObject.SetActive(false);
            }

            if (gridManager != null)
            {
                gridManager.ClearGrid();
            }

            uiManager.Bind(this);
            uiManager.ShowHome(GetClassicStatsText());
        }

        public void StartTutorial()
        {
            BeginLevel(levelManager.SetCurrentLevel(0), GameMode.Tutorial, 0);
        }

        public void StartCampaign()
        {
            activeCampaignLevelIndex = FirstCampaignLevelIndex;
            BeginCampaignLevel(activeCampaignLevelIndex);
        }

        public void StartClassic()
        {
            BeginLevel(CreateClassicLevel(), GameMode.Classic, -1);
        }

        public void StartTimeAttack()
        {
            activeTimeLevelIndex = 0;
            BeginTimeAttackLevel(activeTimeLevelIndex);
        }

        public void StartLevel(int levelIndex)
        {
            activeCampaignLevelIndex = Mathf.Clamp(levelIndex, FirstCampaignLevelIndex, levelManager.LevelCount - 1);
            BeginCampaignLevel(activeCampaignLevelIndex);
        }

        public void RestartLevel()
        {
            if (currentMode == GameMode.Home)
            {
                ShowHomePage();
                return;
            }

            if (currentMode == GameMode.Classic)
            {
                StartClassic();
                return;
            }

            if (currentMode == GameMode.TimeAttack)
            {
                BeginTimeAttackLevel(activeTimeLevelIndex);
                return;
            }

            if (currentMode == GameMode.Tutorial)
            {
                StartTutorial();
                return;
            }

            BeginCampaignLevel(activeCampaignLevelIndex);
        }

        public void StartNextLevel()
        {
            ContinueAfterWin();
        }

        public void ContinueAfterWin()
        {
            ResolveReferences();

            if (lastContinueFrame == Time.frameCount)
            {
                return;
            }

            lastContinueFrame = Time.frameCount;

            if (currentMode == GameMode.Tutorial)
            {
                StartCampaign();
                return;
            }

            if (currentMode == GameMode.Campaign)
            {
                int nextIndex = activeCampaignLevelIndex + 1;
                if (nextIndex >= levelManager.LevelCount)
                {
                    ShowHomePage();
                    return;
                }

                BeginCampaignLevel(nextIndex);
                return;
            }

            if (currentMode == GameMode.TimeAttack)
            {
                activeTimeLevelIndex++;
                if (activeTimeLevelIndex >= 5)
                {
                    ShowHomePage();
                    return;
                }

                BeginTimeAttackLevel(activeTimeLevelIndex);
            }
        }

        public void TogglePauseFromUi()
        {
            ResolveReferences();

            if (lastPauseToggleFrame == Time.frameCount)
            {
                return;
            }

            lastPauseToggleFrame = Time.frameCount;
            TogglePause();
        }

        public void ResumeFromUi()
        {
            ResolveReferences();

            if (state == GameState.Paused)
            {
                TogglePause();
            }
        }

        public void ToggleCameraLockFromUi()
        {
            ResolveReferences();

            if (lastCameraLockToggleFrame == Time.frameCount)
            {
                return;
            }

            lastCameraLockToggleFrame = Time.frameCount;

            if (inputManager != null)
            {
                inputManager.ToggleCameraLock();
                uiManager.ShowPause(inputManager.CameraLocked ? "Settings - Camera Lock" : "Settings - Free Camera");
                uiManager.SetCameraLockState(inputManager.CameraLocked);
            }
        }

        public bool CanAcceptGameplayInput()
        {
            return state == GameState.Playing;
        }

        private void BeginCampaignLevel(int levelIndex)
        {
            activeCampaignLevelIndex = Mathf.Clamp(levelIndex, FirstCampaignLevelIndex, levelManager.LevelCount - 1);
            BeginLevel(levelManager.SetCurrentLevel(activeCampaignLevelIndex), GameMode.Campaign, activeCampaignLevelIndex);
        }

        private void BeginTimeAttackLevel(int timeLevelIndex)
        {
            activeTimeLevelIndex = Mathf.Clamp(timeLevelIndex, 0, 4);
            BeginLevel(CreateTimeAttackLevel(activeTimeLevelIndex), GameMode.TimeAttack, activeTimeLevelIndex);
        }

        private void BeginLevel(LevelDefinition level, GameMode mode, int levelIndex)
        {
            ResolveReferences();
            UnsubscribeGridEvents();

            activeLevel = level;
            currentMode = mode;
            elapsedTime = 0f;
            state = GameState.Playing;
            tutorialStep = -1;
            tutorialMessage = string.Empty;
            roundRecorded = false;
            Time.timeScale = 1f;

            if (playerController != null)
            {
                playerController.gameObject.SetActive(true);
            }

            gridManager.Configure(activeLevel);
            SubscribeGridEvents();
            gridManager.GenerateGrid();

            inputManager.Configure(gridManager, playerController, this);
            playerController.Configure(gridManager, this, inputManager);
            Vector2Int spawnPosition = gridManager.StartPosition;
            gridManager.RevealStartingArea(spawnPosition);
            playerController.BeginAt(spawnPosition);

            uiManager.Bind(this);
            uiManager.ShowGameplay();

            if (currentMode == GameMode.Tutorial)
            {
                StartTutorialStep(0);
            }

            RefreshHud();
        }

        private void Win(string message)
        {
            if (state != GameState.Playing)
            {
                return;
            }

            state = GameState.Won;
            playerController.SetInputEnabled(false);
            gridManager.PlayWinEffect();
            audioManager.PlayMissionComplete();
            RecordClassicWinIfNeeded();

            if (currentMode == GameMode.Tutorial)
            {
                tutorialMessage = "Tutorial clear. Press Next to start Level mode, or Home to choose another mode.";
            }

            Debug.Log(message, this);
        }

        private void Lose(string message)
        {
            if (state != GameState.Playing)
            {
                return;
            }

            state = GameState.Lost;
            playerController.SetInputEnabled(false);
            gridManager.RevealResultTiles();
            audioManager.PlayExplosion();
            RecordClassicLossIfNeeded();
            Debug.Log(message, this);
        }

        private void TogglePause()
        {
            if (state == GameState.Playing)
            {
                state = GameState.Paused;
                Time.timeScale = 0f;
                uiManager.ShowPause(inputManager != null && inputManager.CameraLocked ? "Settings - Camera Lock" : "Settings - Free Camera");
                if (inputManager != null)
                {
                    uiManager.SetCameraLockState(inputManager.CameraLocked);
                }
            }
            else if (state == GameState.Paused)
            {
                state = GameState.Playing;
                Time.timeScale = 1f;
                uiManager.HidePause();
            }
        }

        private void HandleSystemInput()
        {
            if (state == GameState.Home)
            {
                return;
            }

            if (WasKeyPressed(KeyCode.R))
            {
                RestartLevel();
            }

            if (WasKeyPressed(KeyCode.Escape))
            {
                TogglePause();
            }

            if (state == GameState.Won && WasKeyPressed(KeyCode.N))
            {
                ContinueAfterWin();
            }
        }

        private void SubscribeGridEvents()
        {
            gridManager.DangerTriggered += HandleDangerTriggered;
            gridManager.SafeTilesCleared += HandleSafeTilesCleared;
            gridManager.TileRevealed += HandleTileRevealed;
            gridManager.DefuserCountChanged += HandleDefuserCountChanged;
        }

        private void UnsubscribeGridEvents()
        {
            if (gridManager == null)
            {
                return;
            }

            gridManager.DangerTriggered -= HandleDangerTriggered;
            gridManager.SafeTilesCleared -= HandleSafeTilesCleared;
            gridManager.TileRevealed -= HandleTileRevealed;
            gridManager.DefuserCountChanged -= HandleDefuserCountChanged;
        }

        private void HandleTileRevealed(TileNode tile)
        {
            if (tile.HasDanger && !tile.HasDefuser)
            {
                return;
            }

            audioManager.PlayScan();

            if (currentMode != GameMode.Tutorial || state != GameState.Playing)
            {
                return;
            }

            if (tutorialStep == 0 && tile.Coordinates == gridManager.TutorialTargetCoordinates)
            {
                StartTutorialStep(1);
            }
            else if (tutorialStep == 2 && tile.Coordinates == gridManager.TutorialTargetCoordinates)
            {
                StartTutorialStep(3);
            }
        }

        private void HandleDefuserCountChanged()
        {
            audioManager.PlayDefuser();

            if (currentMode == GameMode.Tutorial
                && state == GameState.Playing
                && tutorialStep == 1
                && gridManager.PlacedDefuserCount > 0)
            {
                StartTutorialStep(2);
            }
        }

        private void HandleDangerTriggered(TileNode tile)
        {
            Lose($"Malware breach at ({tile.X}, {tile.Y}).");
        }

        private void HandleSafeTilesCleared()
        {
            Win("All safe nodes verified.");
        }

        private void StartTutorialStep(int step)
        {
            tutorialStep = step;
            gridManager.SetTutorialStep(step);

            switch (step)
            {
                case 0:
                    tutorialMessage = "Step 1: move to the CLEAR marker. It is safe because the opened numbers around spawn give enough information.";
                    break;
                case 1:
                    tutorialMessage = "Step 2: left click the DEFUSE marker. The nearby number indicates one adjacent danger, so this tile should be marked before stepping on it.";
                    break;
                case 2:
                    tutorialMessage = "Step 3: now move to the next CLEAR marker. After a danger is defused, surrounding safe tiles can be opened confidently.";
                    break;
                default:
                    tutorialMessage = "Keep revealing safe tiles and defusing suspected mines. Win by opening every safe tile.";
                    gridManager.SetTutorialStep(99);
                    break;
            }
        }

        private void RefreshHud()
        {
            if (uiManager == null || state == GameState.Home)
            {
                return;
            }

            string resultMessage = GetStateMessage();
            uiManager.SetSnapshot(
                activeLevel.LevelName,
                activeLevel.WinCondition,
                currentMode,
                state,
                elapsedTime,
                activeLevel.TimeLimit,
                gridManager.RemainingDefusers,
                gridManager.SafeTilesRemaining,
                resultMessage,
                GetModeStatsText());

            if (inputManager != null)
            {
                uiManager.SetCameraLockState(inputManager.CameraLocked);
            }
        }

        private string GetStateMessage()
        {
            if (currentMode == GameMode.Tutorial && !string.IsNullOrEmpty(tutorialMessage))
            {
                return tutorialMessage;
            }

            switch (state)
            {
                case GameState.Paused:
                    return "Paused";
                case GameState.Won:
                    if (currentMode == GameMode.Classic)
                    {
                        return "Classic clear. Press Replay for a new random board.";
                    }

                    return "Mission clear. Press Next to continue, Replay to retry, or Home for menu.";
                case GameState.Lost:
                    return currentMode == GameMode.TimeAttack
                        ? "System breached or time expired. Press Replay to retry."
                        : "System breached. Press Replay to retry.";
                default:
                    return "WASD move | Space jump | Left click defuser | Hold right click rotate";
            }
        }

        private string GetModeStatsText()
        {
            return currentMode == GameMode.Classic ? GetClassicStatsText() : string.Empty;
        }

        private string GetClassicStatsText()
        {
            string best = classicBestTime > 0f ? $"{classicBestTime:0.0}s" : "--";
            return $"Classic: {classicWins}/{classicAttempts} wins   Best {best}";
        }

        private void LoadClassicStats()
        {
            classicWins = PlayerPrefs.GetInt(ClassicWinsKey, 0);
            classicAttempts = PlayerPrefs.GetInt(ClassicAttemptsKey, 0);
            classicBestTime = PlayerPrefs.GetFloat(ClassicBestTimeKey, 0f);
        }

        private void SaveClassicStats()
        {
            PlayerPrefs.SetInt(ClassicWinsKey, classicWins);
            PlayerPrefs.SetInt(ClassicAttemptsKey, classicAttempts);
            PlayerPrefs.SetFloat(ClassicBestTimeKey, classicBestTime);
            PlayerPrefs.Save();
        }

        private void RecordClassicWinIfNeeded()
        {
            if (currentMode != GameMode.Classic || roundRecorded)
            {
                return;
            }

            roundRecorded = true;
            classicAttempts++;
            classicWins++;

            if (classicBestTime <= 0f || elapsedTime < classicBestTime)
            {
                classicBestTime = elapsedTime;
            }

            SaveClassicStats();
        }

        private void RecordClassicLossIfNeeded()
        {
            if (currentMode != GameMode.Classic || roundRecorded)
            {
                return;
            }

            roundRecorded = true;
            classicAttempts++;
            SaveClassicStats();
        }

        private static LevelDefinition CreateClassicLevel()
        {
            return new LevelDefinition(
                "Classic",
                8,
                8,
                8,
                8,
                0f,
                new Vector2Int(4, 4),
                new Vector2Int(7, 7),
                WinConditionType.ClearSafeTiles,
                0);
        }

        private static LevelDefinition CreateTimeAttackLevel(int index)
        {
            int size = 8;
            int dangers = 10;
            float limit = 90f;

            return new LevelDefinition(
                "Time",
                size,
                size,
                dangers,
                dangers,
                limit,
                new Vector2Int(size / 2, size / 2),
                new Vector2Int(size - 1, size - 1),
                WinConditionType.ClearSafeTiles,
                0);
        }

        private void ResolveReferences()
        {
            if (gridManager == null)
            {
                gridManager = FindAnyObjectByType<GridManager>();
            }

            if (levelManager == null)
            {
                levelManager = FindAnyObjectByType<LevelManager>();
            }

            if (playerController == null)
            {
                playerController = FindAnyObjectByType<PlayerController>();
            }

            if (inputManager == null)
            {
                inputManager = FindAnyObjectByType<InputManager>();
            }

            if (uiManager == null)
            {
                uiManager = FindAnyObjectByType<UIManager>();
            }

            if (audioManager == null)
            {
                audioManager = FindAnyObjectByType<AudioManager>();
            }

            GameObject root = GameObject.Find("CyberMinefield");
            if (root == null)
            {
                root = gameObject;
            }

            if (levelManager == null)
            {
                levelManager = root.AddComponent<LevelManager>();
            }

            if (gridManager == null)
            {
                gridManager = root.AddComponent<GridManager>();
            }

            if (playerController == null)
            {
                GameObject playerObject = new GameObject("Player");
                playerController = playerObject.AddComponent<PlayerController>();
            }

            if (inputManager == null)
            {
                inputManager = root.AddComponent<InputManager>();
            }

            if (uiManager == null)
            {
                uiManager = UIManager.CreateRuntimeHud();
            }

            if (audioManager == null)
            {
                audioManager = root.AddComponent<AudioManager>();
            }

            uiManager.Bind(this);
        }

        private static bool WasKeyPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyCode == KeyCode.R)
                {
                    return keyboard.rKey.wasPressedThisFrame;
                }

                if (keyCode == KeyCode.N)
                {
                    return keyboard.nKey.wasPressedThisFrame;
                }

                if (keyCode == KeyCode.Escape)
                {
                    return keyboard.escapeKey.wasPressedThisFrame;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }
    }
}
