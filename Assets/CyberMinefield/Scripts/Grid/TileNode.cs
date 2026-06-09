using System.Collections;
using UnityEngine;

namespace CyberMinefield.Grid
{
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

        [SerializeField] private int x;
        [SerializeField] private int y;
        [SerializeField] private bool hasDanger;
        [SerializeField] private bool isRevealed;
        [SerializeField] private bool hasDefuser;
        [SerializeField] private bool isExit;
        [SerializeField] private int adjacentDangerCount;

        private GridManager gridManager;
        private Renderer cachedRenderer;
        private TextMesh label;
        private GameObject defuserMarker;
        private TextMesh tutorialHintLabel;
        private string tutorialHintText = string.Empty;
        private Color tutorialHintColor = Color.white;
        private bool playerOccupying;
        private bool isMisflagRevealed;
        private Coroutine winEffectCoroutine;

        public int X => x;
        public int Y => y;
        public bool HasDanger => hasDanger;
        public bool IsMine => hasDanger;
        public bool IsRevealed => isRevealed;
        public bool HasDefuser => hasDefuser;
        public bool IsFlagged => hasDefuser;
        public bool IsExit => isExit;
        public int AdjacentDangerCount => adjacentDangerCount;
        public int AdjacentMineCount => adjacentDangerCount;
        public Vector2Int Coordinates => new Vector2Int(x, y);

        public void Initialize(int xCoordinate, int yCoordinate, GridManager owner)
        {
            x = xCoordinate;
            y = yCoordinate;
            gridManager = owner;
            hasDanger = false;
            isRevealed = false;
            hasDefuser = false;
            isExit = false;
            adjacentDangerCount = 0;
            playerOccupying = false;
            isMisflagRevealed = false;
            StopWinEffect();
            EnsureLabel();
            EnsureDefuserMarker();
            EnsureTutorialHintLabel();
            ApplyPresentation();
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
            isExit = false;
            adjacentDangerCount = 0;
            playerOccupying = false;
            isMisflagRevealed = false;
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

        public void RevealDangerForResult()
        {
            if (!hasDanger)
            {
                return;
            }

            isRevealed = true;
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

            if (isRevealed)
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
                    labelText = "!";
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
                defuserMarker.SetActive(hasDefuser);
            }
        }

        private void ApplyColor(Color color)
        {
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
            }

            if (cachedRenderer != null)
            {
                cachedRenderer.material.color = color;
            }
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
            tutorialHintLabel.gameObject.SetActive(false);
        }

        private void EnsureDefuserMarker()
        {
            if (defuserMarker != null)
            {
                return;
            }

            defuserMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            defuserMarker.name = "DefuserMarker";
            defuserMarker.transform.SetParent(transform, false);
            defuserMarker.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            defuserMarker.transform.localScale = new Vector3(0.32f, 0.08f, 0.32f);

            Collider markerCollider = defuserMarker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(markerCollider);
                }
                else
                {
                    DestroyImmediate(markerCollider);
                }
            }

            Renderer markerRenderer = defuserMarker.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                markerRenderer.material.color = new Color(0.68f, 1f, 0.96f);
            }

            defuserMarker.SetActive(false);
        }

        private void SetLabel(string text, Color color)
        {
            EnsureLabel();
            label.text = text;
            label.color = color;
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
            float delay = sequenceIndex * 0.025f;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            float elapsed = 0f;
            Color baseColor = cachedRenderer != null ? cachedRenderer.material.color : SafeRevealedColor;

            while (elapsed < 1.8f)
            {
                elapsed += Time.deltaTime;
                float pulse = Mathf.PingPong(elapsed * 4f, 1f);
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
