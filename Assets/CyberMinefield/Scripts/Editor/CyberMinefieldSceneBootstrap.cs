using CyberMinefield.Grid;
using CyberMinefield.Core;
using CyberMinefield.Levels;
using CyberMinefield.Player;
using CyberMinefield.UI;
using CyberMinefield.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CyberMinefield.Editor
{
    public static class CyberMinefieldSceneBootstrap
    {
        private const string RootName = "CyberMinefield";
        private const string GridName = "Grid";
        private const string PlayerName = "Player";

        [InitializeOnLoadMethod]
        private static void EnsureVisibleGrid()
        {
            EditorApplication.delayCall += CreateGridIfNeeded;
        }

        private static void CreateGridIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            GridManager existingManager = Object.FindAnyObjectByType<GridManager>();
            GridManager manager = existingManager != null ? existingManager : CreateManager();
            bool changed = EnsureGameLoop(manager);
            EnsureCamera(manager);
            EnsureLight();

            Transform grid = manager.transform.Find(GridName);
            if (grid != null && grid.childCount > 0)
            {
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }

                return;
            }

            manager.GenerateGrid();
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }

        private static GridManager CreateManager()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                root.transform.position = Vector3.zero;
            }

            GridManager manager = root.GetComponent<GridManager>();
            if (manager == null)
            {
                manager = root.AddComponent<GridManager>();
            }

            return manager;
        }

        private static bool EnsureGameLoop(GridManager manager)
        {
            GameObject root = manager.gameObject;
            bool changed = false;

            if (root.GetComponent<LevelManager>() == null)
            {
                root.AddComponent<LevelManager>();
                changed = true;
            }

            if (root.GetComponent<GameManager>() == null)
            {
                root.AddComponent<GameManager>();
                changed = true;
            }

            if (root.GetComponent<InputManager>() == null)
            {
                root.AddComponent<InputManager>();
                changed = true;
            }

            if (root.GetComponent<AudioManager>() == null)
            {
                root.AddComponent<AudioManager>();
                changed = true;
            }

            if (Object.FindAnyObjectByType<PlayerController>() == null)
            {
                GameObject player = new GameObject(PlayerName);
                player.AddComponent<PlayerController>();
                changed = true;
            }

            if (Object.FindAnyObjectByType<UIManager>() == null)
            {
                UIManager.CreateRuntimeHud();
                changed = true;
            }

            return changed;
        }

        private static void EnsureCamera(GridManager manager)
        {
            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            float centerX = Mathf.Max(0f, (manager.Width - 1) * manager.TileSpacing * 0.5f);
            float centerZ = Mathf.Max(0f, (manager.Height - 1) * manager.TileSpacing * 0.5f);
            camera.transform.position = new Vector3(centerX, 6f, centerZ - 7f);
            camera.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
            camera.orthographic = false;
            camera.fieldOfView = 58f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.04f, 0.07f);
        }

        private static void EnsureLight()
        {
            Light existingLight = Object.FindAnyObjectByType<Light>();
            if (existingLight != null)
            {
                return;
            }

            GameObject lightObject = new GameObject("Cyber Grid Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
