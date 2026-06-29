using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GLTFast;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberMinefield.Grid
{
    public enum DefuserMarkerStyle
    {
        Flag,
        Virus
    }

    public sealed class TileNode : MonoBehaviour
    {
        private static readonly Color ClosedColor = new Color(0.08f, 0.13f, 0.18f);
        private static readonly Color SafeRevealedColor = new Color(0.28f, 0.45f, 0.38f);
        private static readonly Color DangerRevealedColor = new Color(0.9f, 0.16f, 0.12f);
        private static readonly Color DefuserColor = new Color(0.05f, 0.72f, 0.82f);
        private static readonly Color ExitClosedColor = new Color(0.18f, 0.35f, 0.23f);
        private static readonly Color NeutralizedColor = new Color(0.18f, 0.68f, 0.86f);
        private static readonly Color MisflagColor = new Color(0.95f, 0.42f, 0.16f);
        private static readonly Color WinGlowColor = new Color(0.35f, 1f, 0.48f);
        private static readonly Color VirusSpreadColor = new Color(0.76f, 0.08f, 0.12f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Vector3 FlagModelPosition = new Vector3(0f, 0.1f, 0f);
        private static readonly Quaternion FlagModelRotation = Quaternion.identity;
        private const float FlagModelScale = 1.50f;
        private static readonly Vector3 VirusModelPosition = Vector3.zero;
        private static readonly Quaternion VirusModelRotation = Quaternion.identity;
        private const float VirusModelScale = 1.08f;
        private const bool UseDetailedMarkerModels = false;
        private const int DetailedMarkerModelAreaLimit = 225;
        private static Material runtimePrimitiveMaterial;

        [SerializeField] private int x;
        [SerializeField] private int y;
        [SerializeField] private bool hasDanger;
        [SerializeField] private bool isRevealed;
        [SerializeField] private bool hasDefuser;
        [SerializeField] private DefuserMarkerStyle markerStyle;
        [SerializeField] private bool isExit;
        [SerializeField] private int adjacentDangerCount;

        private GridManager gridManager;
        private Renderer cachedRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Color currentTileColor = ClosedColor;
        private TextMesh label;
        private GameObject defuserMarker;
        private GameObject flagMarkerGroup;
        private GameObject virusMarkerGroup;
        private Transform virusSpinRoot;
        private TextMesh tutorialHintLabel;
        private string tutorialHintText = string.Empty;
        private Color tutorialHintColor = Color.white;
        private bool playerOccupying;
        private bool isMisflagRevealed;
        private bool isVirusSpreadRevealed;
        private bool showVirusSpreadMarker;
        private Coroutine winEffectCoroutine;
        private static Font pixelFont;
        private static readonly Dictionary<string, GltfImport> CachedGlbImports = new Dictionary<string, GltfImport>();
        private static readonly HashSet<string> FailedGlbImports = new HashSet<string>();

        public static DefuserMarkerStyle SelectedMarkerStyle { get; private set; } = DefuserMarkerStyle.Flag;

        public int X => x;
        public int Y => y;
        public bool HasDanger => hasDanger;
        public bool IsMine => hasDanger;
        public bool IsRevealed => isRevealed;
        public bool HasDefuser => hasDefuser;
        public bool IsFlagged => hasDefuser;
        public bool IsExit => isExit;
        public bool IsPlayerOccupying => playerOccupying;
        public int AdjacentDangerCount => adjacentDangerCount;
        public int AdjacentMineCount => adjacentDangerCount;
        public Vector2Int Coordinates => new Vector2Int(x, y);

        public static void SetSelectedMarkerStyle(DefuserMarkerStyle style)
        {
            SelectedMarkerStyle = style;
        }

        public void Initialize(int xCoordinate, int yCoordinate, GridManager owner)
        {
            x = xCoordinate;
            y = yCoordinate;
            gridManager = owner;
            hasDanger = false;
            isRevealed = false;
            hasDefuser = false;
            markerStyle = SelectedMarkerStyle;
            isExit = false;
            adjacentDangerCount = 0;
            playerOccupying = false;
            isMisflagRevealed = false;
            isVirusSpreadRevealed = false;
            showVirusSpreadMarker = false;
            StopWinEffect();
            EnsureLabel();
            EnsureDefuserMarker();
            EnsureTutorialHintLabel();
            ApplyPresentation();
        }

        private void Update()
        {
            if ((isVirusSpreadRevealed || (hasDefuser && markerStyle == DefuserMarkerStyle.Virus))
                && virusSpinRoot != null
                && virusMarkerGroup != null
                && virusMarkerGroup.activeInHierarchy)
            {
                virusSpinRoot.Rotate(Vector3.up, 36f * Time.deltaTime, Space.Self);
                float bob = Mathf.Sin(Time.time * 3.4f + x * 0.37f + y * 0.19f) * 0.04f;
                virusSpinRoot.localPosition = new Vector3(0f, 0.36f + bob, 0f);
            }
        }

        public void SetPlayerOccupying(bool value)
        {
            if (playerOccupying == value)
            {
                return;
            }

            playerOccupying = value;
            ApplyPresentation();
        }

        public void SetDanger(bool value)
        {
            hasDanger = value;
        }

        public void ResetGameplayState()
        {
            hasDanger = false;
            isRevealed = false;
            hasDefuser = false;
            markerStyle = SelectedMarkerStyle;
            isExit = false;
            adjacentDangerCount = 0;
            playerOccupying = false;
            isMisflagRevealed = false;
            isVirusSpreadRevealed = false;
            showVirusSpreadMarker = false;
            StopWinEffect();
            tutorialHintText = string.Empty;
            ApplyPresentation();
        }

        public void SetMine(bool value)
        {
            SetDanger(value);
        }

        public void SetExit(bool value)
        {
            isExit = value;
            ApplyPresentation();
        }

        public void SetAdjacentDangerCount(int count)
        {
            adjacentDangerCount = count;
        }

        public void SetAdjacentMineCount(int count)
        {
            SetAdjacentDangerCount(count);
        }

        public void SetTutorialHint(string text, Color color)
        {
            tutorialHintText = text;
            tutorialHintColor = color;
            ApplyPresentation();
        }

        public void ClearTutorialHint()
        {
            tutorialHintText = string.Empty;
            ApplyPresentation();
        }

        public void StepOnTile()
        {
            if (gridManager != null)
            {
                gridManager.RevealTile(Coordinates);
            }
        }

        public bool ToggleDefuser()
        {
            if (isRevealed)
            {
                return hasDefuser;
            }

            hasDefuser = !hasDefuser;
            if (hasDefuser)
            {
                markerStyle = SelectedMarkerStyle;
            }

            ApplyPresentation();
            Debug.Log($"Defuser {(hasDefuser ? "placed" : "removed")} at ({x}, {y})", this);
            return hasDefuser;
        }

        public bool TryReveal()
        {
            if (isRevealed)
            {
                return false;
            }

            isRevealed = true;
            ApplyPresentation();
            Debug.Log(BuildRevealMessage(), this);
            return true;
        }

        public void RevealDangerForResult(bool showMarker = true)
        {
            if (!hasDanger)
            {
                return;
            }

            isRevealed = true;
            isVirusSpreadRevealed = true;
            showVirusSpreadMarker = showMarker;
            ApplyPresentation();
        }

        public void RevealMisflagForResult()
        {
            if (hasDanger || !hasDefuser)
            {
                return;
            }

            isMisflagRevealed = true;
            isRevealed = true;
            ApplyPresentation();
        }

        public void InfectWithVirusSpread(bool showMarker = false)
        {
            isRevealed = true;
            isVirusSpreadRevealed = true;
            showVirusSpreadMarker = showMarker;
            ApplyPresentation();
        }

        public void PlayWinEffect(int sequenceIndex)
        {
            StopWinEffect();
            winEffectCoroutine = StartCoroutine(PulseWinColor(sequenceIndex));
        }

        private void ApplyPresentation()
        {
            Color tileColor = ClosedColor;
            string labelText = string.Empty;
            Color labelColor = Color.white;

            if (isExit && !isRevealed)
            {
                tileColor = ExitClosedColor;
            }

            if (hasDefuser && !isRevealed)
            {
                tileColor = DefuserColor;
            }

            if (isVirusSpreadRevealed)
            {
                tileColor = VirusSpreadColor;
                labelText = string.Empty;
            }
            else if (isRevealed)
            {
                if (isMisflagRevealed)
                {
                    tileColor = MisflagColor;
                    labelText = "X";
                    labelColor = Color.black;
                }
                else if (hasDanger && hasDefuser)
                {
                    tileColor = NeutralizedColor;
                    labelColor = Color.black;
                }
                else if (hasDanger)
                {
                    tileColor = DangerRevealedColor;
                    labelColor = Color.white;
                }
                else
                {
                    tileColor = SafeRevealedColor;
                    labelText = adjacentDangerCount > 0 ? adjacentDangerCount.ToString() : string.Empty;
                    labelColor = adjacentDangerCount == 0 ? new Color(0.78f, 0.92f, 0.86f) : Color.white;
                }
            }

            ApplyColor(tileColor);
            SetLabel(labelText, labelColor);
            SetLabelVisible(!string.IsNullOrEmpty(labelText));
            SetTutorialHintVisible(!string.IsNullOrEmpty(tutorialHintText));

            if (defuserMarker != null)
            {
                UpdateDefuserMarkerTransform();
                bool showVirusSpread = isVirusSpreadRevealed;
                bool showSpreadMarker = showVirusSpread && showVirusSpreadMarker;
                bool showMarker = hasDefuser || showSpreadMarker;
                defuserMarker.SetActive(showMarker);
                if (showSpreadMarker)
                {
                    EnsureMarkerVisual(DefuserMarkerStyle.Virus);
                }
                else if (hasDefuser)
                {
                    EnsureMarkerVisual(markerStyle);
                }

                if (flagMarkerGroup != null)
                {
                    flagMarkerGroup.SetActive(hasDefuser && !showSpreadMarker && markerStyle == DefuserMarkerStyle.Flag);
                }

                if (virusMarkerGroup != null)
                {
                    virusMarkerGroup.SetActive(showSpreadMarker || (hasDefuser && !showVirusSpread && markerStyle == DefuserMarkerStyle.Virus));
                }
            }
        }

        private void ApplyColor(Color color)
        {
            currentTileColor = color;

            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
            }

            if (cachedRenderer == null)
            {
                return;
            }

            Material material = EnsureRuntimePrimitiveMaterial();
            if (material != null && cachedRenderer.sharedMaterial != material)
            {
                cachedRenderer.sharedMaterial = material;
            }

            OptimizeRendererForRuntimeTile(cachedRenderer);

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            cachedRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            cachedRenderer.SetPropertyBlock(propertyBlock);
        }

        private void EnsureLabel()
        {
            if (label != null)
            {
                return;
            }

            GameObject labelObject = new GameObject("HintLabel");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.096f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = Vector3.one * 0.14f;

            label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 1f;
            label.fontSize = 42;
            label.text = string.Empty;
            ApplyPixelFont(label);
        }

        private void EnsureTutorialHintLabel()
        {
            if (tutorialHintLabel != null)
            {
                return;
            }

            GameObject hintObject = new GameObject("TutorialHintLabel");
            hintObject.transform.SetParent(transform, false);
            hintObject.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            hintObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hintObject.transform.localScale = Vector3.one * 0.11f;

            tutorialHintLabel = hintObject.AddComponent<TextMesh>();
            tutorialHintLabel.anchor = TextAnchor.MiddleCenter;
            tutorialHintLabel.alignment = TextAlignment.Center;
            tutorialHintLabel.characterSize = 1f;
            tutorialHintLabel.fontSize = 34;
            tutorialHintLabel.text = string.Empty;
            ApplyPixelFont(tutorialHintLabel);
            tutorialHintLabel.gameObject.SetActive(false);
        }

        private void EnsureDefuserMarker()
        {
            if (defuserMarker != null)
            {
                return;
            }

            defuserMarker = new GameObject("DefuserMarker");
            defuserMarker.name = "DefuserMarker";
            defuserMarker.transform.SetParent(transform, false);
            UpdateDefuserMarkerTransform();

            defuserMarker.SetActive(false);
        }

        private void UpdateDefuserMarkerTransform()
        {
            if (defuserMarker == null)
            {
                return;
            }

            Vector3 parentScale = transform.lossyScale;
            defuserMarker.transform.localPosition = Vector3.zero;
            defuserMarker.transform.localRotation = Quaternion.identity;
            defuserMarker.transform.localScale = new Vector3(
                SafeInverse(parentScale.x),
                SafeInverse(parentScale.y),
                SafeInverse(parentScale.z));
        }

        private static float SafeInverse(float value)
        {
            return Mathf.Abs(value) <= 0.0001f ? 1f : 1f / value;
        }

        private void EnsureMarkerVisual(DefuserMarkerStyle style)
        {
            EnsureDefuserMarker();
            if (style == DefuserMarkerStyle.Flag)
            {
                BuildFlagMarker(defuserMarker.transform);
            }
            else
            {
                BuildVirusMarker(defuserMarker.transform);
            }
        }

        private void BuildFlagMarker(Transform parent)
        {
            if (flagMarkerGroup != null)
            {
                return;
            }

            flagMarkerGroup = new GameObject("FlagMarker").gameObject;
            flagMarkerGroup.transform.SetParent(parent, false);

            if (ShouldUseDetailedMarkerModel()
                && TryInstantiatePlacedMarkerModel("Models/flagpole_3d_model", flagMarkerGroup.transform, FlagModelPosition, FlagModelRotation, FlagModelScale, true, true))
            {
                return;
            }

            BuildBlockyFlag(flagMarkerGroup.transform);
        }

        private static void BuildBlockyFlag(Transform parent)
        {
            Color dark = new Color(0.02f, 0.025f, 0.04f);
            Color metalLight = new Color(0.58f, 0.62f, 0.78f);
            Color metalMid = new Color(0.28f, 0.31f, 0.48f);
            Color red = new Color(0.9f, 0.02f, 0.03f);
            Color redDark = new Color(0.55f, 0.02f, 0.05f);

            AddBlock(parent, "Base", new Vector3(0f, 0.13f, 0f), new Vector3(0.34f, 0.1f, 0.28f), dark);
            AddBlock(parent, "BaseHighlight", new Vector3(-0.04f, 0.2f, -0.03f), new Vector3(0.22f, 0.045f, 0.18f), metalMid);
            AddBlock(parent, "Pole", new Vector3(0f, 0.76f, 0f), new Vector3(0.075f, 1.18f, 0.075f), dark);
            AddBlock(parent, "PoleHighlight", new Vector3(-0.023f, 0.82f, -0.023f), new Vector3(0.022f, 0.98f, 0.018f), metalLight);
            AddBlock(parent, "Cap", new Vector3(0f, 1.38f, 0f), new Vector3(0.16f, 0.11f, 0.16f), dark);
            AddBlock(parent, "CapHighlight", new Vector3(-0.025f, 1.41f, -0.025f), new Vector3(0.08f, 0.045f, 0.08f), metalLight);

            AddBlock(parent, "FlagPanel", new Vector3(0.28f, 1.18f, 0f), new Vector3(0.48f, 0.31f, 0.045f), red);
            AddBlock(parent, "FlagPixelTop", new Vector3(0.1f, 1.31f, 0f), new Vector3(0.12f, 0.09f, 0.05f), red);
            AddBlock(parent, "FlagPixelTail", new Vector3(0.52f, 1.02f, 0f), new Vector3(0.16f, 0.11f, 0.05f), red);
            AddBlock(parent, "FlagBottomShade", new Vector3(0.35f, 1.02f, 0f), new Vector3(0.26f, 0.06f, 0.052f), redDark);
        }

        private static void AddBlock(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = localScale;
            SetPrimitiveColor(block, color);
        }

        private void BuildVirusMarker(Transform parent)
        {
            if (virusMarkerGroup != null)
            {
                return;
            }

            virusMarkerGroup = new GameObject("VirusMarker").gameObject;
            virusMarkerGroup.transform.SetParent(parent, false);

            GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = "Shadow";
            shadow.transform.SetParent(virusMarkerGroup.transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0.105f, 0f);
            shadow.transform.localScale = new Vector3(0.26f, 0.012f, 0.26f);
            SetPrimitiveColor(shadow, new Color(0f, 0f, 0f, 0.35f));

            GameObject spinRootObject = new GameObject("VirusSpinRoot");
            spinRootObject.transform.SetParent(virusMarkerGroup.transform, false);
            virusSpinRoot = spinRootObject.transform;
            virusSpinRoot.localPosition = new Vector3(0f, 0.36f, 0f);

            if (ShouldUseDetailedMarkerModel()
                && TryInstantiatePlacedMarkerModel("Models/pixel_art_creature_3d_model", virusSpinRoot, VirusModelPosition, VirusModelRotation, VirusModelScale, true, false))
            {
                return;
            }

            BuildBlockyVirus(virusSpinRoot);
        }

        private static void BuildBlockyVirus(Transform parent)
        {
            Color red = new Color(0.62f, 0.03f, 0.05f);
            Color redDark = new Color(0.36f, 0.015f, 0.03f);
            Color pale = new Color(0.82f, 0.78f, 0.68f);

            AddBlock(parent, "VirusBody", Vector3.zero, new Vector3(0.42f, 0.34f, 0.42f), red);
            AddBlock(parent, "VirusTop", new Vector3(0f, 0.25f, 0f), new Vector3(0.22f, 0.14f, 0.22f), red);
            AddBlock(parent, "VirusCap", new Vector3(0f, 0.39f, 0f), new Vector3(0.12f, 0.1f, 0.12f), redDark);
            AddBlock(parent, "VirusFace", new Vector3(0f, 0.01f, -0.216f), new Vector3(0.26f, 0.18f, 0.025f), redDark);
            AddBlock(parent, "VirusEyeLeft", new Vector3(-0.085f, 0.04f, -0.232f), new Vector3(0.05f, 0.055f, 0.02f), pale);
            AddBlock(parent, "VirusEyeRight", new Vector3(0.085f, 0.04f, -0.232f), new Vector3(0.05f, 0.055f, 0.02f), pale);
            AddBlock(parent, "VirusMouth", new Vector3(0f, -0.08f, -0.232f), new Vector3(0.11f, 0.045f, 0.02f), pale);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * 0.28f, 0f, Mathf.Sin(angle) * 0.28f);
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "VirusLeg";
                leg.transform.SetParent(parent, false);
                leg.transform.localPosition = offset;
                leg.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                leg.transform.localScale = new Vector3(0.08f, 0.08f, 0.22f);
                SetPrimitiveColor(leg, redDark);
            }
        }

        private bool ShouldUseDetailedMarkerModel()
        {
            return UseDetailedMarkerModels && (gridManager == null || gridManager.Width * gridManager.Height <= DetailedMarkerModelAreaLimit);
        }

        private static bool TryInstantiatePlacedMarkerModel(string resourcesPath, Transform parent, Vector3 localPosition, Quaternion localRotation, float localScale, bool logBounds, bool alignToBottomFootprint)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcesPath);
            if (prefab == null)
            {
                return false;
            }

            GameObject instance = Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = Vector3.one * localScale;
            AlignMarkerBottom(instance.transform, localPosition, alignToBottomFootprint);
            LogMarkerBounds(resourcesPath, instance.transform, logBounds);
            RemoveColliders(instance);
            OptimizeMarkerRenderers(instance);
            return true;
        }

        private static void AlignMarkerBottom(Transform instance, Vector3 anchorLocalPosition, bool alignToBottomFootprint)
        {
            Transform boundsSpace = instance.parent != null ? instance.parent : instance;
            if (alignToBottomFootprint && TryGetBottomFootprintCenterInSpace(instance, boundsSpace, out Vector3 footprintBottomCenter))
            {
                instance.localPosition += anchorLocalPosition - footprintBottomCenter;
                return;
            }

            if (!TryGetRendererBoundsInSpace(instance, boundsSpace, out Bounds bounds))
            {
                return;
            }

            Vector3 currentBottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            instance.localPosition += anchorLocalPosition - currentBottomCenter;
        }

        private static bool TryGetBottomFootprintCenterInSpace(Transform root, Transform boundsSpace, out Vector3 bottomCenter)
        {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            bottomCenter = Vector3.zero;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            bool hasVertex = false;

            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 point = boundsSpace.InverseTransformPoint(meshFilter.transform.TransformPoint(vertices[i]));
                    minY = Mathf.Min(minY, point.y);
                    maxY = Mathf.Max(maxY, point.y);
                    hasVertex = true;
                }
            }

            if (!hasVertex)
            {
                return false;
            }

            float height = Mathf.Max(0.001f, maxY - minY);
            float bottomLimit = minY + Mathf.Max(0.04f, height * 0.14f);
            Bounds bottomBounds = new Bounds();
            bool hasBottom = false;

            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 point = boundsSpace.InverseTransformPoint(meshFilter.transform.TransformPoint(vertices[i]));
                    if (point.y > bottomLimit)
                    {
                        continue;
                    }

                    if (!hasBottom)
                    {
                        bottomBounds = new Bounds(point, Vector3.zero);
                        hasBottom = true;
                    }
                    else
                    {
                        bottomBounds.Encapsulate(point);
                    }
                }
            }

            if (!hasBottom)
            {
                return false;
            }

            bottomCenter = new Vector3(bottomBounds.center.x, bottomBounds.min.y, bottomBounds.center.z);
            return true;
        }

        private static bool TryInstantiateGlbMarkerModel(string resourcesPath, Transform parent, Vector3 localPosition, Quaternion localRotation, float targetHeight, Quaternion[] orientationCandidates, bool logBounds)
        {
            if (TryInstantiateMarkerModel(resourcesPath, parent, localPosition, localRotation, targetHeight, orientationCandidates, logBounds))
            {
                return true;
            }

            string glbPath = GetGlbAssetPath(resourcesPath);
            if (string.IsNullOrEmpty(glbPath) || FailedGlbImports.Contains(glbPath))
            {
                return false;
            }

            if (!TryGetCachedGlbImport(glbPath, out GltfImport gltfImport))
            {
                FailedGlbImports.Add(glbPath);
                return false;
            }

            GameObject instance = new GameObject(Path.GetFileNameWithoutExtension(glbPath));
            instance.transform.SetParent(parent, false);
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = Vector3.one;

            bool instantiated;
#pragma warning disable CS0618
            instantiated = gltfImport.InstantiateMainScene(instance.transform);
#pragma warning restore CS0618
            if (!instantiated)
            {
                DestroyMarkerInstance(instance);
                FailedGlbImports.Add(glbPath);
                return false;
            }

            ApplyBestMarkerOrientation(instance.transform, localRotation, orientationCandidates);

            NormalizeMarkerModel(instance.transform, localPosition, targetHeight);
            LogMarkerBounds(resourcesPath, instance.transform, logBounds);
            RemoveColliders(instance);
            OptimizeMarkerRenderers(instance);
            return true;
        }

        private static void OptimizeMarkerRenderers(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                OptimizeRendererForRuntimeTile(renderers[i]);
            }
        }

        private static void OptimizeRendererForRuntimeTile(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = true;
        }

        private static bool TryGetCachedGlbImport(string glbPath, out GltfImport gltfImport)
        {
            if (CachedGlbImports.TryGetValue(glbPath, out gltfImport))
            {
                return true;
            }

            try
            {
                gltfImport = new GltfImport();
                Uri baseUri = new Uri(glbPath);
                bool loaded = gltfImport.LoadFile(glbPath, baseUri).GetAwaiter().GetResult();
                if (!loaded)
                {
                    gltfImport.Dispose();
                    gltfImport = null;
                    return false;
                }

                CachedGlbImports[glbPath] = gltfImport;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load GLB marker '{glbPath}': {exception.Message}");
                gltfImport = null;
                return false;
            }
        }

        private static string GetGlbAssetPath(string resourcesPath)
        {
            string relativePath = resourcesPath.Replace('/', Path.DirectorySeparatorChar) + ".glb";
            string fullPath = Path.Combine(Application.dataPath, "CyberMinefield", "Resources", relativePath);
            return File.Exists(fullPath) ? fullPath : string.Empty;
        }

        private static void DestroyMarkerInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        private static void SetPrimitiveColor(GameObject primitive, Color color)
        {
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = EnsureRuntimePrimitiveMaterial();
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }

                OptimizeRendererForRuntimeTile(renderer);

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                renderer.SetPropertyBlock(block);
            }
        }

        private static Material EnsureRuntimePrimitiveMaterial()
        {
            if (runtimePrimitiveMaterial != null)
            {
                return runtimePrimitiveMaterial;
            }

            runtimePrimitiveMaterial = Resources.Load<Material>("Materials/CyberTileRuntime");
            if (runtimePrimitiveMaterial != null)
            {
                runtimePrimitiveMaterial.enableInstancing = true;
                return runtimePrimitiveMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            runtimePrimitiveMaterial = new Material(shader)
            {
                name = "CyberMinefield Runtime Primitive Material",
                enableInstancing = true
            };

            return runtimePrimitiveMaterial;
        }

        private static bool TryInstantiateMarkerModel(string resourcesPath, Transform parent, Vector3 localPosition, Quaternion localRotation, float targetHeight, Quaternion[] orientationCandidates, bool logBounds)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcesPath);
            if (prefab == null)
            {
                return false;
            }

            GameObject instance = Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = Vector3.one;
            ApplyBestMarkerOrientation(instance.transform, localRotation, orientationCandidates);

            NormalizeMarkerModel(instance.transform, localPosition, targetHeight);
            LogMarkerBounds(resourcesPath, instance.transform, logBounds);
            RemoveColliders(instance);
            return true;
        }

        private static void ApplyBestMarkerOrientation(Transform instance, Quaternion baseRotation, Quaternion[] orientationCandidates)
        {
            if (orientationCandidates == null || orientationCandidates.Length == 0)
            {
                return;
            }

            Transform boundsSpace = instance.parent != null ? instance.parent : instance;
            Quaternion bestRotation = baseRotation;
            float bestScore = float.NegativeInfinity;

            foreach (Quaternion candidate in orientationCandidates)
            {
                instance.localRotation = baseRotation * candidate;
                if (!TryGetRendererBoundsInSpace(instance, boundsSpace, out Bounds candidateBounds))
                {
                    continue;
                }

                float footprint = Mathf.Max(candidateBounds.size.x, candidateBounds.size.z);
                float uprightRatio = candidateBounds.size.y / Mathf.Max(0.001f, footprint);
                float score = candidateBounds.size.y * 2f + uprightRatio * 0.5f - footprint * 0.05f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestRotation = instance.localRotation;
                }
            }

            instance.localRotation = bestRotation;
        }

        private static void LogMarkerBounds(string resourcesPath, Transform instance, bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            Transform boundsSpace = instance.parent != null ? instance.parent : instance;
            if (!TryGetRendererBoundsInSpace(instance, boundsSpace, out Bounds bounds))
            {
                Debug.LogWarning($"Marker {resourcesPath} has no renderer bounds after import.");
                return;
            }

            Debug.Log($"Marker {resourcesPath} bounds after orientation: size={bounds.size}, rotation={instance.localRotation.eulerAngles}");
        }

        private static void NormalizeMarkerModel(Transform instance, Vector3 baseLocalPosition, float targetHeight)
        {
            Transform boundsSpace = instance.parent != null ? instance.parent : instance;
            if (!TryGetRendererBoundsInSpace(instance, boundsSpace, out Bounds localBounds) || localBounds.size.y <= 0.001f)
            {
                instance.localPosition = baseLocalPosition;
                instance.localScale = Vector3.one * Mathf.Max(0.001f, targetHeight);
                return;
            }

            float largestAxis = Mathf.Max(localBounds.size.x, localBounds.size.y, localBounds.size.z);
            float measuredHeight = localBounds.size.y < largestAxis * 0.55f ? largestAxis : localBounds.size.y;
            float scale = targetHeight / measuredHeight;
            instance.localScale = Vector3.one * scale;

            if (!TryGetRendererBoundsInSpace(instance, boundsSpace, out Bounds scaledBounds))
            {
                instance.localPosition = baseLocalPosition;
                return;
            }

            Vector3 centeredBottom = new Vector3(scaledBounds.center.x, scaledBounds.min.y, scaledBounds.center.z);
            instance.localPosition += baseLocalPosition - centeredBottom;
        }

        private static bool TryGetRendererBoundsInSpace(Transform root, Transform boundsSpace, out Bounds localBounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            localBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            foreach (Renderer renderer in renderers)
            {
                Bounds worldBounds = renderer.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;

                for (int xIndex = 0; xIndex <= 1; xIndex++)
                {
                    for (int yIndex = 0; yIndex <= 1; yIndex++)
                    {
                        for (int zIndex = 0; zIndex <= 1; zIndex++)
                        {
                            Vector3 corner = new Vector3(
                                xIndex == 0 ? min.x : max.x,
                                yIndex == 0 ? min.y : max.y,
                                zIndex == 0 ? min.z : max.z);
                            Vector3 localCorner = boundsSpace.InverseTransformPoint(corner);

                            if (!hasBounds)
                            {
                                localBounds = new Bounds(localCorner, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                localBounds.Encapsulate(localCorner);
                            }
                        }
                    }
                }
            }

            return hasBounds;
        }

        private static void RemoveColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }
        }

        private void SetLabel(string text, Color color)
        {
            EnsureLabel();
            ApplyPixelFont(label);
            label.text = text;
            label.color = color;
        }

        private static void ApplyPixelFont(TextMesh textMesh)
        {
            if (textMesh == null)
            {
                return;
            }

            if (pixelFont == null)
            {
                pixelFont = Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
            }

            if (pixelFont == null)
            {
                return;
            }

            textMesh.font = pixelFont;
            MeshRenderer meshRenderer = textMesh.GetComponent<MeshRenderer>();
            if (meshRenderer != null && pixelFont.material != null)
            {
                meshRenderer.material = pixelFont.material;
            }
        }

        private void SetLabelVisible(bool visible)
        {
            EnsureLabel();
            label.gameObject.SetActive(visible);
        }

        private void SetTutorialHintVisible(bool visible)
        {
            EnsureTutorialHintLabel();
            tutorialHintLabel.gameObject.SetActive(visible);
            tutorialHintLabel.text = tutorialHintText;
            tutorialHintLabel.color = tutorialHintColor;
        }

        private IEnumerator PulseWinColor(int sequenceIndex)
        {
            float delay = Mathf.Min(0.75f, sequenceIndex * 0.0035f);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            float elapsed = 0f;
            Color baseColor = currentTileColor;

            while (elapsed < 0.9f)
            {
                elapsed += Time.deltaTime;
                float pulse = Mathf.PingPong(elapsed * 7f, 1f);
                ApplyColor(Color.Lerp(baseColor, WinGlowColor, 0.45f + pulse * 0.35f));
                yield return null;
            }

            ApplyColor(WinGlowColor);
            winEffectCoroutine = null;
        }

        private void StopWinEffect()
        {
            if (winEffectCoroutine == null)
            {
                return;
            }

            StopCoroutine(winEffectCoroutine);
            winEffectCoroutine = null;
        }

        private string BuildRevealMessage()
        {
            if (hasDanger && hasDefuser)
            {
                return $"Danger neutralized at ({x}, {y}).";
            }

            if (hasDanger)
            {
                return $"Danger triggered at ({x}, {y}).";
            }

            return $"Safe tile revealed at ({x}, {y}) with {adjacentDangerCount} adjacent dangers.";
        }
    }
}
