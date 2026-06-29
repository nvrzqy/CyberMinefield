using CyberMinefield.Audio;
using CyberMinefield.Core;
using CyberMinefield.Grid;
using CyberMinefield.Levels;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CyberMinefield.UI
{
    public enum TutorialFocusTarget
    {
        None,
        NumberTile,
        DefuseTile,
        MarkerSelector,
        DefusedTile,
        ClearTile,
        ForwardTile,
        DefuserStats,
        TimerStats,
        SettingsButton,
        RestartButton,
        HomeButton
    }

    public sealed class UIManager : MonoBehaviour
    {
        private const int CurrentLayoutVersion = 20;

        [SerializeField] private Text levelText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text messageText;
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject storyPanel;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private GameObject levelSelectPanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Text homeStatsText;
        [SerializeField] private Text storyText;
        [SerializeField] private Text storyProgressText;
        [SerializeField] private Text loadingText;
        [SerializeField] private Text loadingHintText;
        [SerializeField] private Image storyCharacterImage;
        [SerializeField] private Text pauseTitleText;
        [SerializeField] private Text levelSelectTitleText;
        [SerializeField] private Text gameOverTitleText;
        [SerializeField] private Text gameOverMessageText;
        [SerializeField] private Text sfxVolumeText;
        [SerializeField] private Text musicVolumeText;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private RectTransform levelSelectEntriesRoot;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button tutorialButton;
        [SerializeField] private Button levelButton;
        [SerializeField] private Button classicButton;
        [SerializeField] private Button timeButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Text tutorialButtonLabel;
        [SerializeField] private Text levelButtonLabel;
        [SerializeField] private Text classicButtonLabel;
        [SerializeField] private Text timeButtonLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button cameraLockButton;
        [SerializeField] private Text cameraLockButtonLabel;
        [SerializeField] private Text flagMarkerButtonLabel;
        [SerializeField] private Text virusMarkerButtonLabel;
        [SerializeField] private Image[] tutorialDimPanels;
        [SerializeField] private int layoutVersion;

        private GameManager gameManager;
        private Font font;
        private Sprite[] buttonSprites;
        private Sprite[] storyKidSprites;
        private readonly List<Button> runtimeButtons = new List<Button>();
        private int storyFrameIndex;
        private float storyFrameTimer;
        private string loadingBaseText = "Loading";
        private int lastManualClickFrame = -1;
        private float lastManualClickTime = -10f;
        private TutorialFocusTarget tutorialFocusTarget = TutorialFocusTarget.None;
        private Vector3 tutorialFocusWorldPosition;
        private Vector2 tutorialWorldFocusSize = new Vector2(180f, 150f);
        private bool tutorialFocusActive;
        private bool tutorialFocusUsesWorld;
        private bool tutorialBlocksInteraction;

        public static UIManager CreateRuntimeHud()
        {
            GameObject canvasObject = new GameObject("UIManager");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            ConfigureCanvasScaler(canvasObject.AddComponent<CanvasScaler>());
            canvasObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            UIManager uiManager = canvasObject.AddComponent<UIManager>();
            uiManager.BuildLayout(canvasObject.transform);
            return uiManager;
        }

        private void Awake()
        {
            EnsureCanvasSettings();

            if (NeedsLayoutRebuild())
            {
                ClearChildren(transform);
                ClearReferences();
                BuildLayout(transform);
            }

            EnsureEventSystem();
            EnsureMainMenuController();
            ShowHome(string.Empty);
        }

        private void EnsureCanvasSettings()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            ConfigureCanvasScaler(scaler);

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private static void ConfigureCanvasScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void Update()
        {
            AnimateStoryCharacter();
            AnimateLoadingText();
            UpdateTutorialSpotlight();
            InvokeButtonUnderMouseIfNeeded();
        }

        private void OnGUI()
        {
            Event currentEvent = Event.current;
            if (currentEvent == null
                || (currentEvent.type != EventType.MouseDown && currentEvent.type != EventType.MouseUp)
                || currentEvent.button != 0
                || lastManualClickFrame == Time.frameCount)
            {
                return;
            }

            if (InvokeButtonUnderPosition(GuiToScreenPoint(currentEvent.mousePosition)))
            {
                currentEvent.Use();
            }
        }

        public void Bind(GameManager manager)
        {
            gameManager = manager;
        }

        public void ShowHome(string classicStats)
        {
            ShowHome(classicStats, 0, 0);
        }

        public void ShowHome(string classicStats, int unlockedCampaignLevel, int levelCount)
        {
            EnsureLayout();
            ApplyPanelRaycastRules();
            ClearTutorialFocus();
            homePanel.SetActive(true);
            storyPanel.SetActive(false);
            loadingPanel.SetActive(false);
            hudPanel.SetActive(false);
            pausePanel.SetActive(false);
            controlsPanel.SetActive(false);
            audioPanel.SetActive(false);
            levelSelectPanel.SetActive(false);
            gameOverPanel.SetActive(false);
            homeStatsText.text = classicStats;
            SetHomeProgress(unlockedCampaignLevel, levelCount);
        }

        public void ShowLevelSelect(int unlockedCampaignLevel, int levelCount)
        {
            EnsureLayout();
            ApplyPanelRaycastRules();
            ClearTutorialFocus();
            homePanel.SetActive(false);
            storyPanel.SetActive(false);
            loadingPanel.SetActive(false);
            hudPanel.SetActive(false);
            pausePanel.SetActive(false);
            controlsPanel.SetActive(false);
            audioPanel.SetActive(false);
            levelSelectPanel.SetActive(true);
            gameOverPanel.SetActive(false);
            PopulateLevelSelect(unlockedCampaignLevel, levelCount);
        }

        public void ShowStory(string line, int lineNumber, int totalLines)
        {
            EnsureLayout();
            ApplyPanelRaycastRules();
            ClearTutorialFocus();
            homePanel.SetActive(false);
            storyPanel.SetActive(true);
            loadingPanel.SetActive(false);
            hudPanel.SetActive(false);
            pausePanel.SetActive(false);
            controlsPanel.SetActive(false);
            audioPanel.SetActive(false);
            levelSelectPanel.SetActive(false);
            gameOverPanel.SetActive(false);
            storyText.text = line;
            storyProgressText.text = $"{lineNumber}/{totalLines}  Click or Space";
            storyFrameIndex = 0;
            storyFrameTimer = 0f;
            ApplyStoryFrame();
        }

        public void ShowLoading(string title)
        {
            EnsureLayout();
            ApplyPanelRaycastRules();
            ClearTutorialFocus();
            homePanel.SetActive(false);
            storyPanel.SetActive(false);
            loadingPanel.SetActive(true);
            hudPanel.SetActive(false);
            pausePanel.SetActive(false);
            controlsPanel.SetActive(false);
            audioPanel.SetActive(false);
            levelSelectPanel.SetActive(false);
            gameOverPanel.SetActive(false);
            loadingBaseText = string.IsNullOrWhiteSpace(title) ? "Loading" : title;
            AnimateLoadingText(true);
        }

        public void ShowGameplay()
        {
            EnsureLayout();
            ApplyPanelRaycastRules();
            ClearTutorialFocus();
            homePanel.SetActive(false);
            storyPanel.SetActive(false);
            loadingPanel.SetActive(false);
            hudPanel.SetActive(true);
            pausePanel.SetActive(false);
            controlsPanel.SetActive(false);
            audioPanel.SetActive(false);
            levelSelectPanel.SetActive(false);
            gameOverPanel.SetActive(false);
        }

        public void ShowPause(string title)
        {
            EnsureLayout();
            ApplyPanelRaycastRules();
            ClearTutorialFocus();
            storyPanel.SetActive(false);
            loadingPanel.SetActive(false);
            levelSelectPanel.SetActive(false);
            gameOverPanel.SetActive(false);
            pausePanel.SetActive(true);
            controlsPanel.SetActive(false);
            audioPanel.SetActive(false);
            pauseTitleText.text = title;
        }

        public void HidePause()
        {
            EnsureLayout();
            ClearTutorialFocus();
            pausePanel.SetActive(false);
            controlsPanel.SetActive(false);
            audioPanel.SetActive(false);
        }

        public void ShowControlsPopup()
        {
            EnsureLayout();
            storyPanel.SetActive(false);
            loadingPanel.SetActive(false);
            levelSelectPanel.SetActive(false);
            gameOverPanel.SetActive(false);
            pausePanel.SetActive(false);
            controlsPanel.SetActive(true);
            audioPanel.SetActive(false);
        }

        public void HideControlsPopup()
        {
            EnsureLayout();
            controlsPanel.SetActive(false);
            audioPanel.SetActive(false);
            pausePanel.SetActive(true);
        }

        public void ShowAudioPopup()
        {
            EnsureLayout();
            storyPanel.SetActive(false);
            loadingPanel.SetActive(false);
            levelSelectPanel.SetActive(false);
            gameOverPanel.SetActive(false);
            controlsPanel.SetActive(false);
            pausePanel.SetActive(false);
            audioPanel.SetActive(true);
            RefreshAudioSlidersFromManager();
        }

        public void HideAudioPopup()
        {
            EnsureLayout();
            audioPanel.SetActive(false);
            pausePanel.SetActive(true);
        }

        public void ShowGameOver(string message)
        {
            EnsureLayout();
            ApplyPanelRaycastRules();
            ClearTutorialFocus();
            homePanel.SetActive(false);
            storyPanel.SetActive(false);
            loadingPanel.SetActive(false);
            pausePanel.SetActive(false);
            controlsPanel.SetActive(false);
            audioPanel.SetActive(false);
            levelSelectPanel.SetActive(false);
            hudPanel.SetActive(true);
            gameOverPanel.SetActive(true);
            gameOverTitleText.text = "GAME OVER";
            gameOverMessageText.text = message;
        }

        public void SetCameraLockState(bool cameraLocked)
        {
            EnsureLayout();
            if (cameraLockButtonLabel != null)
            {
                cameraLockButtonLabel.text = cameraLocked ? "Camera Lock: ON" : "Camera Lock: OFF";
            }
        }

        public void SetSnapshot(
            string levelName,
            WinConditionType winCondition,
            GameMode gameMode,
            GameState gameState,
            float elapsedTime,
            float timeLimit,
            int remainingDefusers,
            int safeTilesRemaining,
            string message,
            string modeStats)
        {
            EnsureLayout();
            levelText.text = BuildLevelHeader(levelName, gameMode);
            objectiveText.text = BuildObjectiveText(levelName, gameMode, timeLimit, elapsedTime);
            statsText.text = BuildStatsText(elapsedTime, timeLimit, remainingDefusers, safeTilesRemaining, modeStats);
            messageText.text = message;
            nextButton.gameObject.SetActive(gameState == GameState.Won && gameMode != GameMode.Classic && gameMode != GameMode.Tutorial);
        }

        private void EnsureLayout()
        {
            EnsureEventSystem();

            if (!NeedsLayoutRebuild())
            {
                ApplyPanelRaycastRules();
                return;
            }

            ClearChildren(transform);
            ClearReferences();
            BuildLayout(transform);
        }

        private void ApplyPanelRaycastRules()
        {
            SetPanelRaycast(homePanel, false);
            SetPanelRaycast(storyPanel, false);
            SetPanelRaycast(loadingPanel, true);
            SetPanelRaycast(hudPanel, false);
            SetPanelRaycast(pausePanel, true);
            SetPanelRaycast(controlsPanel, true);
            SetPanelRaycast(audioPanel, true);
            SetPanelRaycast(levelSelectPanel, true);
            SetPanelRaycast(gameOverPanel, true);
        }

        private static void SetPanelRaycast(GameObject panel, bool value)
        {
            if (panel == null)
            {
                return;
            }

            Image image = panel.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = value;
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            StandaloneInputModule legacyInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyInputModule != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(legacyInputModule);
                }
                else
                {
                    DestroyImmediate(legacyInputModule);
                }
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

        private void EnsureMainMenuController()
        {
            if (GetComponent<MainMenuController>() == null)
            {
                gameObject.AddComponent<MainMenuController>();
            }
        }

        private void BuildLayout(Transform parent)
        {
            if (!NeedsLayoutRebuild())
            {
                return;
            }

            LoadRuntimeAssets();
            font = Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
            }

            homePanel = CreatePanel(parent, "HomePanel", new Color(0.02f, 0.04f, 0.07f, 0.92f), false);
            storyPanel = CreatePanel(parent, "StoryPanel", new Color(0.02f, 0.04f, 0.07f, 0.98f), false);
            loadingPanel = CreatePanel(parent, "LoadingPanel", new Color(0.02f, 0.04f, 0.07f, 0.98f), true);
            hudPanel = CreatePanel(parent, "HudPanel", new Color(0f, 0f, 0f, 0f), false);
            pausePanel = CreatePanel(parent, "PausePanel", new Color(0.015f, 0.028f, 0.05f, 0.9f), true);
            controlsPanel = CreatePanel(parent, "ControlsPanel", new Color(0.015f, 0.028f, 0.05f, 0.92f), true);
            audioPanel = CreatePanel(parent, "AudioPanel", new Color(0.015f, 0.028f, 0.05f, 0.92f), true);
            levelSelectPanel = CreatePanel(parent, "LevelSelectPanel", new Color(0.02f, 0.04f, 0.07f, 0.94f), false);
            gameOverPanel = CreatePanel(parent, "GameOverPanel", new Color(0.02f, 0.02f, 0.03f, 0.72f), false);

            BuildHomePanel(homePanel.transform);
            BuildStoryPanel(storyPanel.transform);
            BuildLoadingPanel(loadingPanel.transform);
            BuildHudPanel(hudPanel.transform);
            BuildPausePanel(pausePanel.transform);
            BuildControlsPanel(controlsPanel.transform);
            BuildAudioPanel(audioPanel.transform);
            BuildLevelSelectPanel(levelSelectPanel.transform);
            BuildGameOverPanel(gameOverPanel.transform);
            homePanel.SetActive(true);
            storyPanel.SetActive(false);
            loadingPanel.SetActive(false);
            hudPanel.SetActive(false);
            pausePanel.SetActive(false);
            controlsPanel.SetActive(false);
            audioPanel.SetActive(false);
            levelSelectPanel.SetActive(false);
            gameOverPanel.SetActive(false);
            layoutVersion = CurrentLayoutVersion;
        }

        private bool NeedsLayoutRebuild()
        {
            return layoutVersion != CurrentLayoutVersion
                || levelText == null
                || objectiveText == null
                || statsText == null
                || messageText == null
                || homePanel == null
                || storyPanel == null
                || loadingPanel == null
                || hudPanel == null
                || pausePanel == null
                || controlsPanel == null
                || audioPanel == null
                || levelSelectPanel == null
                || gameOverPanel == null
                || homeStatsText == null
                || storyText == null
                || storyProgressText == null
                || loadingText == null
                || loadingHintText == null
                || storyCharacterImage == null
                || pauseTitleText == null
                || levelSelectTitleText == null
                || gameOverTitleText == null
                || gameOverMessageText == null
                || sfxVolumeText == null
                || musicVolumeText == null
                || sfxVolumeSlider == null
                || musicVolumeSlider == null
                || levelSelectEntriesRoot == null
                || replayButton == null
                || homeButton == null
                || settingsButton == null
                || newGameButton == null
                || tutorialButton == null
                || levelButton == null
                || classicButton == null
                || timeButton == null
                || quitButton == null
                || nextButton == null
                || cameraLockButton == null
                || cameraLockButtonLabel == null
                || flagMarkerButtonLabel == null
                || virusMarkerButtonLabel == null
                || tutorialDimPanels == null
                || tutorialDimPanels.Length != 4;
        }

        private void LoadRuntimeAssets()
        {
            if (buttonSprites != null && buttonSprites.Length == 2)
            {
                return;
            }

            buttonSprites = new[]
            {
                LoadSpriteFromTexture("UI/ButtonStyleA"),
                LoadSpriteFromTexture("UI/ButtonStyleB")
            };

            if (storyKidSprites == null || storyKidSprites.Length == 0)
            {
                storyKidSprites = LoadSpritesFromTextures("UI/StoryKidFrames");
            }
        }

        private static Sprite LoadSpriteFromTexture(string resourcesPath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
        }

        private static Sprite[] LoadSpritesFromTextures(string resourcesPath)
        {
            Texture2D[] textures = Resources.LoadAll<Texture2D>(resourcesPath);
            Array.Sort(textures, (left, right) => string.CompareOrdinal(left.name, right.name));

            Sprite[] sprites = new Sprite[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D texture = textures[i];
                sprites[i] = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
            }

            return sprites;
        }

        private void ClearReferences()
        {
            layoutVersion = 0;
            levelText = null;
            objectiveText = null;
            statsText = null;
            messageText = null;
            homePanel = null;
            storyPanel = null;
            loadingPanel = null;
            hudPanel = null;
            pausePanel = null;
            controlsPanel = null;
            audioPanel = null;
            levelSelectPanel = null;
            gameOverPanel = null;
            homeStatsText = null;
            storyText = null;
            storyProgressText = null;
            loadingText = null;
            loadingHintText = null;
            storyCharacterImage = null;
            pauseTitleText = null;
            levelSelectTitleText = null;
            gameOverTitleText = null;
            gameOverMessageText = null;
            sfxVolumeText = null;
            musicVolumeText = null;
            sfxVolumeSlider = null;
            musicVolumeSlider = null;
            levelSelectEntriesRoot = null;
            replayButton = null;
            homeButton = null;
            settingsButton = null;
            newGameButton = null;
            tutorialButton = null;
            levelButton = null;
            classicButton = null;
            timeButton = null;
            quitButton = null;
            tutorialButtonLabel = null;
            levelButtonLabel = null;
            classicButtonLabel = null;
            timeButtonLabel = null;
            nextButton = null;
            cameraLockButton = null;
            cameraLockButtonLabel = null;
            flagMarkerButtonLabel = null;
            virusMarkerButtonLabel = null;
            tutorialDimPanels = null;
            tutorialFocusTarget = TutorialFocusTarget.None;
            tutorialFocusActive = false;
            tutorialFocusUsesWorld = false;
            tutorialBlocksInteraction = false;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void BuildHomePanel(Transform parent)
        {
            Text title = CreateText(parent, "Title", 96, TextAnchor.UpperCenter, new Vector2(0f, -115f), new Vector2(1260f, 120f));
            title.text = "Cyber Minefield";
            title.color = new Color32(0xF3, 0xF1, 0xB7, 0xFF);
            title.gameObject.AddComponent<UiPulse>().Configure(title, new Color32(0xF3, 0xF1, 0xB7, 0xFF), new Color(0.74f, 0.98f, 1f), 0.055f, 1.15f);

            CreatePixelStar(parent, "TitleStarLeft", new Vector2(-610f, -159f), 0.82f, 0.1f);
            CreatePixelStar(parent, "TitleStarRight", new Vector2(610f, -155f), 0.82f, 0.65f);
            CreatePixelStar(parent, "TitleStarSmallLeft", new Vector2(-455f, -105f), 0.48f, 0.95f);
            CreatePixelStar(parent, "TitleStarSmallRight", new Vector2(455f, -103f), 0.48f, 1.25f);

            tutorialButton = CreateButton(parent, "TutorialButton", "Tutorial", new Vector2(0f, -230f), () => WithGameManager(manager => manager.StartTutorial()), TextAnchor.UpperCenter, new Vector2(480f, 104f), 32, 1);
            newGameButton = CreateButton(parent, "NewGameButton", "New Game", new Vector2(0f, -332f), () => WithGameManager(manager => manager.StartNewGame()), TextAnchor.UpperCenter, new Vector2(480f, 104f), 32, 1);
            levelButton = CreateButton(parent, "LevelButton", "Level", new Vector2(0f, -434f), () => WithGameManager(manager => manager.ShowLevelSelect()), TextAnchor.UpperCenter, new Vector2(480f, 104f), 32, 1);
            classicButton = CreateButton(parent, "ClassicButton", "Classic", new Vector2(0f, -536f), () => WithGameManager(manager => manager.StartClassic()), TextAnchor.UpperCenter, new Vector2(480f, 104f), 32, 1);
            timeButton = CreateButton(parent, "TimeButton", "Time", new Vector2(0f, -638f), () => WithGameManager(manager => manager.StartTimeAttack()), TextAnchor.UpperCenter, new Vector2(480f, 104f), 32, 1);
            CreateButton(parent, "HomeSettingsButton", "Settings", new Vector2(0f, -740f), () => WithGameManager(manager => manager.TogglePauseFromUi()), TextAnchor.UpperCenter, new Vector2(480f, 104f), 32, 1);
            quitButton = CreateButton(parent, "QuitButton", "Quit", new Vector2(0f, -842f), () => WithGameManager(manager => manager.QuitGame()), TextAnchor.UpperCenter, new Vector2(480f, 104f), 32, 1);
            tutorialButtonLabel = tutorialButton.GetComponentInChildren<Text>();
            levelButtonLabel = levelButton.GetComponentInChildren<Text>();
            classicButtonLabel = classicButton.GetComponentInChildren<Text>();
            timeButtonLabel = timeButton.GetComponentInChildren<Text>();

            homeStatsText = CreateText(parent, "HomeStats", 22, TextAnchor.LowerCenter, new Vector2(0f, 52f), new Vector2(900f, 56f));
            homeStatsText.text = string.Empty;
        }

        private void BuildStoryPanel(Transform parent)
        {
            LoadRuntimeAssets();
            storyCharacterImage = CreateImage(parent, "StoryKid", null, new Vector2(0f, 150f), new Vector2(430f, 640f), TextAnchor.MiddleCenter);
            storyCharacterImage.preserveAspect = true;
            storyCharacterImage.color = Color.white;

            Image bubble = CreateImage(parent, "StoryBubble", LoadSpriteFromTexture("UI/ButtonWideA"), new Vector2(0f, 58f), new Vector2(1320f, 300f), TextAnchor.LowerCenter);
            bubble.preserveAspect = false;
            bubble.color = Color.white;

            storyText = CreateText(parent, "StoryText", 31, TextAnchor.LowerCenter, new Vector2(16f, 160f), new Vector2(960f, 146f));
            storyText.color = new Color(0.08f, 0.08f, 0.07f);
            storyText.alignment = TextAnchor.MiddleCenter;
            storyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            storyText.verticalOverflow = VerticalWrapMode.Truncate;
            storyText.resizeTextForBestFit = true;
            storyText.resizeTextMinSize = 22;
            storyText.resizeTextMaxSize = 31;
            storyText.lineSpacing = 0.9f;

            storyProgressText = CreateText(parent, "StoryProgress", 22, TextAnchor.LowerCenter, new Vector2(0f, 30f), new Vector2(920f, 42f));
            storyProgressText.color = new Color(0.78f, 0.96f, 1f);
            ApplyStoryFrame();
        }

        private void BuildLoadingPanel(Transform parent)
        {
            Text title = CreateText(parent, "LoadingTitle", 78, TextAnchor.MiddleCenter, new Vector2(0f, 58f), new Vector2(1240f, 110f));
            title.text = "Loading";
            title.color = new Color32(0xF3, 0xF1, 0xB7, 0xFF);
            title.gameObject.AddComponent<UiPulse>().Configure(title, new Color32(0xF3, 0xF1, 0xB7, 0xFF), new Color(0.74f, 0.98f, 1f), 0.045f, 1.1f);
            loadingText = title;

            loadingHintText = CreateText(parent, "LoadingHint", 24, TextAnchor.MiddleCenter, new Vector2(0f, -42f), new Vector2(960f, 60f));
            loadingHintText.text = "Preparing safe tiles and virus clues";
            loadingHintText.color = new Color(0.78f, 0.96f, 1f);

            CreatePixelStar(parent, "LoadingStarLeft", new Vector2(-265f, -365f), 0.44f, 0.1f);
            CreatePixelStar(parent, "LoadingStarRight", new Vector2(265f, -365f), 0.44f, 0.7f);
        }

        private void AnimateLoadingText(bool force = false)
        {
            if (loadingPanel == null || loadingText == null || (!force && !loadingPanel.activeInHierarchy))
            {
                return;
            }

            int dotCount = Mathf.FloorToInt(Time.unscaledTime * 2.6f) % 4;
            loadingText.text = loadingBaseText + new string('.', dotCount);
        }

        private void SetHomeProgress(int unlockedCampaignLevel, int levelCount)
        {
            bool hasLevelProgress = unlockedCampaignLevel >= 1;
            SetHomeButtonState(tutorialButton, tutorialButtonLabel, hasLevelProgress, "Tutorial");
            SetHomeButtonState(levelButton, levelButtonLabel, hasLevelProgress, hasLevelProgress ? BuildLevelButtonLabel(unlockedCampaignLevel, levelCount) : "Level");
            SetHomeButtonState(classicButton, classicButtonLabel, hasLevelProgress, "Classic");
            SetHomeButtonState(timeButton, timeButtonLabel, hasLevelProgress, "Time");

            if (newGameButton != null)
            {
                newGameButton.interactable = true;
            }
        }

        private static void SetHomeButtonState(Button button, Text label, bool interactable, string text)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }

            if (label != null)
            {
                label.text = text;
            }
        }

        private static string BuildLevelButtonLabel(int unlockedCampaignLevel, int levelCount)
        {
            if (unlockedCampaignLevel < 1)
            {
                return "Level";
            }

            if (levelCount > 0 && unlockedCampaignLevel >= levelCount)
            {
                return "Levels Clear";
            }

            return $"Level {unlockedCampaignLevel}";
        }

        private void CreatePixelStar(Transform parent, string name, Vector2 anchoredPosition, float scale, float phase)
        {
            GameObject star = new GameObject(name);
            star.transform.SetParent(parent, false);

            RectTransform rect = star.AddComponent<RectTransform>();
            SetAnchors(rect, TextAnchor.UpperCenter);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(84f, 84f) * scale;

            AddStarBlock(star.transform, "Core", Vector2.zero, new Vector2(18f, 18f) * scale, new Color(1f, 0.92f, 0.08f));
            AddStarBlock(star.transform, "Horizontal", Vector2.zero, new Vector2(54f, 14f) * scale, new Color(1f, 0.92f, 0.08f));
            AddStarBlock(star.transform, "Vertical", Vector2.zero, new Vector2(14f, 54f) * scale, new Color(1f, 0.92f, 0.08f));
            AddStarBlock(star.transform, "TopPixel", new Vector2(0f, 34f) * scale, new Vector2(14f, 14f) * scale, new Color(1f, 0.92f, 0.08f));
            AddStarBlock(star.transform, "BottomPixel", new Vector2(0f, -34f) * scale, new Vector2(14f, 14f) * scale, new Color(1f, 0.92f, 0.08f));
            AddStarBlock(star.transform, "LeftShadow", new Vector2(-36f, 0f) * scale, new Vector2(16f, 16f) * scale, new Color(0.1f, 0.1f, 0.09f, 0.75f));
            AddStarBlock(star.transform, "RightShadow", new Vector2(36f, 0f) * scale, new Vector2(16f, 16f) * scale, new Color(0.1f, 0.1f, 0.09f, 0.75f));

            star.AddComponent<PixelStarTwinkle>().Configure(phase);
        }

        private static void AddStarBlock(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject block = new GameObject(name);
            block.transform.SetParent(parent, false);

            Image image = block.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            RectTransform rect = block.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private void BuildHudPanel(Transform parent)
        {
            levelText = CreateText(parent, "LevelText", 28, TextAnchor.UpperLeft, new Vector2(22f, -16f), new Vector2(760f, 34f));
            objectiveText = CreateText(parent, "ObjectiveText", 20, TextAnchor.UpperLeft, new Vector2(22f, -54f), new Vector2(940f, 30f));
            statsText = CreateText(parent, "StatsText", 20, TextAnchor.UpperLeft, new Vector2(22f, -86f), new Vector2(980f, 30f));
            messageText = CreateText(parent, "MessageText", 22, TextAnchor.LowerCenter, new Vector2(0f, 30f), new Vector2(980f, 42f));

            replayButton = CreateIconButton(parent, "ReplayButton", "Replay", new Vector2(-164f, -24f), () => WithGameManager(manager => manager.RestartLevel()));
            homeButton = CreateIconButton(parent, "HomeButton", "Home", new Vector2(-96f, -24f), () => WithGameManager(manager => manager.ShowHomePage()));
            settingsButton = CreateIconButton(parent, "SettingsButton", "Settings", new Vector2(-28f, -24f), () => WithGameManager(manager => manager.TogglePauseFromUi()));
            Button flagButton = CreateButton(parent, "MarkerFlagButton", "Flag", new Vector2(-96f, -24f), () => SetMarkerStyle(DefuserMarkerStyle.Flag), TextAnchor.UpperCenter, new Vector2(160f, 52f), 18, 1);
            Button virusButton = CreateButton(parent, "MarkerVirusButton", "Virus", new Vector2(96f, -24f), () => SetMarkerStyle(DefuserMarkerStyle.Virus), TextAnchor.UpperCenter, new Vector2(180f, 52f), 18, 1);
            flagMarkerButtonLabel = flagButton.GetComponentInChildren<Text>();
            virusMarkerButtonLabel = virusButton.GetComponentInChildren<Text>();
            RefreshMarkerSelectorLabels();
            nextButton = CreateButton(parent, "NextButton", "Next", new Vector2(0f, 76f), () => WithGameManager(manager => manager.ContinueAfterWin()), TextAnchor.LowerCenter, new Vector2(220f, 70f), 24, 1);
            nextButton.gameObject.SetActive(false);
            CreateTutorialSpotlight(parent);
            messageText.transform.SetAsLastSibling();
        }

        public void SetTutorialFocus(TutorialFocusTarget target, bool blocksInteraction)
        {
            tutorialFocusTarget = target;
            tutorialFocusUsesWorld = false;
            tutorialFocusActive = target != TutorialFocusTarget.None;
            tutorialBlocksInteraction = blocksInteraction;
            SetTutorialDimVisible(tutorialFocusActive);
            UpdateTutorialSpotlight();
        }

        public void SetTutorialWorldFocus(TutorialFocusTarget target, Vector3 worldPosition, Vector2 screenSize, bool blocksInteraction)
        {
            tutorialFocusTarget = target;
            tutorialFocusWorldPosition = worldPosition;
            tutorialWorldFocusSize = screenSize == Vector2.zero ? new Vector2(180f, 150f) : screenSize;
            tutorialFocusUsesWorld = true;
            tutorialFocusActive = target != TutorialFocusTarget.None;
            tutorialBlocksInteraction = blocksInteraction;
            SetTutorialDimVisible(tutorialFocusActive);
            UpdateTutorialSpotlight();
        }

        public void ClearTutorialFocus()
        {
            tutorialFocusTarget = TutorialFocusTarget.None;
            tutorialFocusActive = false;
            tutorialFocusUsesWorld = false;
            tutorialBlocksInteraction = false;
            SetTutorialDimVisible(false);
        }

        private void CreateTutorialSpotlight(Transform parent)
        {
            tutorialDimPanels = new Image[4];
            Color dimColor = new Color(0f, 0f, 0f, 0.72f);
            for (int i = 0; i < tutorialDimPanels.Length; i++)
            {
                GameObject panel = new GameObject($"TutorialDim_{i}");
                panel.transform.SetParent(parent, false);
                Image image = panel.AddComponent<Image>();
                image.color = dimColor;
                image.raycastTarget = false;

                RectTransform rect = image.GetComponent<RectTransform>();
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                tutorialDimPanels[i] = image;
                panel.SetActive(false);
            }
        }

        private void UpdateTutorialSpotlight()
        {
            if (!tutorialFocusActive || tutorialDimPanels == null || tutorialDimPanels.Length != 4)
            {
                return;
            }

            Rect focusRect;
            if (tutorialFocusUsesWorld)
            {
                if (!TryGetWorldFocusScreenRect(out focusRect))
                {
                    SetTutorialDimVisible(false);
                    return;
                }
            }
            else if (!TryGetUiFocusScreenRect(out focusRect))
            {
                SetTutorialDimVisible(false);
                return;
            }

            SetTutorialDimVisible(true);
            ApplyTutorialSpotlightRect(focusRect);
            if (messageText != null)
            {
                messageText.transform.SetAsLastSibling();
            }
        }

        private bool TryGetWorldFocusScreenRect(out Rect screenRect)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                screenRect = default;
                return false;
            }

            Vector3 screenPoint = camera.WorldToScreenPoint(tutorialFocusWorldPosition);
            if (screenPoint.z < 0.01f)
            {
                screenRect = default;
                return false;
            }

            Vector2 size = tutorialWorldFocusSize;
            screenRect = new Rect(
                screenPoint.x - size.x * 0.5f,
                screenPoint.y - size.y * 0.5f,
                size.x,
                size.y);
            return true;
        }

        private bool TryGetUiFocusScreenRect(out Rect screenRect)
        {
            if (tutorialFocusTarget == TutorialFocusTarget.MarkerSelector)
            {
                RectTransform flagRect = flagMarkerButtonLabel != null ? flagMarkerButtonLabel.transform.parent as RectTransform : null;
                RectTransform virusRect = virusMarkerButtonLabel != null ? virusMarkerButtonLabel.transform.parent as RectTransform : null;
                return TryGetCombinedScreenRect(flagRect, virusRect, 18f, out screenRect);
            }

            RectTransform targetRect = GetTutorialFocusRect();
            return TryGetScreenRect(targetRect, 18f, out screenRect);
        }

        private RectTransform GetTutorialFocusRect()
        {
            switch (tutorialFocusTarget)
            {
                case TutorialFocusTarget.DefuserStats:
                case TutorialFocusTarget.TimerStats:
                    return statsText != null ? statsText.transform as RectTransform : null;
                case TutorialFocusTarget.SettingsButton:
                    return settingsButton != null ? settingsButton.transform as RectTransform : null;
                case TutorialFocusTarget.RestartButton:
                    return replayButton != null ? replayButton.transform as RectTransform : null;
                case TutorialFocusTarget.HomeButton:
                    return homeButton != null ? homeButton.transform as RectTransform : null;
                default:
                    return null;
            }
        }

        private static bool TryGetCombinedScreenRect(RectTransform first, RectTransform second, float padding, out Rect screenRect)
        {
            if (!TryGetScreenRect(first, padding, out Rect firstRect))
            {
                screenRect = default;
                return TryGetScreenRect(second, padding, out screenRect);
            }

            if (!TryGetScreenRect(second, padding, out Rect secondRect))
            {
                screenRect = firstRect;
                return true;
            }

            float xMin = Mathf.Min(firstRect.xMin, secondRect.xMin);
            float yMin = Mathf.Min(firstRect.yMin, secondRect.yMin);
            float xMax = Mathf.Max(firstRect.xMax, secondRect.xMax);
            float yMax = Mathf.Max(firstRect.yMax, secondRect.yMax);
            screenRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        private static bool TryGetScreenRect(RectTransform target, float padding, out Rect screenRect)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                screenRect = default;
                return false;
            }

            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            screenRect = Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
            return true;
        }

        private void ApplyTutorialSpotlightRect(Rect screenRect)
        {
            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);

            float xMin = Mathf.Clamp01(screenRect.xMin / screenWidth);
            float xMax = Mathf.Clamp01(screenRect.xMax / screenWidth);
            float yMin = Mathf.Clamp01(screenRect.yMin / screenHeight);
            float yMax = Mathf.Clamp01(screenRect.yMax / screenHeight);

            SetDimAnchors(tutorialDimPanels[0], new Vector2(0f, yMax), Vector2.one);
            SetDimAnchors(tutorialDimPanels[1], Vector2.zero, new Vector2(1f, yMin));
            SetDimAnchors(tutorialDimPanels[2], new Vector2(0f, yMin), new Vector2(xMin, yMax));
            SetDimAnchors(tutorialDimPanels[3], new Vector2(xMax, yMin), new Vector2(1f, yMax));
        }

        private static void SetDimAnchors(Image image, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (image == null)
            {
                return;
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetTutorialDimVisible(bool visible)
        {
            if (tutorialDimPanels == null)
            {
                return;
            }

            foreach (Image image in tutorialDimPanels)
            {
                if (image != null)
                {
                    image.gameObject.SetActive(visible);
                }
            }
        }

        private void BuildPausePanel(Transform parent)
        {
            Vector2 settingsButtonSize = new Vector2(520f, 92f);

            pauseTitleText = CreateText(parent, "PauseTitle", 42, TextAnchor.MiddleCenter, new Vector2(0f, 250f), new Vector2(700f, 62f));
            pauseTitleText.text = "Settings";
            CreateButton(parent, "ResumeButton", "Resume", new Vector2(0f, 165f), () => WithGameManager(manager => manager.ResumeFromUi()), TextAnchor.MiddleCenter, settingsButtonSize, 26, 1);
            cameraLockButton = CreateButton(parent, "CameraLockButton", "Camera Lock: OFF", new Vector2(0f, 65f), () => WithGameManager(manager => manager.ToggleCameraLockFromUi()), TextAnchor.MiddleCenter, settingsButtonSize, 26, 1);
            cameraLockButtonLabel = cameraLockButton.GetComponentInChildren<Text>();
            CreateButton(parent, "ControlsButton", "Controls", new Vector2(0f, -35f), () => ShowControlsPopup(), TextAnchor.MiddleCenter, settingsButtonSize, 26, 1);
            CreateButton(parent, "AudioButton", "Audio", new Vector2(0f, -135f), () => ShowAudioPopup(), TextAnchor.MiddleCenter, settingsButtonSize, 26, 1);
            CreateButton(parent, "PauseReplayButton", "Retry", new Vector2(0f, -235f), () => WithGameManager(manager => manager.RestartLevel()), TextAnchor.MiddleCenter, settingsButtonSize, 26, 1);
            CreateButton(parent, "PauseHomeButton", "Home", new Vector2(0f, -335f), () => WithGameManager(manager => manager.ShowHomePage()), TextAnchor.MiddleCenter, settingsButtonSize, 26, 1);
        }

        private void BuildControlsPanel(Transform parent)
        {
            Image popupImage = CreateImage(parent, "ControlsPopupImage", LoadSpriteFromTexture("UI/ControlsPopup"), Vector2.zero, new Vector2(1060f, 814f), TextAnchor.MiddleCenter);
            popupImage.preserveAspect = true;
            popupImage.color = Color.white;
            CreateButton(parent, "CloseControlsButton", "Close", new Vector2(0f, -440f), () => HideControlsPopup(), TextAnchor.MiddleCenter, new Vector2(320f, 78f), 24, 1);
        }

        private void BuildAudioPanel(Transform parent)
        {
            Text title = CreateText(parent, "AudioTitle", 48, TextAnchor.MiddleCenter, new Vector2(0f, 230f), new Vector2(760f, 70f));
            title.text = "Audio";

            sfxVolumeText = CreateText(parent, "SfxVolumeLabel", 28, TextAnchor.MiddleCenter, new Vector2(0f, 118f), new Vector2(760f, 46f));
            musicVolumeText = CreateText(parent, "MusicVolumeLabel", 28, TextAnchor.MiddleCenter, new Vector2(0f, -36f), new Vector2(760f, 46f));

            sfxVolumeSlider = CreateSlider(parent, "SfxVolumeSlider", new Vector2(0f, 70f), value =>
            {
                AudioManager manager = FindAnyObjectByType<AudioManager>();
                manager?.SetSfxVolume(value);
                UpdateAudioLabel(sfxVolumeText, "SFX", value);
            });

            musicVolumeSlider = CreateSlider(parent, "MusicVolumeSlider", new Vector2(0f, -84f), value =>
            {
                AudioManager manager = FindAnyObjectByType<AudioManager>();
                manager?.SetMusicVolume(value);
                UpdateAudioLabel(musicVolumeText, "Music", value);
            });

            CreateButton(parent, "CloseAudioButton", "Close", new Vector2(0f, -238f), () => HideAudioPopup(), TextAnchor.MiddleCenter, new Vector2(360f, 86f), 26, 1);
            RefreshAudioSlidersFromManager();
        }

        private Slider CreateSlider(Transform parent, string name, Vector2 anchoredPosition, UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            GameObject sliderObject = new GameObject(name);
            sliderObject.transform.SetParent(parent, false);

            RectTransform rect = sliderObject.AddComponent<RectTransform>();
            SetAnchors(rect, TextAnchor.MiddleCenter);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(600f, 46f);

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            Image background = AddSliderImage(sliderObject.transform, "Background", new Vector2(0f, 0f), new Vector2(600f, 18f), new Color(0.05f, 0.72f, 0.82f, 1f));

            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = new Vector2(20f, 0f);
            handleAreaRect.offsetMax = new Vector2(-20f, 0f);

            Image handle = AddSliderImage(handleArea.transform, "Handle", new Vector2(0f, 0f), new Vector2(34f, 46f), new Color32(0xF3, 0xF1, 0xB7, 0xFF));

            slider.targetGraphic = handle;
            slider.fillRect = null;
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(onValueChanged);
            background.raycastTarget = true;
            return slider;
        }

        private static Image AddSliderImage(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;

            RectTransform rect = image.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return image;
        }

        private void RefreshAudioSlidersFromManager()
        {
            AudioManager manager = FindAnyObjectByType<AudioManager>();
            float sfxValue = manager != null ? manager.SfxVolume : 1f;
            float musicValue = manager != null ? manager.MusicVolume : 0.65f;

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.SetValueWithoutNotify(sfxValue);
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.SetValueWithoutNotify(musicValue);
            }

            UpdateAudioLabel(sfxVolumeText, "SFX", sfxValue);
            UpdateAudioLabel(musicVolumeText, "Music", musicValue);
        }

        private static void UpdateAudioLabel(Text label, string name, float value)
        {
            if (label != null)
            {
                label.text = $"{name}: {Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}";
            }
        }

        private void BuildLevelSelectPanel(Transform parent)
        {
            levelSelectTitleText = CreateText(parent, "LevelSelectTitle", 64, TextAnchor.UpperCenter, new Vector2(0f, -94f), new Vector2(1120f, 92f));
            levelSelectTitleText.text = "Select Level";
            levelSelectTitleText.color = new Color32(0xF3, 0xF1, 0xB7, 0xFF);

            GameObject entriesObject = new GameObject("LevelEntries");
            entriesObject.transform.SetParent(parent, false);
            levelSelectEntriesRoot = entriesObject.AddComponent<RectTransform>();
            SetAnchors(levelSelectEntriesRoot, TextAnchor.MiddleCenter);
            levelSelectEntriesRoot.anchoredPosition = new Vector2(0f, -24f);
            levelSelectEntriesRoot.sizeDelta = new Vector2(920f, 520f);

            CreateButton(parent, "LevelSelectHomeButton", "Home", new Vector2(0f, 92f), () => WithGameManager(manager => manager.ShowHomePage()), TextAnchor.LowerCenter, new Vector2(360f, 86f), 26, 1);
        }

        private void BuildGameOverPanel(Transform parent)
        {
            gameOverTitleText = CreateText(parent, "GameOverTitle", 96, TextAnchor.UpperCenter, new Vector2(0f, -132f), new Vector2(1280f, 124f));
            gameOverTitleText.text = "GAME OVER";
            gameOverTitleText.color = new Color32(0xF3, 0xF1, 0xB7, 0xFF);
            gameOverTitleText.gameObject.AddComponent<UiPulse>().Configure(gameOverTitleText, new Color32(0xF3, 0xF1, 0xB7, 0xFF), new Color(0.74f, 0.98f, 1f), 0.055f, 1.15f);

            gameOverMessageText = CreateText(parent, "GameOverMessage", 26, TextAnchor.UpperCenter, new Vector2(0f, -268f), new Vector2(1180f, 84f));
            gameOverMessageText.text = string.Empty;
            gameOverMessageText.color = new Color(0.78f, 0.96f, 1f);
            gameOverMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            gameOverMessageText.verticalOverflow = VerticalWrapMode.Truncate;

            CreateButton(parent, "GameOverReplayButton", "Restart", new Vector2(-250f, -98f), () => WithGameManager(manager => manager.RestartLevel()), TextAnchor.MiddleCenter, new Vector2(420f, 100f), 30, 1);
            CreateButton(parent, "GameOverHomeButton", "Home", new Vector2(250f, -98f), () => WithGameManager(manager => manager.ShowHomePage()), TextAnchor.MiddleCenter, new Vector2(420f, 100f), 30, 1);
        }

        private void PopulateLevelSelect(int unlockedCampaignLevel, int levelCount)
        {
            if (levelSelectEntriesRoot == null)
            {
                return;
            }

            ClearChildren(levelSelectEntriesRoot);

            int campaignLevelCount = Mathf.Max(0, levelCount - 1);
            const int columns = 4;
            Vector2 buttonSize = new Vector2(320f, 92f);
            float xSpacing = 360f;
            float ySpacing = 118f;
            int rows = Mathf.Max(1, Mathf.CeilToInt(campaignLevelCount / (float)columns));
            float startY = (rows - 1) * ySpacing * 0.5f;
            float startX = -(Mathf.Min(columns, Mathf.Max(1, campaignLevelCount)) - 1) * xSpacing * 0.5f;

            for (int displayLevel = 1; displayLevel <= campaignLevelCount; displayLevel++)
            {
                int capturedLevelIndex = displayLevel;
                int zeroBased = displayLevel - 1;
                int column = zeroBased % columns;
                int row = zeroBased / columns;
                Vector2 position = new Vector2(startX + column * xSpacing, startY - row * ySpacing);
                Button button = CreateButton(
                    levelSelectEntriesRoot,
                    $"CampaignLevelButton_{capturedLevelIndex}",
                    string.Empty,
                    position,
                    () => WithGameManager(manager => manager.StartCampaignLevelFromMenu(capturedLevelIndex)),
                    TextAnchor.MiddleCenter,
                    buttonSize,
                    26,
                    1);

                button.interactable = capturedLevelIndex <= unlockedCampaignLevel;

                Text label = CreateText(
                    levelSelectEntriesRoot,
                    $"CampaignLevelLabel_{capturedLevelIndex}",
                    26,
                    TextAnchor.MiddleCenter,
                    new Vector2(position.x, position.y - 8f),
                    new Vector2(230f, 42f));
                label.text = $"Level {displayLevel}";
                label.color = button.interactable ? new Color(0.09f, 0.09f, 0.08f) : new Color(0.52f, 0.52f, 0.46f);
                label.raycastTarget = false;
            }
        }

        private void WithGameManager(System.Action<GameManager> action)
        {
            lastManualClickFrame = Time.frameCount;
            lastManualClickTime = Time.unscaledTime;

            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            if (gameManager == null)
            {
                Debug.LogError("UI button clicked, but no GameManager exists in the scene.", this);
                return;
            }

            Debug.Log("UI button action received.", this);
            action.Invoke(gameManager);
        }

        private void InvokeButtonUnderMouseIfNeeded()
        {
            if ((!WasLeftMousePressed() && !WasLeftMouseReleased()) || lastManualClickFrame == Time.frameCount)
            {
                return;
            }

            InvokeButtonUnderPosition(GetMousePosition());
        }

        private bool InvokeButtonUnderPosition(Vector2 mousePosition)
        {
            if (Time.unscaledTime - lastManualClickTime < 0.12f)
            {
                return false;
            }

            if (tutorialFocusActive && tutorialBlocksInteraction)
            {
                lastManualClickFrame = Time.frameCount;
                lastManualClickTime = Time.unscaledTime;
                return true;
            }

            RefreshRuntimeButtons();
            RectTransform interactionScope = GetActiveInteractionScope();

            if (InvokeButtonViaEventSystem(mousePosition, interactionScope))
            {
                return true;
            }

            for (int i = runtimeButtons.Count - 1; i >= 0; i--)
            {
                Button button = runtimeButtons[i];
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                {
                    continue;
                }

                RectTransform rectTransform = button.transform as RectTransform;
                if (rectTransform == null)
                {
                    continue;
                }

                if (interactionScope != null && !button.transform.IsChildOf(interactionScope))
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, mousePosition, null))
                {
                    InvokeRuntimeButton(button);
                    return true;
                }
            }

            if (interactionScope != null && RectTransformUtility.RectangleContainsScreenPoint(interactionScope, mousePosition, null))
            {
                return true;
            }

            return false;
        }

        private bool InvokeButtonViaEventSystem(Vector2 mousePosition, RectTransform interactionScope)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = mousePosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (RaycastResult result in results)
            {
                Button button = result.gameObject.GetComponentInParent<Button>();
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                {
                    continue;
                }

                if (interactionScope != null && !button.transform.IsChildOf(interactionScope))
                {
                    continue;
                }

                InvokeRuntimeButton(button);
                return true;
            }

            if (interactionScope != null && RectTransformUtility.RectangleContainsScreenPoint(interactionScope, mousePosition, null))
            {
                return true;
            }

            return false;
        }

        private RectTransform GetActiveInteractionScope()
        {
            if (audioPanel != null && audioPanel.activeInHierarchy)
            {
                return audioPanel.transform as RectTransform;
            }

            if (controlsPanel != null && controlsPanel.activeInHierarchy)
            {
                return controlsPanel.transform as RectTransform;
            }

            if (pausePanel != null && pausePanel.activeInHierarchy)
            {
                return pausePanel.transform as RectTransform;
            }

            if (gameOverPanel != null && gameOverPanel.activeInHierarchy)
            {
                return gameOverPanel.transform as RectTransform;
            }

            if (levelSelectPanel != null && levelSelectPanel.activeInHierarchy)
            {
                return levelSelectPanel.transform as RectTransform;
            }

            return null;
        }

        private void InvokeRuntimeButton(Button button)
        {
            lastManualClickFrame = Time.frameCount;
            lastManualClickTime = Time.unscaledTime;
            UiButtonClickSound.Play();

            if (TryInvokeNamedButtonAction(button))
            {
                return;
            }

            button.onClick.Invoke();
        }

        private bool TryInvokeNamedButtonAction(Button button)
        {
            if (button == null)
            {
                return false;
            }

            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            if (gameManager == null)
            {
                return false;
            }

            switch (button.gameObject.name)
            {
                case "TutorialButton":
                    gameManager.StartTutorial();
                    return true;
                case "NewGameButton":
                    gameManager.StartNewGame();
                    return true;
                case "LevelButton":
                    gameManager.ShowLevelSelect();
                    return true;
                case "ClassicButton":
                    gameManager.StartClassic();
                    return true;
                case "TimeButton":
                    gameManager.StartTimeAttack();
                    return true;
                case "HomeSettingsButton":
                    gameManager.TogglePauseFromUi();
                    return true;
                case "MarkerFlagButton":
                    SetMarkerStyle(DefuserMarkerStyle.Flag);
                    return true;
                case "MarkerVirusButton":
                    SetMarkerStyle(DefuserMarkerStyle.Virus);
                    return true;
                case "ReplayButton":
                case "PauseReplayButton":
                case "GameOverReplayButton":
                    gameManager.RestartLevel();
                    return true;
                case "HomeButton":
                case "PauseHomeButton":
                case "LevelSelectHomeButton":
                case "GameOverHomeButton":
                    gameManager.ShowHomePage();
                    return true;
                case "SettingsButton":
                    gameManager.TogglePauseFromUi();
                    return true;
                case "ControlsButton":
                    ShowControlsPopup();
                    return true;
                case "CloseControlsButton":
                    HideControlsPopup();
                    return true;
                case "AudioButton":
                    ShowAudioPopup();
                    return true;
                case "CloseAudioButton":
                    HideAudioPopup();
                    return true;
                case "ResumeButton":
                    gameManager.ResumeFromUi();
                    return true;
                case "CameraLockButton":
                    gameManager.ToggleCameraLockFromUi();
                    return true;
                case "NextButton":
                    gameManager.ContinueAfterWin();
                    return true;
                default:
                    if (button.gameObject.name.StartsWith("CampaignLevelButton_", StringComparison.Ordinal)
                        && int.TryParse(button.gameObject.name.Substring("CampaignLevelButton_".Length), out int levelIndex))
                    {
                        gameManager.StartCampaignLevelFromMenu(levelIndex);
                        return true;
                    }

                    return false;
            }
        }

        private void SetMarkerStyle(DefuserMarkerStyle markerStyle)
        {
            DefuserMarkerStyle currentStyle = TileNode.SelectedMarkerStyle;
            TileNode.SetSelectedMarkerStyle(markerStyle);
            RefreshMarkerSelectorLabels();
            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            gameManager?.NotifyMarkerStyleChanged(markerStyle);
            Debug.Log($"Marker style changed from {currentStyle} to {markerStyle}.", this);
        }

        private void RefreshMarkerSelectorLabels()
        {
            if (flagMarkerButtonLabel != null)
            {
                flagMarkerButtonLabel.text = TileNode.SelectedMarkerStyle == DefuserMarkerStyle.Flag ? "Flag*" : "Flag";
            }

            if (virusMarkerButtonLabel != null)
            {
                virusMarkerButtonLabel.text = TileNode.SelectedMarkerStyle == DefuserMarkerStyle.Virus ? "Virus*" : "Virus";
            }
        }

        private void AnimateStoryCharacter()
        {
            if (storyPanel == null || !storyPanel.activeInHierarchy || storyKidSprites == null || storyKidSprites.Length == 0)
            {
                return;
            }

            storyFrameTimer += Time.unscaledDeltaTime;
            if (storyFrameTimer < 0.075f)
            {
                return;
            }

            storyFrameTimer = 0f;
            storyFrameIndex = (storyFrameIndex + 1) % storyKidSprites.Length;
            ApplyStoryFrame();
        }

        private void ApplyStoryFrame()
        {
            if (storyCharacterImage == null || storyKidSprites == null || storyKidSprites.Length == 0)
            {
                return;
            }

            storyCharacterImage.sprite = storyKidSprites[Mathf.Clamp(storyFrameIndex, 0, storyKidSprites.Length - 1)];
        }

        private void RefreshRuntimeButtons()
        {
            Button[] sceneButtons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (Button button in sceneButtons)
            {
                if (button != null && !runtimeButtons.Contains(button))
                {
                    runtimeButtons.Add(button);
                }
            }
        }

        private static bool WasLeftMousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private static bool WasLeftMouseReleased()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.wasReleasedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(0);
#else
            return false;
#endif
        }

        private static Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                return mouse.position.ReadValue();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        private static Vector2 GuiToScreenPoint(Vector2 guiPosition)
        {
            return new Vector2(guiPosition.x, Screen.height - guiPosition.y);
        }

        private GameObject CreatePanel(Transform parent, string name, Color color, bool blocksRaycasts)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            Image image = panel.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blocksRaycasts;

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return panel;
        }

        private Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor anchor)
        {
            GameObject imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;

            RectTransform rect = image.GetComponent<RectTransform>();
            SetAnchors(rect, anchor);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            return image;
        }

        private Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction action,
            TextAnchor anchor = TextAnchor.UpperCenter,
            Vector2 sizeDelta = default,
            int labelSize = 22,
            int styleIndex = 0)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.AddComponent<Image>();
            Sprite sprite = GetButtonSprite(styleIndex);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
            }
            else
            {
                image.color = new Color(0.9f, 0.88f, 0.74f, 1f);
            }

            Button button = buttonObject.AddComponent<Button>();
            buttonObject.AddComponent<UiButtonClickSound>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.52f, 0.52f, 0.46f, 1f);
            colors.pressedColor = new Color(0.34f, 0.34f, 0.31f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.24f, 0.24f, 0.22f, 0.7f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            RectTransform rect = button.GetComponent<RectTransform>();
            SetAnchors(rect, anchor);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta == default ? new Vector2(220f, 64f) : sizeDelta;

            Text text = CreateText(buttonObject.transform, "Label", labelSize, TextAnchor.MiddleCenter, new Vector2(0f, -8f), new Vector2(rect.sizeDelta.x * 0.62f, rect.sizeDelta.y * 0.36f));
            text.text = label;
            text.color = new Color(0.09f, 0.09f, 0.08f);
            text.fontStyle = FontStyle.Normal;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, labelSize - 8);
            text.resizeTextMaxSize = labelSize;

            runtimeButtons.Add(button);
            return button;
        }

        private Button CreateIconButton(Transform parent, string name, string iconName, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.07f, 0.28f, 0.34f, 0.16f);

            Button button = buttonObject.AddComponent<Button>();
            buttonObject.AddComponent<UiButtonClickSound>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.7f);
            colors.highlightedColor = new Color(0.42f, 0.62f, 0.66f, 0.95f);
            colors.pressedColor = new Color(0.22f, 0.34f, 0.38f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            RectTransform rect = button.GetComponent<RectTransform>();
            SetAnchors(rect, TextAnchor.UpperRight);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(56f, 56f);

            DrawIcon(buttonObject.transform, iconName);
            runtimeButtons.Add(button);
            return button;
        }

        private static void DrawIcon(Transform parent, string iconName)
        {
            Color iconColor = new Color32(0xE4, 0xE4, 0xDB, 0xFF);

            switch (iconName)
            {
                case "Home":
                    AddIconBlock(parent, new Vector2(-14f, 1f), new Vector2(6f, 16f), iconColor);
                    AddIconBlock(parent, new Vector2(14f, 1f), new Vector2(6f, 16f), iconColor);
                    AddIconBlock(parent, new Vector2(0f, -8f), new Vector2(28f, 6f), iconColor);
                    AddIconBlock(parent, new Vector2(-8f, 13f), new Vector2(18f, 6f), iconColor, 35f);
                    AddIconBlock(parent, new Vector2(8f, 13f), new Vector2(18f, 6f), iconColor, -35f);
                    AddIconBlock(parent, new Vector2(0f, -2f), new Vector2(8f, 14f), iconColor);
                    break;
                case "Settings":
                    AddIconBlock(parent, new Vector2(0f, 0f), new Vector2(18f, 18f), iconColor);
                    AddIconBlock(parent, new Vector2(0f, 22f), new Vector2(8f, 12f), iconColor);
                    AddIconBlock(parent, new Vector2(0f, -22f), new Vector2(8f, 12f), iconColor);
                    AddIconBlock(parent, new Vector2(22f, 0f), new Vector2(12f, 8f), iconColor);
                    AddIconBlock(parent, new Vector2(-22f, 0f), new Vector2(12f, 8f), iconColor);
                    AddIconBlock(parent, new Vector2(15f, 15f), new Vector2(10f, 8f), iconColor, 45f);
                    AddIconBlock(parent, new Vector2(-15f, 15f), new Vector2(10f, 8f), iconColor, -45f);
                    AddIconBlock(parent, new Vector2(15f, -15f), new Vector2(10f, 8f), iconColor, -45f);
                    AddIconBlock(parent, new Vector2(-15f, -15f), new Vector2(10f, 8f), iconColor, 45f);
                    AddIconBlock(parent, new Vector2(0f, 0f), new Vector2(8f, 8f), new Color(0.02f, 0.04f, 0.07f, 1f));
                    break;
                default:
                    AddIconBlock(parent, new Vector2(-8f, 16f), new Vector2(22f, 6f), iconColor);
                    AddIconBlock(parent, new Vector2(-19f, 6f), new Vector2(6f, 18f), iconColor);
                    AddIconBlock(parent, new Vector2(-8f, -9f), new Vector2(22f, 6f), iconColor);
                    AddIconBlock(parent, new Vector2(13f, 2f), new Vector2(6f, 16f), iconColor);
                    AddIconBlock(parent, new Vector2(21f, 14f), new Vector2(14f, 6f), iconColor, 45f);
                    AddIconBlock(parent, new Vector2(21f, 14f), new Vector2(14f, 6f), iconColor, -45f);
                    break;
            }
        }

        private static void AddIconBlock(Transform parent, Vector2 anchoredPosition, Vector2 size, Color color, float rotation = 0f)
        {
            GameObject block = new GameObject("Pixel");
            block.transform.SetParent(parent, false);

            Image image = block.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            RectTransform rect = block.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private Sprite GetButtonSprite(int styleIndex)
        {
            LoadRuntimeAssets();
            if (buttonSprites == null || buttonSprites.Length == 0)
            {
                return null;
            }

            int index = Mathf.Abs(styleIndex) % buttonSprites.Length;
            return buttonSprites[index];
        }

        private Text CreateText(Transform parent, string name, int size, TextAnchor anchor, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = new Color(0.78f, 0.96f, 1f);
            text.alignment = anchor;
            text.raycastTarget = false;

            RectTransform rect = text.GetComponent<RectTransform>();
            SetAnchors(rect, anchor);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta == Vector2.zero ? new Vector2(140f, 34f) : sizeDelta;

            return text;
        }

        private static void SetAnchors(RectTransform rect, TextAnchor anchor)
        {
            if (anchor == TextAnchor.UpperLeft)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
            }
            else if (anchor == TextAnchor.UpperCenter)
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
            }
            else if (anchor == TextAnchor.UpperRight)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
            }
            else if (anchor == TextAnchor.LowerCenter)
            {
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        private static string GetModeLabel(GameMode gameMode)
        {
            switch (gameMode)
            {
                case GameMode.Tutorial:
                    return "Tutorial";
                case GameMode.Campaign:
                    return "Level";
                case GameMode.Classic:
                    return "Classic";
                case GameMode.TimeAttack:
                    return "Time";
                default:
                    return "Home";
            }
        }

        private static string BuildLevelHeader(string levelName, GameMode gameMode)
        {
            string modeLabel = GetModeLabel(gameMode);
            if (string.IsNullOrWhiteSpace(levelName))
            {
                return modeLabel;
            }

            if (levelName.StartsWith(modeLabel, StringComparison.OrdinalIgnoreCase))
            {
                return levelName;
            }

            return $"{modeLabel}  {levelName}";
        }

        private static string BuildObjectiveText(string levelName, GameMode gameMode, float timeLimit, float elapsedTime)
        {
            if (gameMode == GameMode.Tutorial)
            {
                return "Follow the highlighted tile. Read the reason below before moving.";
            }

            if (gameMode == GameMode.TimeAttack)
            {
                return $"Objective: clear every safe node before {Mathf.Max(0f, timeLimit - elapsedTime):0}s runs out";
            }

            return "Objective: reveal every safe node";
        }

        private static string BuildStatsText(float elapsedTime, float timeLimit, int remainingDefusers, int safeTilesRemaining, string modeStats)
        {
            string timer = timeLimit > 0f
                ? $"Time {Mathf.Max(0f, timeLimit - elapsedTime):0.0}s"
                : $"Time {elapsedTime:0.0}s";

            return $"{timer}   Viruses {remainingDefusers}   Safe {safeTilesRemaining}   {modeStats}";
        }
    }

    internal sealed class UiPulse : MonoBehaviour
    {
        private Text targetText;
        private Color dimColor;
        private Color brightColor;
        private float scaleAmount;
        private float speed;
        private Vector3 baseScale;

        public void Configure(Text target, Color dim, Color bright, float scale, float pulseSpeed)
        {
            targetText = target;
            dimColor = dim;
            brightColor = bright;
            scaleAmount = scale;
            speed = pulseSpeed;
            baseScale = transform.localScale;
        }

        private void Update()
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * speed * Mathf.PI * 2f) + 1f) * 0.5f;
            if (targetText != null)
            {
                targetText.color = Color.Lerp(dimColor, brightColor, pulse);
            }

            transform.localScale = baseScale * (1f + pulse * scaleAmount);
        }
    }

    internal sealed class PixelStarTwinkle : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private float phase;

        public void Configure(float startPhase)
        {
            phase = startPhase;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void Update()
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 4.1f + phase * Mathf.PI * 2f) + 1f) * 0.5f;
            float snap = pulse > 0.58f ? 1f : 0.38f;
            canvasGroup.alpha = snap;
            transform.localScale = Vector3.one * Mathf.Lerp(0.86f, 1.12f, snap);
        }
    }

    internal sealed class UiButtonClickSound : MonoBehaviour, IPointerClickHandler
    {
        private static int lastClickFrame = -1;
        private static float lastClickTime = -10f;

        public static void Play()
        {
            if (lastClickFrame == Time.frameCount || Time.unscaledTime - lastClickTime < 0.05f)
            {
                return;
            }

            lastClickFrame = Time.frameCount;
            lastClickTime = Time.unscaledTime;

            AudioManager audioManager = FindAnyObjectByType<AudioManager>();
            audioManager?.PlayUiClick();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Button button = GetComponent<Button>();
            if (button == null || !button.interactable)
            {
                return;
            }

            Play();
        }
    }
}
