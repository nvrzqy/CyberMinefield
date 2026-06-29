using System.Collections;
using CyberMinefield.Audio;
using CyberMinefield.Grid;
using CyberMinefield.Levels;
using CyberMinefield.Player;
using CyberMinefield.UI;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CyberMinefield.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        private const int FirstCampaignLevelIndex = 1;
        private const string SaveInitializedKey = "CyberMinefield.SaveInitialized.v1";
        private const string BuildGuidKey = "CyberMinefield.BuildGuid";
        private const string CampaignUnlockKey = "CyberMinefield.CampaignUnlockedLevel";
        private const string ClassicWinsKey = "CyberMinefield.ClassicWins";
        private const string ClassicAttemptsKey = "CyberMinefield.ClassicAttempts";
        private const string ClassicBestTimeKey = "CyberMinefield.ClassicBestTime";
        private static readonly Vector2Int InvalidTutorialTarget = new Vector2Int(int.MinValue, int.MinValue);
        private static readonly string[] StoryLines =
        {
            "Hey... can you hear me?",
            "I was playing on my computer.",
            "Then I clicked\na weird file by mistake.",
            "The screen glitched.\nNow I am trapped inside this game.",
            "The virus is hiding\nunder these tiles.",
            "Numbers are clues.\nSome tiles are infected.",
            "Please help me place defusers\nand clean the safe tiles.",
            "If we clear every level,\nI can escape.",
            "Click or press Space.\nLet's start the tutorial."
        };
        private static readonly string[] EndingStoryLines =
        {
            "You did it!",
            "The last virus core is gone.",
            "My computer is finally clean again.",
            "And I can get out of this game.",
            "Thank you for helping me.",
            "Cyber Minefield is safe now."
        };

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
        private bool tutorialInputBlocked;
        private Vector2Int tutorialDefuseTarget = InvalidTutorialTarget;
        private Vector2Int tutorialClearTarget = InvalidTutorialTarget;
        private bool roundRecorded;
        private int highestUnlockedCampaignLevelIndex;
        private int storyLineIndex;
        private bool storyReturnsHome;
        private Coroutine autoContinueCoroutine;
        private Coroutine levelLoadCoroutine;
        private int classicWins;
        private int classicAttempts;
        private float classicBestTime;
        private int lastPauseToggleFrame = -1;
        private int lastCameraLockToggleFrame = -1;
        private int lastContinueFrame = -1;
        private float lastPauseToggleTime = -10f;
        private float lastCameraLockToggleTime = -10f;
        private float lastContinueTime = -10f;

        public GameState State => state;
        public LevelDefinition ActiveLevel => activeLevel;
        public float ElapsedTime => elapsedTime;

        private void Awake()
        {
            ResolveReferences();
            EnsureFreshInstallDefaults();
            LoadCampaignProgress();
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
                    Lose("Time expired. Press Restart to try again.");
                }
            }

            RefreshHud();
        }

        public void ShowHomePage()
        {
            ResolveReferences();
            UnsubscribeGridEvents();
            StopAutoContinue();
            StopLevelLoad();
            Time.timeScale = 1f;
            state = GameState.Home;
            currentMode = GameMode.Home;
            activeLevel = null;
            tutorialStep = -1;
            tutorialMessage = string.Empty;
            tutorialInputBlocked = false;
            tutorialDefuseTarget = InvalidTutorialTarget;
            tutorialClearTarget = InvalidTutorialTarget;

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
            uiManager.ShowHome(GetClassicStatsText(), highestUnlockedCampaignLevelIndex, levelManager.LevelCount);
        }

        public void StartNewGame()
        {
            ResolveReferences();
            StopLevelLoad();
            ResetCampaignProgress();
            ShowStoryIntro();
        }

        public void ShowStoryIntro()
        {
            ResolveReferences();
            StopAutoContinue();
            StopLevelLoad();
            UnsubscribeGridEvents();
            Time.timeScale = 1f;
            state = GameState.Story;
            currentMode = GameMode.Home;
            activeLevel = null;
            tutorialStep = -1;
            tutorialMessage = string.Empty;
            tutorialInputBlocked = false;
            tutorialDefuseTarget = InvalidTutorialTarget;
            tutorialClearTarget = InvalidTutorialTarget;
            storyLineIndex = 0;
            storyReturnsHome = false;

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
            uiManager.ShowStory(StoryLines[storyLineIndex], storyLineIndex + 1, StoryLines.Length);
        }

        private void ShowEndingStory()
        {
            ResolveReferences();
            StopAutoContinue();
            UnsubscribeGridEvents();
            Time.timeScale = 1f;
            state = GameState.Story;
            currentMode = GameMode.Home;
            activeLevel = null;
            tutorialStep = -1;
            tutorialMessage = string.Empty;
            tutorialInputBlocked = false;
            tutorialDefuseTarget = InvalidTutorialTarget;
            tutorialClearTarget = InvalidTutorialTarget;
            storyLineIndex = 0;
            storyReturnsHome = true;

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
            uiManager.ShowStory(EndingStoryLines[storyLineIndex], storyLineIndex + 1, EndingStoryLines.Length);
        }

        public void AdvanceStory()
        {
            ResolveReferences();

            if (state != GameState.Story)
            {
                return;
            }

            if (lastContinueFrame == Time.frameCount || Time.unscaledTime - lastContinueTime < 0.18f)
            {
                return;
            }

            lastContinueFrame = Time.frameCount;
            lastContinueTime = Time.unscaledTime;
            storyLineIndex++;

            string[] activeStoryLines = storyReturnsHome ? EndingStoryLines : StoryLines;
            if (storyLineIndex >= activeStoryLines.Length)
            {
                if (storyReturnsHome)
                {
                    ShowHomePage();
                    return;
                }

                UnlockCampaignLevel(FirstCampaignLevelIndex);
                StartTutorial();
                return;
            }

            uiManager.ShowStory(activeStoryLines[storyLineIndex], storyLineIndex + 1, activeStoryLines.Length);
        }

        public void StartTutorial()
        {
            StopAutoContinue();
            BeginLevel(levelManager.SetCurrentLevel(0), GameMode.Tutorial, 0);
        }

        public void StartCampaign()
        {
            if (highestUnlockedCampaignLevelIndex < FirstCampaignLevelIndex)
            {
                ShowHomePage();
                return;
            }

            activeCampaignLevelIndex = Mathf.Clamp(
                highestUnlockedCampaignLevelIndex,
                FirstCampaignLevelIndex,
                levelManager.LevelCount - 1);
            BeginCampaignLevel(activeCampaignLevelIndex);
        }

        public void ShowLevelSelect()
        {
            ResolveReferences();

            if (highestUnlockedCampaignLevelIndex < FirstCampaignLevelIndex)
            {
                ShowHomePage();
                return;
            }

            UnsubscribeGridEvents();
            Time.timeScale = 1f;
            state = GameState.Home;
            currentMode = GameMode.Home;
            activeLevel = null;
            tutorialStep = -1;
            tutorialMessage = string.Empty;
            tutorialInputBlocked = false;
            tutorialDefuseTarget = InvalidTutorialTarget;
            tutorialClearTarget = InvalidTutorialTarget;

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
            uiManager.ShowLevelSelect(highestUnlockedCampaignLevelIndex, levelManager.LevelCount);
        }

        public void StartCampaignLevelFromMenu(int levelIndex)
        {
            ResolveReferences();

            if (highestUnlockedCampaignLevelIndex < FirstCampaignLevelIndex
                || levelIndex < FirstCampaignLevelIndex
                || levelIndex >= levelManager.LevelCount
                || levelIndex > highestUnlockedCampaignLevelIndex)
            {
                return;
            }

            BeginCampaignLevel(levelIndex);
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
            if (highestUnlockedCampaignLevelIndex < FirstCampaignLevelIndex)
            {
                ShowHomePage();
                return;
            }

            int maxLevel = Mathf.Clamp(highestUnlockedCampaignLevelIndex, FirstCampaignLevelIndex, levelManager.LevelCount - 1);
            activeCampaignLevelIndex = Mathf.Clamp(levelIndex, FirstCampaignLevelIndex, maxLevel);
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

            if (Time.unscaledTime - lastContinueTime < 0.18f)
            {
                return;
            }

            lastContinueFrame = Time.frameCount;
            lastContinueTime = Time.unscaledTime;

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
                    ShowEndingStory();
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

            if (Time.unscaledTime - lastPauseToggleTime < 0.18f)
            {
                return;
            }

            lastPauseToggleFrame = Time.frameCount;
            lastPauseToggleTime = Time.unscaledTime;

            if (state == GameState.Home)
            {
                uiManager.ShowPause(inputManager != null && inputManager.CameraLocked ? "Settings - Camera Lock" : "Settings - Free Camera");
                if (inputManager != null)
                {
                    uiManager.SetCameraLockState(inputManager.CameraLocked);
                }

                return;
            }

            TogglePause();
        }

        public void ResumeFromUi()
        {
            ResolveReferences();

            if (state == GameState.Home)
            {
                uiManager.ShowHome(GetClassicStatsText(), highestUnlockedCampaignLevelIndex, levelManager.LevelCount);
                return;
            }

            if (state == GameState.Paused)
            {
                if (Time.unscaledTime - lastPauseToggleTime < 0.18f)
                {
                    return;
                }

                lastPauseToggleTime = Time.unscaledTime;
                TogglePause();
            }
        }

        public void QuitGame()
        {
            PlayerPrefs.Save();
            Time.timeScale = 1f;

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ToggleCameraLockFromUi()
        {
            ResolveReferences();

            if (lastCameraLockToggleFrame == Time.frameCount)
            {
                return;
            }

            if (Time.unscaledTime - lastCameraLockToggleTime < 0.18f)
            {
                return;
            }

            lastCameraLockToggleFrame = Time.frameCount;
            lastCameraLockToggleTime = Time.unscaledTime;

            if (inputManager != null)
            {
                inputManager.ToggleCameraLock();
                uiManager.ShowPause(inputManager.CameraLocked ? "Settings - Camera Lock" : "Settings - Free Camera");
                uiManager.SetCameraLockState(inputManager.CameraLocked);
            }
        }

        public bool CanAcceptGameplayInput()
        {
            return state == GameState.Playing && !tutorialInputBlocked;
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
            if (levelLoadCoroutine != null)
            {
                StopCoroutine(levelLoadCoroutine);
            }

            levelLoadCoroutine = StartCoroutine(BeginLevelRoutine(level, mode, levelIndex));
        }

        private IEnumerator BeginLevelRoutine(LevelDefinition level, GameMode mode, int levelIndex)
        {
            ResolveReferences();
            UnsubscribeGridEvents();
            StopAutoContinue();

            activeLevel = level;
            currentMode = mode;
            elapsedTime = 0f;
            state = GameState.Loading;
            tutorialStep = -1;
            tutorialMessage = string.Empty;
            tutorialInputBlocked = false;
            tutorialDefuseTarget = InvalidTutorialTarget;
            tutorialClearTarget = InvalidTutorialTarget;
            roundRecorded = false;
            Time.timeScale = 1f;

            if (playerController != null)
            {
                playerController.SetInputEnabled(false);
                playerController.gameObject.SetActive(false);
            }

            uiManager.Bind(this);
            uiManager.ShowLoading(BuildLoadingTitle(mode, level));
            RefreshHud();
            yield return null;
            yield return new WaitForEndOfFrame();

            if (state != GameState.Loading || activeLevel != level)
            {
                levelLoadCoroutine = null;
                yield break;
            }

            gridManager.Configure(activeLevel);
            SubscribeGridEvents();
            yield return gridManager.GenerateGridAsync();

            if (playerController != null)
            {
                playerController.gameObject.SetActive(true);
            }

            inputManager.Configure(gridManager, playerController, this);
            playerController.Configure(gridManager, this, inputManager);
            Vector2Int spawnPosition = gridManager.StartPosition;
            gridManager.RevealStartingArea(spawnPosition);
            playerController.BeginAt(spawnPosition);

            state = GameState.Playing;
            uiManager.ShowGameplay();

            if (currentMode == GameMode.Tutorial)
            {
                StartTutorialStep(0);
            }

            RefreshHud();
            levelLoadCoroutine = null;
        }

        private static string BuildLoadingTitle(GameMode mode, LevelDefinition level)
        {
            if (mode == GameMode.Classic)
            {
                return "Loading Classic...";
            }

            if (mode == GameMode.TimeAttack)
            {
                return "Loading Time Mode...";
            }

            if (mode == GameMode.Tutorial)
            {
                return "Loading Tutorial...";
            }

            return string.IsNullOrWhiteSpace(level.LevelName)
                ? "Loading Level..."
                : $"Loading {level.LevelName}...";
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
                tutorialMessage = "Tutorial clear. Loading Level 1...";
                UnlockCampaignLevel(FirstCampaignLevelIndex);
                autoContinueCoroutine = StartCoroutine(BeginCampaignLevelAfterDelay(1.15f, FirstCampaignLevelIndex));
            }
            else if (currentMode == GameMode.Campaign)
            {
                UnlockCampaignLevel(activeCampaignLevelIndex + 1);
                if (activeCampaignLevelIndex + 1 >= levelManager.LevelCount)
                {
                    autoContinueCoroutine = StartCoroutine(ShowEndingStoryAfterDelay(1.2f));
                }
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
            audioManager.PlayExplosion();
            RecordClassicLossIfNeeded();
            autoContinueCoroutine = StartCoroutine(PlayGameOverSequence(message));
            Debug.Log(message, this);
        }

        private IEnumerator PlayGameOverSequence(string message)
        {
            if (gridManager != null)
            {
                yield return gridManager.PlayLoseVirusSpread();
            }

            if (uiManager != null)
            {
                uiManager.ShowGameOver(message);
            }

            autoContinueCoroutine = null;
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
            if (state == GameState.Story)
            {
                if (WasAdvancePressed())
                {
                    AdvanceStory();
                }

                return;
            }

            if (state == GameState.Home)
            {
                return;
            }

            if (currentMode == GameMode.Tutorial && state == GameState.Playing && tutorialInputBlocked)
            {
                if (WasAdvancePressed())
                {
                    AdvanceTutorialFromInfoStep();
                }

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
            gridManager.DefuserPlaced += HandleDefuserPlaced;
            gridManager.SafeTilesCleared += HandleSafeTilesCleared;
            gridManager.TileRevealed += HandleTileRevealed;
            gridManager.TileEntered += HandleTileEntered;
            gridManager.DefuserCountChanged += HandleDefuserCountChanged;
        }

        private void UnsubscribeGridEvents()
        {
            if (gridManager == null)
            {
                return;
            }

            gridManager.DangerTriggered -= HandleDangerTriggered;
            gridManager.DefuserPlaced -= HandleDefuserPlaced;
            gridManager.SafeTilesCleared -= HandleSafeTilesCleared;
            gridManager.TileRevealed -= HandleTileRevealed;
            gridManager.TileEntered -= HandleTileEntered;
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

            if (tile.Coordinates != gridManager.TutorialTargetCoordinates)
            {
                return;
            }

            switch (tutorialStep)
            {
                case 3:
                    StartTutorialStep(4);
                    break;
                case 4:
                    StartTutorialStep(5);
                    break;
                case 5:
                    StartTutorialStep(6);
                    break;
            }
        }

        private void HandleTileEntered(TileNode tile)
        {
            if (currentMode != GameMode.Tutorial
                || state != GameState.Playing
                || tutorialStep != 3
                || tile == null
                || tile.Coordinates != tutorialDefuseTarget
                || !tile.HasDefuser)
            {
                return;
            }

            StartTutorialStep(4);
        }

        private void HandleDefuserCountChanged()
        {
        }

        private void HandleDefuserPlaced(TileNode tile)
        {
            audioManager.PlayDefuser();

            if (currentMode == GameMode.Tutorial
                && state == GameState.Playing
                && tutorialStep == 1
                && tile != null
                && tile.Coordinates == tutorialDefuseTarget)
            {
                StartTutorialStep(2);
            }
        }

        public void NotifyMarkerStyleChanged(DefuserMarkerStyle markerStyle)
        {
            if (currentMode == GameMode.Tutorial && state == GameState.Playing && tutorialStep == 2)
            {
                StartTutorialStep(3);
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
            tutorialInputBlocked = false;
            gridManager.ClearTutorialHints();
            uiManager.ClearTutorialFocus();

            switch (step)
            {
                case 0:
                    tutorialInputBlocked = true;
                    tutorialMessage = "The number on the tiles represents how many viruses are touching that tile.";
                    FocusTutorialNumberTile();
                    break;
                case 1:
                    tutorialMessage = "Check all the corners. Defuse the pointed tile with left click.";
                    gridManager.SetTutorialStep(1);
                    tutorialDefuseTarget = gridManager.TutorialTargetCoordinates;
                    FocusTutorialTarget(TutorialFocusTarget.DefuseTile, tutorialDefuseTarget, false);
                    break;
                case 2:
                    tutorialInputBlocked = true;
                    tutorialMessage = "You can choose Flag or Virus to mark where the viruses are.";
                    uiManager.SetTutorialFocus(TutorialFocusTarget.MarkerSelector, false);
                    break;
                case 3:
                    tutorialMessage = "Tiles that have been defused are safe to step on!";
                    gridManager.SetTutorialHintAt(tutorialDefuseTarget, "SAFE", new Color(0.45f, 1f, 1f));
                    FocusTutorialTarget(TutorialFocusTarget.DefusedTile, tutorialDefuseTarget, false);
                    break;
                case 4:
                    tutorialMessage = "Clear all the surrounding tiles once the flags satisfy the number on the tile.";
                    gridManager.SetTutorialClearHintNear(tutorialDefuseTarget, "CLEAR", new Color(0.7f, 1f, 0.75f));
                    tutorialClearTarget = gridManager.TutorialTargetCoordinates;
                    FocusTutorialTarget(TutorialFocusTarget.ClearTile, tutorialClearTarget, false);
                    break;
                case 5:
                    tutorialMessage = "Continue forward.";
                    gridManager.SetTutorialClearHintNear(tutorialClearTarget, "CLEAR", new Color(0.7f, 1f, 0.75f));
                    FocusTutorialTarget(TutorialFocusTarget.ForwardTile, gridManager.TutorialTargetCoordinates, false);
                    break;
                case 6:
                    tutorialInputBlocked = true;
                    tutorialMessage = "You can see how many viruses are left here.";
                    uiManager.SetTutorialFocus(TutorialFocusTarget.DefuserStats, true);
                    break;
                case 7:
                    tutorialInputBlocked = true;
                    tutorialMessage = "You can also see how long you've been playing on this level here.";
                    uiManager.SetTutorialFocus(TutorialFocusTarget.TimerStats, true);
                    break;
                case 8:
                    tutorialInputBlocked = true;
                    tutorialMessage = "Hold right click to rotate the camera. Camera Lock is in Settings.";
                    uiManager.SetTutorialFocus(TutorialFocusTarget.SettingsButton, true);
                    break;
                case 9:
                    tutorialInputBlocked = true;
                    tutorialMessage = "Restart with this button, or press R on your keyboard.";
                    uiManager.SetTutorialFocus(TutorialFocusTarget.RestartButton, true);
                    break;
                case 10:
                    tutorialInputBlocked = true;
                    tutorialMessage = "To go back to the home page, click the Home button.";
                    uiManager.SetTutorialFocus(TutorialFocusTarget.HomeButton, true);
                    break;
                default:
                    tutorialMessage = "Now finish clearing every safe tile.";
                    gridManager.SetTutorialStep(99);
                    uiManager.ClearTutorialFocus();
                    break;
            }
        }

        private void AdvanceTutorialFromInfoStep()
        {
            switch (tutorialStep)
            {
                case 0:
                    StartTutorialStep(1);
                    break;
                case 2:
                    StartTutorialStep(3);
                    break;
                case 6:
                    StartTutorialStep(7);
                    break;
                case 7:
                    StartTutorialStep(8);
                    break;
                case 8:
                    StartTutorialStep(9);
                    break;
                case 9:
                    StartTutorialStep(10);
                    break;
                case 10:
                    StartTutorialStep(11);
                    break;
            }
        }

        private void FocusTutorialNumberTile()
        {
            Vector2Int focus = gridManager.StartPosition;
            foreach (TileNode tile in gridManager.TilesByCoordinate.Values)
            {
                if (tile != null && tile.IsRevealed && !tile.HasDanger && tile.AdjacentDangerCount > 0)
                {
                    focus = tile.Coordinates;
                    break;
                }
            }

            gridManager.SetTutorialHintAt(focus, "CLUE", new Color(0.7f, 1f, 0.75f));
            FocusTutorialTarget(TutorialFocusTarget.NumberTile, focus, true);
        }

        private void FocusTutorialTarget(TutorialFocusTarget focusTarget, Vector2Int coordinates, bool blocksInteraction)
        {
            if (coordinates == InvalidTutorialTarget
                || gridManager == null
                || uiManager == null
                || !gridManager.TryGetTile(coordinates, out TileNode tile)
                || tile == null)
            {
                uiManager?.SetTutorialFocus(TutorialFocusTarget.None, blocksInteraction);
                return;
            }

            Vector3 focusPosition = tile.transform.position + Vector3.up * 0.28f;
            uiManager.SetTutorialWorldFocus(focusTarget, focusPosition, new Vector2(185f, 145f), blocksInteraction);
        }

        private void RefreshHud()
        {
            if (uiManager == null || state == GameState.Home || state == GameState.Story)
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
                        return "Classic clear. Press Restart for a new random board.";
                    }

                    return "Mission clear. Press Next to continue, Restart to retry, or Home for menu.";
                case GameState.Lost:
                    return currentMode == GameMode.TimeAttack
                        ? "System breached or time expired. Press Restart to retry."
                        : "System breached. Press Restart to retry.";
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

        private static void EnsureFreshInstallDefaults()
        {
            string currentBuildGuid = GetCurrentBuildGuid();
            string savedBuildGuid = PlayerPrefs.GetString(BuildGuidKey, string.Empty);
            bool isInitialized = PlayerPrefs.GetInt(SaveInitializedKey, 0) == 1;
            bool isNewBuild = savedBuildGuid != currentBuildGuid;

            if (isInitialized && !isNewBuild)
            {
                return;
            }

            PlayerPrefs.DeleteKey(CampaignUnlockKey);
            PlayerPrefs.DeleteKey(ClassicWinsKey);
            PlayerPrefs.DeleteKey(ClassicAttemptsKey);
            PlayerPrefs.DeleteKey(ClassicBestTimeKey);
            PlayerPrefs.SetInt(SaveInitializedKey, 1);
            PlayerPrefs.SetString(BuildGuidKey, currentBuildGuid);
            PlayerPrefs.Save();
        }

        private static string GetCurrentBuildGuid()
        {
#if UNITY_EDITOR
            return "Editor";
#else
            string buildGuid = Application.buildGUID;
            return string.IsNullOrEmpty(buildGuid) ? Application.version : buildGuid;
#endif
        }

        private void LoadCampaignProgress()
        {
            highestUnlockedCampaignLevelIndex = PlayerPrefs.GetInt(CampaignUnlockKey, 0);
        }

        private void ResetCampaignProgress()
        {
            highestUnlockedCampaignLevelIndex = 0;
            SaveCampaignProgress();
        }

        private void UnlockCampaignLevel(int levelIndex)
        {
            int clamped = Mathf.Clamp(levelIndex, 0, levelManager != null ? levelManager.LevelCount : levelIndex);
            if (clamped <= highestUnlockedCampaignLevelIndex)
            {
                return;
            }

            highestUnlockedCampaignLevelIndex = clamped;
            SaveCampaignProgress();
        }

        private void SaveCampaignProgress()
        {
            PlayerPrefs.SetInt(CampaignUnlockKey, highestUnlockedCampaignLevelIndex);
            PlayerPrefs.Save();
        }

        private IEnumerator BeginCampaignLevelAfterDelay(float delay, int levelIndex)
        {
            yield return new WaitForSecondsRealtime(delay);
            autoContinueCoroutine = null;
            BeginCampaignLevel(levelIndex);
        }

        private IEnumerator ShowEndingStoryAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            autoContinueCoroutine = null;
            ShowEndingStory();
        }

        private void StopAutoContinue()
        {
            if (autoContinueCoroutine == null)
            {
                return;
            }

            StopCoroutine(autoContinueCoroutine);
            autoContinueCoroutine = null;
        }

        private void StopLevelLoad()
        {
            if (levelLoadCoroutine == null)
            {
                return;
            }

            StopCoroutine(levelLoadCoroutine);
            levelLoadCoroutine = null;
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
                20,
                20,
                100,
                100,
                0f,
                new Vector2Int(10, 10),
                new Vector2Int(19, 19),
                WinConditionType.ClearSafeTiles,
                0);
        }

        private static LevelDefinition CreateTimeAttackLevel(int index)
        {
            int size = 9 + index;
            int dangers = 22 + index * 7;
            float limit = 40f;

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

                if (keyCode == KeyCode.Space)
                {
                    return keyboard.spaceKey.wasPressedThisFrame;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

        private static bool WasAdvancePressed()
        {
            bool keyboardAdvance = WasKeyPressed(KeyCode.Space);
            bool mouseAdvance = false;
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                mouseAdvance = mouse.leftButton.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            mouseAdvance = mouseAdvance || Input.GetMouseButtonDown(0);
#endif
            return keyboardAdvance || mouseAdvance;
        }
    }
}
