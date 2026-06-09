using CyberMinefield.Grid;
using CyberMinefield.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CyberMinefield.Core
{
    public sealed class InputManager : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private float cameraDistance = 8f;
        [SerializeField] private float cameraHeight = 4f;
        [SerializeField] private float cameraRotateSpeed = 0.25f;
        [SerializeField] private float cameraZoomSpeed = 1.2f;
        [SerializeField] private float minCameraDistance = 4.2f;
        [SerializeField] private float maxCameraDistance = 16f;
        [SerializeField] private float minPitch = 18f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private bool cameraLocked;
        [SerializeField] private float lockedCameraHeight = 13f;
        [SerializeField] private float lockedZoomScale = 1f;

        private float cameraYaw = 45f;
        private float cameraPitch = 42f;
        private bool jumpPressed;
        private bool wasRightMouseHeld;
        private Vector2 lastMousePosition;
        private int lastCameraDragFrame = -1;
        private int lastDefuserToggleFrame = -1;
        private int lastZoomFrame = -1;

        public Vector2 MoveInput { get; private set; }
        public bool CameraLocked => cameraLocked;

        private void Awake()
        {
            ResolveCamera();
            InitializeCameraAnglesFromCurrentCamera();
        }

        private void Update()
        {
            ReadKeyboard();
            HandleLeftClickDefuser();
        }

        private void OnGUI()
        {
            Event currentEvent = Event.current;
            if (currentEvent == null || !CanAcceptMouseGameplayInput())
            {
                return;
            }

            if (!cameraLocked && currentEvent.type == EventType.MouseDrag && currentEvent.button == 1)
            {
                ApplyCameraDrag(currentEvent.delta);
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.ScrollWheel
                && lastZoomFrame != Time.frameCount
                && !IsPointerOverActiveButton(GuiToScreenPoint(currentEvent.mousePosition)))
            {
                ApplyZoom(-currentEvent.delta.y);
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.MouseDown
                && currentEvent.button == 0
                && lastDefuserToggleFrame != Time.frameCount
                && !IsPointerOverActiveButton(GuiToScreenPoint(currentEvent.mousePosition)))
            {
                if (TryToggleDefuserAtScreenPosition(GuiToScreenPoint(currentEvent.mousePosition)))
                {
                    currentEvent.Use();
                }
            }
        }

        private void LateUpdate()
        {
            UpdateCameraOrbit();
        }

        public void Configure(GridManager grid, PlayerController player, GameManager manager)
        {
            gridManager = grid;
            playerController = player;
            gameManager = manager;
            ResolveCamera();
            cameraLocked = false;
            cameraYaw = 45f;
            cameraPitch = 48f;
            cameraDistance = Mathf.Clamp(cameraDistance, minCameraDistance, maxCameraDistance);
            lockedZoomScale = 1f;
            wasRightMouseHeld = false;
            lastMousePosition = GetMousePosition();
            ApplyCameraModeImmediately();
        }

        public void ToggleCameraLock()
        {
            SetCameraLocked(!cameraLocked);
        }

        public void SetCameraLocked(bool value)
        {
            cameraLocked = value;
            wasRightMouseHeld = false;
            lastMousePosition = GetMousePosition();
            ApplyCameraModeImmediately();
            Debug.Log(cameraLocked ? "Camera lock enabled: top-down view." : "Camera lock disabled: 3D orbit view.", this);
        }

        public Vector3 BuildCameraRelativeMove(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }

            if (cameraLocked)
            {
                return new Vector3(moveInput.x, 0f, moveInput.y);
            }

            Transform cameraTransform = targetCamera != null ? targetCamera.transform : Camera.main != null ? Camera.main.transform : null;
            if (cameraTransform == null)
            {
                return new Vector3(moveInput.x, 0f, moveInput.y);
            }

            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return forward * moveInput.y + right * moveInput.x;
        }

        public bool ConsumeJumpPressed()
        {
            bool wasPressed = jumpPressed;
            jumpPressed = false;
            return wasPressed;
        }

        private void ReadKeyboard()
        {
            MoveInput = Vector2.zero;
            jumpPressed = false;

            if (gameManager != null && !gameManager.CanAcceptGameplayInput())
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                float horizontal = 0f;
                float vertical = 0f;

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    horizontal -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    horizontal += 1f;
                }

                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    vertical += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    vertical -= 1f;
                }

                MoveInput = new Vector2(horizontal, vertical);
                jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            MoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            jumpPressed = Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private void HandleLeftClickDefuser()
        {
            if (!CanAcceptMouseGameplayInput())
            {
                return;
            }

            if (!WasLeftMousePressed() || lastDefuserToggleFrame == Time.frameCount)
            {
                return;
            }

            TryToggleDefuserAtScreenPosition(GetMousePosition());
        }

        private bool TryToggleDefuserAtScreenPosition(Vector2 screenPosition)
        {
            if (gridManager == null || IsPointerOverActiveButton(screenPosition))
            {
                return false;
            }

            ResolveCamera();
            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera == null)
            {
                return false;
            }

            TileNode tile = FindTileAtScreenPosition(camera, screenPosition);

            if (tile != null)
            {
                lastDefuserToggleFrame = Time.frameCount;
                bool changed = gridManager.ToggleDefuser(tile.Coordinates);
                Debug.Log(changed
                    ? $"Left click toggled defuser at ({tile.X}, {tile.Y})."
                    : $"Left click found tile ({tile.X}, {tile.Y}) but defuser was not toggled.", tile);
                return changed;
            }

            Debug.Log("Left click did not hit a tile for defuser placement.", this);
            return false;
        }

        private void UpdateCameraOrbit()
        {
            if (targetCamera == null)
            {
                ResolveCamera();
            }

            if (targetCamera == null)
            {
                return;
            }

            bool isRightMouseHeld = IsRightMouseHeld();
            Vector2 currentMousePosition = GetMousePosition();
            ApplyZoom(GetMouseScrollDelta());

            if (!cameraLocked
                && isRightMouseHeld
                && lastCameraDragFrame != Time.frameCount
                && !IsPointerOverActiveButton(currentMousePosition))
            {
                Vector2 delta = wasRightMouseHeld
                    ? currentMousePosition - lastMousePosition
                    : Vector2.zero;

                if (delta.sqrMagnitude < 0.001f)
                {
                    delta = GetMouseDelta();
                }

                delta.y = -delta.y;
                ApplyCameraDrag(delta);
            }

            wasRightMouseHeld = isRightMouseHeld;
            lastMousePosition = currentMousePosition;

            if (cameraLocked)
            {
                Vector3 lockedFocus = GetLockedCameraFocus();
                targetCamera.orthographic = true;
                targetCamera.orthographicSize = GetLockedCameraSize();
                targetCamera.transform.position = lockedFocus + Vector3.up * lockedCameraHeight;
                targetCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                return;
            }

            targetCamera.orthographic = false;
            Quaternion rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            Vector3 focus = GetCameraFocus();
            Vector3 offset = rotation * new Vector3(0f, 0f, -cameraDistance);
            targetCamera.transform.position = focus + offset + Vector3.up * cameraHeight * 0.15f;
            targetCamera.transform.LookAt(focus);
        }

        private void ApplyCameraDrag(Vector2 delta)
        {
            cameraYaw += delta.x * cameraRotateSpeed;
            cameraPitch = Mathf.Clamp(cameraPitch + delta.y * cameraRotateSpeed, minPitch, maxPitch);
            lastCameraDragFrame = Time.frameCount;
        }

        private void ApplyZoom(float scrollDelta)
        {
            if (Mathf.Abs(scrollDelta) < 0.001f || IsPointerOverActiveButton(GetMousePosition()))
            {
                return;
            }

            if (cameraLocked)
            {
                lockedZoomScale = Mathf.Clamp(lockedZoomScale - scrollDelta * 0.08f, 0.45f, 1.8f);
                lastZoomFrame = Time.frameCount;
                return;
            }

            cameraDistance = Mathf.Clamp(
                cameraDistance - scrollDelta * cameraZoomSpeed,
                minCameraDistance,
                maxCameraDistance);
            lastZoomFrame = Time.frameCount;
        }

        private TileNode FindTileAtScreenPosition(Camera camera, Vector2 screenPosition)
        {
            Ray ray = camera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.SphereCastAll(
                ray,
                0.08f,
                200f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            TileNode closestTile = null;
            float closestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                TileNode tile = hit.collider.GetComponent<TileNode>();
                if (tile == null)
                {
                    tile = hit.collider.GetComponentInParent<TileNode>();
                }

                if (tile != null && hit.distance < closestDistance)
                {
                    closestTile = tile;
                    closestDistance = hit.distance;
                }
            }

            if (closestTile != null)
            {
                return closestTile;
            }

            return FindTileFromBoardPlane(ray);
        }

        private TileNode FindTileFromBoardPlane(Ray ray)
        {
            if (gridManager == null)
            {
                return null;
            }

            Plane boardPlane = new Plane(Vector3.up, Vector3.zero);
            if (!boardPlane.Raycast(ray, out float enter))
            {
                return null;
            }

            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 localPoint = gridManager.transform.InverseTransformPoint(worldPoint);
            Vector2Int coordinates = new Vector2Int(
                Mathf.RoundToInt(localPoint.x / gridManager.TileSpacing),
                Mathf.RoundToInt(localPoint.z / gridManager.TileSpacing));

            return gridManager.TryGetTile(coordinates, out TileNode tile) ? tile : null;
        }

        private void ResolveCamera()
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                targetCamera = mainCamera;
                return;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            foreach (Camera camera in cameras)
            {
                if (camera.isActiveAndEnabled)
                {
                    targetCamera = camera;
                    return;
                }
            }
        }

        private Vector3 GetCameraFocus()
        {
            if (playerController != null && playerController.gameObject.activeInHierarchy)
            {
                return playerController.transform.position + Vector3.up * 1.1f;
            }

            if (gridManager != null)
            {
                return new Vector3(
                    (gridManager.Width - 1) * gridManager.TileSpacing * 0.5f,
                    0f,
                    (gridManager.Height - 1) * gridManager.TileSpacing * 0.5f);
            }

            return Vector3.zero;
        }

        private Vector3 GetLockedCameraFocus()
        {
            if (gridManager != null)
            {
                return new Vector3(
                    (gridManager.Width - 1) * gridManager.TileSpacing * 0.5f,
                    0f,
                    (gridManager.Height - 1) * gridManager.TileSpacing * 0.5f);
            }

            return GetCameraFocus();
        }

        private float GetLockedCameraSize()
        {
            if (gridManager == null)
            {
                return 6f;
            }

            float boardWidth = Mathf.Max(1f, gridManager.Width * gridManager.TileSpacing);
            float boardHeight = Mathf.Max(1f, gridManager.Height * gridManager.TileSpacing);
            return Mathf.Clamp(Mathf.Max(boardWidth, boardHeight) * 0.58f * lockedZoomScale, 2.5f, 24f);
        }

        private void ApplyCameraModeImmediately()
        {
            ResolveCamera();
            UpdateCameraOrbit();
        }

        private bool CanAcceptMouseGameplayInput()
        {
            return gridManager != null
                && (gameManager == null || gameManager.CanAcceptGameplayInput());
        }

        private void InitializeCameraAnglesFromCurrentCamera()
        {
            if (targetCamera == null)
            {
                return;
            }

            Vector3 euler = targetCamera.transform.eulerAngles;
            cameraYaw = euler.y;
            cameraPitch = Mathf.Clamp(NormalizePitch(euler.x), minPitch, maxPitch);
        }

        private static float NormalizePitch(float pitch)
        {
            return pitch > 180f ? pitch - 360f : pitch;
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

        private static bool IsRightMouseHeld()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                return mouse.rightButton.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        private static Vector2 GetMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                return mouse.delta.ReadValue();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 12f;
#else
            return Vector2.zero;
#endif
        }

        private static float GetMouseScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                return mouse.scroll.ReadValue().y / 120f;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        private static Vector3 GetMousePosition()
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
            return Vector3.zero;
#endif
        }

        private static Vector2 GuiToScreenPoint(Vector2 guiPosition)
        {
            return new Vector2(guiPosition.x, Screen.height - guiPosition.y);
        }

        private static bool IsPointerOverActiveButton(Vector2 screenPosition)
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude);
            foreach (Button button in buttons)
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                {
                    continue;
                }

                RectTransform rectTransform = button.transform as RectTransform;
                if (rectTransform == null)
                {
                    continue;
                }

                Canvas canvas = button.GetComponentInParent<Canvas>();
                Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;

                if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
