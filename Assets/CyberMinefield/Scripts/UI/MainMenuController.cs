using CyberMinefield.Core;
using CyberMinefield.Grid;
using CyberMinefield.Levels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CyberMinefield.UI
{
    [RequireComponent(typeof(UIManager))]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private Button tutorialButton;
        [SerializeField] private Button levelButton;
        [SerializeField] private Button classicButton;
        [SerializeField] private Button timeButton;

        private bool buttonsWired;
        private int lastModeStartFrame = -1;

        private void Awake()
        {
            ResolveReferences();
            EnsureEventSystem();
        }

        private void Start()
        {
            WireButtons();
        }

        private void OnEnable()
        {
            WireButtons();
        }

        private void Update()
        {
            if (!buttonsWired)
            {
                WireButtons();
            }

            if (homePanel != null && homePanel.activeInHierarchy)
            {
                SetModeButtonsActive(true);
            }
        }

        [ContextMenu("Wire Main Menu Buttons")]
        public void WireButtons()
        {
            ResolveReferences();

            if (tutorialButton == null || levelButton == null || classicButton == null || timeButton == null)
            {
                buttonsWired = false;
                return;
            }

            WireButton(tutorialButton, StartTutorialMode);
            WireButton(levelButton, StartLevelMode);
            WireButton(classicButton, StartClassicMode);
            WireButton(timeButton, StartTimeMode);
            buttonsWired = true;
        }

        public void StartTutorialMode()
        {
            StartMode(GameMode.Tutorial, CreateTutorialLevel());
        }

        public void StartLevelMode()
        {
            StartMode(GameMode.Campaign, CreateLevelModeLevel());
        }

        public void StartClassicMode()
        {
            StartMode(GameMode.Classic, CreateClassicLevel());
        }

        public void StartTimeMode()
        {
            StartMode(GameMode.TimeAttack, CreateTimeLevel());
        }

        private void StartMode(GameMode mode, LevelDefinition fallbackLevel)
        {
            if (lastModeStartFrame == Time.frameCount)
            {
                return;
            }

            lastModeStartFrame = Time.frameCount;
            ResolveReferences();
            HideMenuShowGame();

            if (gameManager != null)
            {
                switch (mode)
                {
                    case GameMode.Tutorial:
                        gameManager.StartTutorial();
                        break;
                    case GameMode.Campaign:
                        gameManager.StartCampaign();
                        break;
                    case GameMode.Classic:
                        gameManager.StartClassic();
                        break;
                    case GameMode.TimeAttack:
                        gameManager.StartTimeAttack();
                        break;
                }

                return;
            }

            if (gridManager != null)
            {
                gridManager.Configure(fallbackLevel);
                gridManager.GenerateGrid();
            }

            if (uiManager != null)
            {
                uiManager.ShowGameplay();
            }
        }

        private void HideMenuShowGame()
        {
            if (homePanel != null)
            {
                homePanel.SetActive(false);
            }

            if (hudPanel != null)
            {
                hudPanel.SetActive(true);
            }

            SetModeButtonsActive(false);
        }

        private void SetModeButtonsActive(bool isActive)
        {
            SetButtonActive(tutorialButton, isActive);
            SetButtonActive(levelButton, isActive);
            SetButtonActive(classicButton, isActive);
            SetButtonActive(timeButton, isActive);
        }

        private static void SetButtonActive(Button button, bool isActive)
        {
            if (button != null && button.gameObject.activeSelf != isActive)
            {
                button.gameObject.SetActive(isActive);
            }
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void ResolveReferences()
        {
            if (uiManager == null)
            {
                uiManager = GetComponent<UIManager>();
            }

            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            if (gridManager == null)
            {
                gridManager = FindAnyObjectByType<GridManager>();
            }

            if (homePanel == null)
            {
                homePanel = FindSceneGameObject("HomePanel");
            }

            if (hudPanel == null)
            {
                hudPanel = FindSceneGameObject("HudPanel");
            }

            if (tutorialButton == null)
            {
                tutorialButton = FindSceneComponent<Button>("TutorialButton");
            }

            if (levelButton == null)
            {
                levelButton = FindSceneComponent<Button>("LevelButton");
            }

            if (classicButton == null)
            {
                classicButton = FindSceneComponent<Button>("ClassicButton");
            }

            if (timeButton == null)
            {
                timeButton = FindSceneComponent<Button>("TimeButton");
            }
        }

        private static T FindSceneComponent<T>(string objectName) where T : Component
        {
            T[] components = FindObjectsByType<T>(FindObjectsInactive.Include);
            foreach (T component in components)
            {
                if (component.gameObject.name == objectName)
                {
                    return component;
                }
            }

            return null;
        }

        private static GameObject FindSceneGameObject(string objectName)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (Transform candidate in transforms)
            {
                if (candidate.gameObject.name == objectName)
                {
                    return candidate.gameObject;
                }
            }

            return null;
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

        private static LevelDefinition CreateTutorialLevel()
        {
            return new LevelDefinition("Tutorial", 5, 5, 3, 3, 0f, new Vector2Int(2, 2), new Vector2Int(4, 4), WinConditionType.ClearSafeTiles, 9);
        }

        private static LevelDefinition CreateLevelModeLevel()
        {
            return new LevelDefinition("Level", 6, 6, 5, 5, 0f, new Vector2Int(3, 3), new Vector2Int(5, 5), WinConditionType.ClearSafeTiles, 19);
        }

        private static LevelDefinition CreateClassicLevel()
        {
            return new LevelDefinition("Classic", 8, 8, 8, 8, 0f, new Vector2Int(4, 4), new Vector2Int(7, 7), WinConditionType.ClearSafeTiles, 29);
        }

        private static LevelDefinition CreateTimeLevel()
        {
            return new LevelDefinition("Time", 8, 8, 10, 10, 90f, new Vector2Int(4, 4), new Vector2Int(7, 7), WinConditionType.ClearSafeTiles, 39);
        }
    }
}
