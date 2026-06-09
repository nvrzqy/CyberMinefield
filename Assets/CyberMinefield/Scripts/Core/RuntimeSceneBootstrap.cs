using CyberMinefield.Audio;
using CyberMinefield.Grid;
using CyberMinefield.Levels;
using CyberMinefield.Player;
using CyberMinefield.UI;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CyberMinefield.Core
{
    public static class RuntimeSceneBootstrap
    {
        private const string RootName = "CyberMinefield";
        private const string PlayerName = "Player";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeObjects()
        {
            GameManager existingGameManager = Object.FindAnyObjectByType<GameManager>();
            if (existingGameManager != null)
            {
                EnsureGameLoopObjects(existingGameManager.gameObject);
                EnsureCamera();
                EnsureLight();
                EnsureEventSystem();
                EnsureSingleAudioListener();
                return;
            }

            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
            }

            AddIfMissing<LevelManager>(root);
            AddIfMissing<GridManager>(root);
            AddIfMissing<InputManager>(root);
            AddIfMissing<AudioManager>(root);
            AddIfMissing<GameManager>(root);

            EnsureGameLoopObjects(root);

            EnsureCamera();
            EnsureLight();
            EnsureEventSystem();
            EnsureSingleAudioListener();
        }

        private static void EnsureGameLoopObjects(GameObject root)
        {
            if (root == null)
            {
                root = GameObject.Find(RootName);
            }

            if (root == null)
            {
                root = new GameObject(RootName);
            }

            if (Object.FindAnyObjectByType<LevelManager>() == null)
            {
                root.AddComponent<LevelManager>();
            }

            if (Object.FindAnyObjectByType<GridManager>() == null)
            {
                root.AddComponent<GridManager>();
            }

            if (Object.FindAnyObjectByType<InputManager>() == null)
            {
                root.AddComponent<InputManager>();
            }

            if (Object.FindAnyObjectByType<AudioManager>() == null)
            {
                root.AddComponent<AudioManager>();
            }

            if (Object.FindAnyObjectByType<GameManager>() == null)
            {
                root.AddComponent<GameManager>();
            }

            if (Object.FindAnyObjectByType<PlayerController>() == null)
            {
                GameObject player = new GameObject(PlayerName);
                player.AddComponent<PlayerController>();
            }

            if (Object.FindAnyObjectByType<UIManager>() == null)
            {
                UIManager.CreateRuntimeHud();
            }
        }

        private static void EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
                if (cameras.Length > 0)
                {
                    camera = cameras[0];
                    camera.tag = "MainCamera";
                }
                else
                {
                    GameObject cameraObject = new GameObject("Main Camera");
                    camera = cameraObject.AddComponent<Camera>();
                    camera.tag = "MainCamera";
                }
            }

            if (camera == null)
            {
                return;
            }

            DisableExtraCameras(camera);
            camera.transform.position = new Vector3(2.3f, 6f, -7f);
            camera.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
            camera.orthographic = false;
            camera.fieldOfView = 58f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.04f, 0.07f);

            if (camera.GetComponent<AudioListener>() == null)
            {
                camera.gameObject.AddComponent<AudioListener>();
            }
        }

        private static void DisableExtraCameras(Camera primaryCamera)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            foreach (Camera camera in cameras)
            {
                if (camera == primaryCamera)
                {
                    continue;
                }

                camera.enabled = false;
                AudioListener extraListener = camera.GetComponent<AudioListener>();
                if (extraListener != null)
                {
                    Object.Destroy(extraListener);
                }
            }
        }

        private static void EnsureLight()
        {
            if (Object.FindAnyObjectByType<Light>() != null)
            {
                return;
            }

            GameObject lightObject = new GameObject("Cyber Grid Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static T AddIfMissing<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            StandaloneInputModule legacyInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyInputModule != null)
            {
                Object.Destroy(legacyInputModule);
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

        private static void EnsureSingleAudioListener()
        {
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
            if (listeners.Length <= 1)
            {
                return;
            }

            AudioListener listenerToKeep = Camera.main != null
                ? Camera.main.GetComponent<AudioListener>()
                : listeners[0];

            if (listenerToKeep == null)
            {
                listenerToKeep = Camera.main != null
                    ? Camera.main.gameObject.AddComponent<AudioListener>()
                    : listeners[0];
            }

            foreach (AudioListener listener in listeners)
            {
                if (listener != listenerToKeep)
                {
                    Object.Destroy(listener);
                }
            }
        }
    }
}
