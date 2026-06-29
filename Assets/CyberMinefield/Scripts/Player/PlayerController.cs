using CyberMinefield.Audio;
using CyberMinefield.Core;
using CyberMinefield.Grid;
using UnityEngine;

namespace CyberMinefield.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        private const string CurrentVisualResourcePath = "Models/CartoonCharacter/cartoon_character_player";
        private const string LegacyFbxVisualResourcePath = "Models/CartoonCharacter/tripo_convert_dd1119ff-37b7-4fc5-a573-a8fe51240daf";
        private const string LegacyObjVisualResourcePath = "Models/CartoonCharacter/tripo_convert_c76a24f7-7e9f-4656-a6a3-010f361cea3f";
        private const float CurrentVisualYawOffset = 90f;

        [SerializeField, Range(0.5f, 4f)] private float moveTilesPerSecond = 2.9f;
        [SerializeField, Range(2f, 18f)] private float acceleration = 9f;
        [SerializeField, Range(4f, 24f)] private float turnSpeed = 12f;
        [SerializeField] private float jumpHeight = 1.35f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float groundCheckDistance = 1.6f;
        [SerializeField] private float fallRestartHeight = -8f;
        [SerializeField] private Color bodyColor = new Color(0.92f, 0.86f, 0.2f);
        [SerializeField] private string visualResourcePath = CurrentVisualResourcePath;
        [SerializeField] private Vector3 visualRotationOffset = new Vector3(0f, CurrentVisualYawOffset, 0f);
        [SerializeField, Range(0.5f, 3f)] private float visualTargetHeight = 1.55f;
        [SerializeField] private string idleAnimationState = "Idle";
        [SerializeField] private string walkAnimationState = "Walk";
        [SerializeField] private string jumpAnimationState = "Jump";

        private CharacterController characterController;
        private GridManager gridManager;
        private GameManager gameManager;
        private InputManager inputManager;
        private AudioManager audioManager;
        private Transform visualRoot;
        private Animator visualAnimator;
        private AnimationClip idleClip;
        private AnimationClip walkClip;
        private AnimationClip jumpClip;
        private AnimationClip currentClip;
        private Vector3 horizontalVelocity;
        private Vector3 verticalVelocity;
        private Vector3 visualBaseLocalPosition;
        private Quaternion visualBaseLocalRotation = Quaternion.identity;
        private Vector2Int currentTile = new Vector2Int(int.MinValue, int.MinValue);
        private TileNode occupiedTile;
        private bool inputEnabled = true;
        private bool hasAnimatorController;
        private bool hasSampledAnimation;
        private VisualMotionState visualMotionState = VisualMotionState.Idle;
        private bool jumpAnimationRequested;
        private float proceduralAnimTime;
        private float sampledClipTime;

        public Vector2Int CurrentTile => currentTile;

        private void Awake()
        {
            EnsureController();
            EnsureVisual();
        }

        private void Update()
        {
            if (gameManager != null && transform.position.y <= fallRestartHeight)
            {
                gameManager.RestartLevel();
                return;
            }

            if (!inputEnabled || gameManager == null || !gameManager.CanAcceptGameplayInput())
            {
                audioManager?.StopFootsteps();
                UpdateVisualAnimation(false, characterController != null && characterController.isGrounded);
                return;
            }

            MovePlayer();
            RevealTileUnderPlayer();
        }

        public void Configure(GridManager grid, GameManager manager, InputManager input)
        {
            gridManager = grid;
            gameManager = manager;
            inputManager = input;
            audioManager = FindAnyObjectByType<AudioManager>();
            moveTilesPerSecond = Mathf.Max(moveTilesPerSecond, 2.9f);
            SetInputEnabled(true);
        }

        public void BeginAt(Vector2Int startCoordinates)
        {
            EnsureController();

            Vector3 startPosition = gridManager.GetWorldPosition(startCoordinates);
            float controllerFootOffset = characterController.height * 0.5f - characterController.center.y;
            float spawnLift = gridManager.TileSurfaceHeight + controllerFootOffset + characterController.skinWidth + 0.02f;

            bool wasControllerEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.position = startPosition + Vector3.up * spawnLift;
            characterController.enabled = wasControllerEnabled;

            currentTile = startCoordinates;
            if (gridManager.TryGetTile(startCoordinates, out TileNode spawnTile))
            {
                SetOccupiedTile(spawnTile);
            }
            else
            {
                SetOccupiedTile(null);
            }

            horizontalVelocity = Vector3.zero;
            verticalVelocity = Vector3.zero;
            jumpAnimationRequested = false;
            gameObject.tag = "Player";
            SetInputEnabled(true);
        }

        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
            if (!value)
            {
                audioManager?.StopFootsteps();
            }
        }

        private void MovePlayer()
        {
            Vector2 moveInput = inputManager != null ? inputManager.MoveInput : Vector2.zero;
            Vector3 moveDirection = BuildCameraRelativeMove(moveInput);
            float worldMoveSpeed = gridManager != null
                ? gridManager.TileSpacing * moveTilesPerSecond
                : moveTilesPerSecond;
            Vector3 desiredHorizontalVelocity = moveDirection * worldMoveSpeed;

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                desiredHorizontalVelocity,
                acceleration * worldMoveSpeed * Time.deltaTime);

            bool isGrounded = characterController.isGrounded;
            if (isGrounded && verticalVelocity.y < 0f)
            {
                verticalVelocity.y = -2f;
            }

            if (isGrounded && inputManager != null && inputManager.ConsumeJumpPressed())
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                audioManager?.PlayJump();
                jumpAnimationRequested = true;
                SetAnimatorState(VisualMotionState.Jump, true);
            }

            verticalVelocity.y += gravity * Time.deltaTime;

            Vector3 movement = (horizontalVelocity + verticalVelocity) * Time.deltaTime;
            characterController.Move(movement);
            bool isMoving = isGrounded && moveDirection.sqrMagnitude > 0.001f;
            audioManager?.PlayFootsteps(isMoving);
            UpdateVisualAnimation(isMoving, characterController.isGrounded);

            if (horizontalVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(horizontalVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime);
            }
        }

        private Vector3 BuildCameraRelativeMove(Vector2 moveInput)
        {
            return inputManager != null
                ? inputManager.BuildCameraRelativeMove(moveInput)
                : new Vector3(moveInput.x, 0f, moveInput.y);
        }

        private void RevealTileUnderPlayer()
        {
            if (gridManager == null)
            {
                return;
            }

            if (!characterController.isGrounded)
            {
                return;
            }

            Vector3 localPosition = gridManager.transform.InverseTransformPoint(transform.position);
            Vector2Int coordinateFromPosition = new Vector2Int(
                Mathf.RoundToInt(localPosition.x / gridManager.TileSpacing),
                Mathf.RoundToInt(localPosition.z / gridManager.TileSpacing));

            if (gridManager.IsInsideBoard(coordinateFromPosition))
            {
                RevealTileIfChanged(coordinateFromPosition);
                return;
            }

            Vector3 rayOrigin = transform.position + Vector3.up * 0.2f;
            if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide))
            {
                return;
            }

            TileNode tile = hit.collider.GetComponent<TileNode>();
            if (tile == null)
            {
                tile = hit.collider.GetComponentInParent<TileNode>();
            }

            if (tile == null || tile.Coordinates == currentTile)
            {
                return;
            }

            RevealTileIfChanged(tile.Coordinates);
        }

        private void RevealTileIfChanged(Vector2Int coordinates)
        {
            if (coordinates == currentTile)
            {
                return;
            }

            if (gridManager.TryGetTile(coordinates, out TileNode tile))
            {
                SetOccupiedTile(tile);
            }

            currentTile = coordinates;
            gridManager.NotifyTileEntered(currentTile);
            gridManager.RevealTile(currentTile);
        }

        private void SetOccupiedTile(TileNode tile)
        {
            if (occupiedTile == tile)
            {
                return;
            }

            if (occupiedTile != null)
            {
                occupiedTile.SetPlayerOccupying(false);
            }

            occupiedTile = tile;

            if (occupiedTile != null)
            {
                occupiedTile.SetPlayerOccupying(true);
            }
        }

        private void EnsureController()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
            }

            characterController.height = 1.65f;
            characterController.radius = 0.32f;
            characterController.center = new Vector3(0f, 0.82f, 0f);
            characterController.stepOffset = 0.35f;
        }

        private void EnsureVisual()
        {
            if (visualRoot != null)
            {
                return;
            }

            RemoveOldVisuals();
            GameObject visualPrefab = LoadVisualPrefab();
            if (visualPrefab != null)
            {
                GameObject visualInstance = Instantiate(visualPrefab, transform);
                visualInstance.name = "CyberAnalystVisual";
                visualRoot = visualInstance.transform;
                visualRoot.localPosition = Vector3.zero;
                NormalizeVisualRotationOffset();
                visualRoot.localRotation = Quaternion.Euler(visualRotationOffset);
                visualRoot.localScale = Vector3.one;
                RemoveVisualColliders(visualRoot);
                FitVisualToController();

                visualAnimator = visualRoot.GetComponentInChildren<Animator>();
                hasAnimatorController = visualAnimator != null && visualAnimator.runtimeAnimatorController != null;
                LoadAnimationClips();
                SetAnimatorState(VisualMotionState.Idle);
                return;
            }

            CreateFallbackCapsule();
        }

        private GameObject LoadVisualPrefab()
        {
            if (string.IsNullOrWhiteSpace(visualResourcePath)
                || visualResourcePath == LegacyFbxVisualResourcePath
                || visualResourcePath == LegacyObjVisualResourcePath
                || visualResourcePath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)
                || visualResourcePath.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase)
                || visualResourcePath.Contains("dd1119ff")
                || visualResourcePath.Contains("c76a24f7"))
            {
                visualResourcePath = CurrentVisualResourcePath;
            }

            GameObject visualPrefab = Resources.Load<GameObject>(visualResourcePath);
            if (visualPrefab != null)
            {
                return visualPrefab;
            }

            visualPrefab = Resources.Load<GameObject>(CurrentVisualResourcePath);
            if (visualPrefab != null)
            {
                visualResourcePath = CurrentVisualResourcePath;
                return visualPrefab;
            }

            GameObject[] candidates = Resources.LoadAll<GameObject>("Models/CartoonCharacter");
            foreach (GameObject candidate in candidates)
            {
                if (candidate != null && candidate.name.Contains("cartoon_character_player"))
                {
                    visualResourcePath = CurrentVisualResourcePath;
                    return candidate;
                }
            }

            if (candidates.Length > 0)
            {
                Debug.LogWarning($"Player visual path '{visualResourcePath}' was not found. Using '{candidates[0].name}' from CartoonCharacter resources.", this);
                return candidates[0];
            }

            Debug.LogWarning($"Player visual '{visualResourcePath}' could not be loaded. Falling back to capsule.", this);
            return null;
        }

        private void NormalizeVisualRotationOffset()
        {
            if (visualResourcePath == CurrentVisualResourcePath
                || visualResourcePath.Contains("cartoon_character_player"))
            {
                visualRotationOffset = new Vector3(0f, CurrentVisualYawOffset, 0f);
            }
        }

        private void RemoveOldVisuals()
        {
            RemoveChild("CyberAnalystBody");
            RemoveChild("CyberAnalystVisual");
        }

        private void RemoveChild(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        private void CreateFallbackCapsule()
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "CyberAnalystBody";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
            visualRoot = body.transform;
            visualBaseLocalPosition = body.transform.localPosition;
            visualBaseLocalRotation = body.transform.localRotation;

            Renderer renderer = body.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = bodyColor;
            }

            Collider collider = body.GetComponent<Collider>();
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
        }

        private static void RemoveVisualColliders(Transform root)
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

        private void FitVisualToController()
        {
            if (visualRoot == null || !TryGetVisualBounds(out Bounds bounds) || bounds.size.y <= 0.001f)
            {
                visualBaseLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
                visualBaseLocalRotation = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
                return;
            }

            float scale = visualTargetHeight / bounds.size.y;
            visualRoot.localScale *= scale;

            if (TryGetVisualBounds(out bounds))
            {
                Vector3 localBottomCenter = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                visualRoot.localPosition -= new Vector3(localBottomCenter.x, localBottomCenter.y, localBottomCenter.z);
            }

            visualBaseLocalPosition = visualRoot.localPosition;
            visualBaseLocalRotation = visualRoot.localRotation;
        }

        private bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            if (visualRoot == null)
            {
                return false;
            }

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        private void UpdateVisualAnimation(bool isMoving, bool isGrounded)
        {
            EnsureVisual();
            if (isGrounded && visualMotionState == VisualMotionState.Jump)
            {
                jumpAnimationRequested = false;
            }

            VisualMotionState targetState = jumpAnimationRequested && !isGrounded
                ? VisualMotionState.Jump
                : isMoving
                    ? VisualMotionState.Walk
                    : VisualMotionState.Idle;

            SetAnimatorState(targetState);
            if (!UpdateSampledClipAnimation())
            {
                UpdateProceduralAnimation(isMoving, isGrounded);
            }
        }

        private void SetAnimatorState(VisualMotionState state, bool restart = false)
        {
            if (visualMotionState == state && !restart)
            {
                if (!hasAnimatorController && hasSampledAnimation && currentClip == null)
                {
                    SetSampledClipState(state, false);
                }

                return;
            }

            visualMotionState = state;

            if (!hasAnimatorController || visualAnimator == null)
            {
                SetSampledClipState(state, restart);
                return;
            }

            string stateName = GetAnimatorStateName(state);
            int stateHash = Animator.StringToHash(stateName);
            if (visualAnimator.HasState(0, stateHash))
            {
                visualAnimator.CrossFade(stateHash, 0.08f);
            }
        }

        private void LoadAnimationClips()
        {
            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(visualResourcePath);
            if (clips == null || clips.Length == 0)
            {
                hasSampledAnimation = false;
                Debug.LogWarning("No animation clips found in the player FBX. The player will use procedural movement only.", this);
                return;
            }

            idleClip = FindClip(clips, "idle") ?? FindClip(clips, "stand") ?? FindClip(clips, "breath");
            walkClip = FindClip(clips, "walk") ?? FindClip(clips, "run");
            jumpClip = FindClip(clips, "jump") ?? FindClip(clips, "hop");

            if (idleClip == null)
            {
                Debug.Log("No idle animation clip found for player. The character will stay still while idle.", this);
            }

            hasSampledAnimation = idleClip != null || walkClip != null || jumpClip != null;
        }

        private static AnimationClip FindClip(AnimationClip[] clips, string keyword)
        {
            foreach (AnimationClip clip in clips)
            {
                if (clip != null && clip.name.ToLowerInvariant().Contains(keyword))
                {
                    return clip;
                }
            }

            return null;
        }

        private static AnimationClip FirstClipExcluding(AnimationClip[] clips, params AnimationClip[] excluded)
        {
            foreach (AnimationClip clip in clips)
            {
                if (clip == null)
                {
                    continue;
                }

                bool isExcluded = false;
                foreach (AnimationClip excludedClip in excluded)
                {
                    if (clip == excludedClip)
                    {
                        isExcluded = true;
                        break;
                    }
                }

                if (!isExcluded)
                {
                    return clip;
                }
            }

            return null;
        }

        private void SetSampledClipState(VisualMotionState state, bool restart)
        {
            if (!hasSampledAnimation)
            {
                return;
            }

            AnimationClip nextClip = GetSampledClip(state);
            if (nextClip == null)
            {
                nextClip = state == VisualMotionState.Idle ? idleClip : null;
            }

            if (nextClip != currentClip || restart)
            {
                currentClip = nextClip;
                sampledClipTime = 0f;
            }
        }

        private AnimationClip GetSampledClip(VisualMotionState state)
        {
            switch (state)
            {
                case VisualMotionState.Walk:
                    return walkClip;
                case VisualMotionState.Jump:
                    return jumpClip;
                default:
                    return idleClip;
            }
        }

        private bool UpdateSampledClipAnimation()
        {
            if (visualRoot == null || hasAnimatorController || currentClip == null)
            {
                return false;
            }

            sampledClipTime += Time.deltaTime;
            float clipLength = Mathf.Max(0.01f, currentClip.length);
            bool shouldLoop = visualMotionState != VisualMotionState.Jump;
            float sampleTime = shouldLoop
                ? Mathf.Repeat(sampledClipTime, clipLength)
                : Mathf.Min(sampledClipTime, clipLength);

            currentClip.SampleAnimation(visualRoot.gameObject, sampleTime);
            visualRoot.localPosition = visualBaseLocalPosition;
            visualRoot.localRotation = visualBaseLocalRotation;
            if (visualMotionState == VisualMotionState.Jump && sampledClipTime >= clipLength)
            {
                jumpAnimationRequested = false;
            }

            return true;
        }

        private string GetAnimatorStateName(VisualMotionState state)
        {
            switch (state)
            {
                case VisualMotionState.Walk:
                    return walkAnimationState;
                case VisualMotionState.Jump:
                    return jumpAnimationState;
                default:
                    return idleAnimationState;
            }
        }

        private void UpdateProceduralAnimation(bool isMoving, bool isGrounded)
        {
            if (visualRoot == null || hasAnimatorController)
            {
                return;
            }

            proceduralAnimTime += Time.deltaTime;
            Vector3 targetPosition = visualBaseLocalPosition;
            Quaternion targetRotation = visualBaseLocalRotation;

            if (jumpAnimationRequested && !isGrounded)
            {
                float jumpPulse = Mathf.Sin(Time.time * 10f) * 0.015f;
                targetPosition += Vector3.up * (0.08f + jumpPulse);
                targetRotation *= Quaternion.Euler(-7f, 0f, 0f);
            }
            else if (isMoving)
            {
                float walk = Mathf.Sin(proceduralAnimTime * 12f);
                float sway = Mathf.Sin(proceduralAnimTime * 6f);
                targetPosition += Vector3.up * (Mathf.Abs(walk) * 0.055f);
                targetRotation *= Quaternion.Euler(0f, 0f, sway * 4f);
            }

            visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, targetPosition, 18f * Time.deltaTime);
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, targetRotation, 18f * Time.deltaTime);
        }

        private enum VisualMotionState
        {
            Idle,
            Walk,
            Jump
        }
    }
}
