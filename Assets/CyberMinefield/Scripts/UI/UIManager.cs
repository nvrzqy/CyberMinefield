using CyberMinefield.Core;
using CyberMinefield.Levels;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CyberMinefield.UI
{
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private Text levelText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text messageText;
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Text homeStatsText;
        [SerializeField] private Text pauseTitleText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button cameraLockButton;
        [SerializeField] private Text cameraLockButtonLabel;

        private GameManager gameManager;
        private Font font;
        private readonly List<Button> runtimeButtons = new List<Button>();
        private int lastManualClickFrame = -1;

        public static UIManager CreateRuntimeHud()
        {
            GameObject canvasObject = new GameObject("UIManager");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            UIManager uiManager = canvasObject.AddComponent<UIManager>();
            uiManager.BuildLayout(canvasObject.transform);
            return uiManager;
        }

        private void Awake()
        {
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

        private void Update()
        {
            InvokeButtonUnderMouseIfNeeded();
        }

        private void OnGUI()
        {
            Event currentEvent = Event.current;
            if (currentEvent == null
                || currentEvent.type != EventType.MouseDown
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
            EnsureLayout();
            ApplyPanelRaycastRules();
            homePanel.SetActive(true);
            hudPanel.SetActive(false);
            pausePanel.SetActive(false);
            homeStatsText.text = classicStats;
        }

        public void ShowGameplay()
        {
            EnsureLayout();
            ApplyPanelRaycastRules();
            homePanel.SetActive(false);
            hudPanel.SetActive(true);
            pausePanel.SetActive(false);
        }

        public void ShowPause(string title)
        {
            EnsureLayout();
            ApplyPanelRaycastRules();
            pausePanel.SetActive(true);
            pauseTitleText.text = title;
        }

        public void HidePause()
        {
            EnsureLayout();
            pausePanel.SetActive(false);
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
            levelText.text = $"{GetModeLabel(gameMode)}  {levelName}";
            objectiveText.text = BuildObjectiveText(levelName, gameMode, timeLimit, elapsedTime);
            statsText.text = BuildStatsText(elapsedTime, timeLimit, remainingDefusers, safeTilesRemaining, modeStats);
            messageText.text = message;
            nextButton.gameObject.SetActive(gameState == GameState.Won && gameMode != GameMode.Classic);
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
            SetPanelRaycast(hudPanel, false);
            SetPanelRaycast(pausePanel, false);
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

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            homePanel = CreatePanel(parent, "HomePanel", new Color(0.02f, 0.04f, 0.07f, 0.92f), false);
            hudPanel = CreatePanel(parent, "HudPanel", new Color(0f, 0f, 0f, 0f), false);
            pausePanel = CreatePanel(parent, "PausePanel", new Color(0.02f, 0.04f, 0.07f, 0.72f), false);

            BuildHomePanel(homePanel.transform);
            BuildHudPanel(hudPanel.transform);
            BuildPausePanel(pausePanel.transform);
            homePanel.SetActive(true);
            hudPanel.SetActive(false);
            pausePanel.SetActive(false);
        }

        private bool NeedsLayoutRebuild()
        {
            return levelText == null
                || objectiveText == null
                || statsText == null
                || messageText == null
                || homePanel == null
                || hudPanel == null
                || pausePanel == null
                || homeStatsText == null
                || pauseTitleText == null
                || nextButton == null
                || cameraLockButton == null
                || cameraLockButtonLabel == null;
        }

        private void ClearReferences()
        {
            levelText = null;
            objectiveText = null;
            statsText = null;
            messageText = null;
            homePanel = null;
            hudPanel = null;
            pausePanel = null;
            homeStatsText = null;
            pauseTitleText = null;
            nextButton = null;
            cameraLockButton = null;
            cameraLockButtonLabel = null;
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
            Text title = CreateText(parent, "Title", 34, TextAnchor.UpperCenter, new Vector2(0f, -58f), new Vector2(0f, 44f));
            title.text = "Cyber Minefield";

            Text subtitle = CreateText(parent, "Subtitle", 16, TextAnchor.UpperCenter, new Vector2(0f, -106f), new Vector2(0f, 26f));
            subtitle.text = "Reveal safe nodes, defuse threats, and clear the network.";

            CreateButton(parent, "TutorialButton", "Tutorial", new Vector2(0f, -165f), () => WithGameManager(manager => manager.StartTutorial()));
            CreateButton(parent, "LevelButton", "Level", new Vector2(0f, -220f), () => WithGameManager(manager => manager.StartCampaign()));
            CreateButton(parent, "ClassicButton", "Classic", new Vector2(0f, -275f), () => WithGameManager(manager => manager.StartClassic()));
            CreateButton(parent, "TimeButton", "Time", new Vector2(0f, -330f), () => WithGameManager(manager => manager.StartTimeAttack()));

            homeStatsText = CreateText(parent, "HomeStats", 15, TextAnchor.LowerCenter, new Vector2(0f, 42f), new Vector2(0f, 44f));
            homeStatsText.text = string.Empty;
        }

        private void BuildHudPanel(Transform parent)
        {
            levelText = CreateText(parent, "LevelText", 22, TextAnchor.UpperLeft, new Vector2(18f, -14f), new Vector2(680f, 28f));
            objectiveText = CreateText(parent, "ObjectiveText", 16, TextAnchor.UpperLeft, new Vector2(18f, -44f), new Vector2(760f, 26f));
            statsText = CreateText(parent, "StatsText", 16, TextAnchor.UpperLeft, new Vector2(18f, -70f), new Vector2(880f, 26f));
            messageText = CreateText(parent, "MessageText", 16, TextAnchor.LowerCenter, new Vector2(0f, 24f), new Vector2(0f, 36f));

            CreateButton(parent, "ReplayButton", "Replay", new Vector2(-232f, -24f), () => WithGameManager(manager => manager.RestartLevel()), TextAnchor.UpperRight);
            CreateButton(parent, "HomeButton", "Home", new Vector2(-128f, -24f), () => WithGameManager(manager => manager.ShowHomePage()), TextAnchor.UpperRight);
            CreateButton(parent, "SettingsButton", "Settings", new Vector2(-24f, -24f), () => WithGameManager(manager => manager.TogglePauseFromUi()), TextAnchor.UpperRight);
            nextButton = CreateButton(parent, "NextButton", "Next", new Vector2(0f, 68f), () => WithGameManager(manager => manager.ContinueAfterWin()), TextAnchor.LowerCenter);
            nextButton.gameObject.SetActive(false);
        }

        private void BuildPausePanel(Transform parent)
        {
            pauseTitleText = CreateText(parent, "PauseTitle", 28, TextAnchor.MiddleCenter, new Vector2(0f, 76f), new Vector2(360f, 42f));
            pauseTitleText.text = "Settings";
            CreateButton(parent, "ResumeButton", "Resume", new Vector2(0f, 20f), () => WithGameManager(manager => manager.ResumeFromUi()), TextAnchor.MiddleCenter);
            cameraLockButton = CreateButton(parent, "CameraLockButton", "Camera Lock: OFF", new Vector2(0f, -36f), () => WithGameManager(manager => manager.ToggleCameraLockFromUi()), TextAnchor.MiddleCenter);
            cameraLockButtonLabel = cameraLockButton.GetComponentInChildren<Text>();
            CreateButton(parent, "PauseReplayButton", "Replay", new Vector2(0f, -92f), () => WithGameManager(manager => manager.RestartLevel()), TextAnchor.MiddleCenter);
            CreateButton(parent, "PauseHomeButton", "Home", new Vector2(0f, -148f), () => WithGameManager(manager => manager.ShowHomePage()), TextAnchor.MiddleCenter);
        }

        private void WithGameManager(System.Action<GameManager> action)
        {
            lastManualClickFrame = Time.frameCount;

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
            if (!WasLeftMousePressed() || lastManualClickFrame == Time.frameCount)
            {
                return;
            }

            InvokeButtonUnderPosition(GetMousePosition());
        }

        private bool InvokeButtonUnderPosition(Vector2 mousePosition)
        {
            RefreshRuntimeButtons();

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

                if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, mousePosition, null))
                {
                    lastManualClickFrame = Time.frameCount;
                    button.onClick.Invoke();
                    return true;
                }
            }

            return false;
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

        private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action, TextAnchor anchor = TextAnchor.UpperCenter)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.05f, 0.72f, 0.82f, 0.92f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            RectTransform rect = button.GetComponent<RectTransform>();
            SetAnchors(rect, anchor);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(150f, 38f);

            Text text = CreateText(buttonObject.transform, "Label", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            text.text = label;
            text.color = Color.black;

            runtimeButtons.Add(button);
            return button;
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

            return $"{timer}   Defusers {remainingDefusers}   Safe {safeTilesRemaining}   {modeStats}";
        }
    }
}
