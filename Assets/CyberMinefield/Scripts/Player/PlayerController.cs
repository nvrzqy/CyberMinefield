using CyberMinefield.Core;
using CyberMinefield.Grid;
using UnityEngine;

namespace CyberMinefield.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField, Range(0.5f, 4f)] private float moveTilesPerSecond = 2.9f;
        [SerializeField, Range(2f, 18f)] private float acceleration = 9f;
        [SerializeField, Range(4f, 24f)] private float turnSpeed = 12f;
        [SerializeField] private float jumpHeight = 1.35f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float groundCheckDistance = 1.6f;
        [SerializeField] private float fallRestartHeight = -8f;
        [SerializeField] private Color bodyColor = new Color(0.92f, 0.86f, 0.2f);

        private CharacterController characterController;
        private GridManager gridManager;
        private GameManager gameManager;
        private InputManager inputManager;
        private Vector3 horizontalVelocity;
        private Vector3 verticalVelocity;
        private Vector2Int currentTile = new Vector2Int(int.MinValue, int.MinValue);
        private TileNode occupiedTile;
        private bool inputEnabled = true;

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
            moveTilesPerSecond = Mathf.Max(moveTilesPerSecond, 2.9f);
            SetInputEnabled(true);
        }

        public void BeginAt(Vector2Int startCoordinates)
        {
            EnsureController();

            Vector3 startPosition = gridManager.GetWorldPosition(startCoordinates);
            transform.position = startPosition + Vector3.up * 1.1f;
            currentTile = new Vector2Int(int.MinValue, int.MinValue);
            SetOccupiedTile(null);
            horizontalVelocity = Vector3.zero;
            verticalVelocity = Vector3.zero;
            gameObject.tag = "Player";
            SetInputEnabled(true);
            RevealTileUnderPlayer();
        }

        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
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
            }

            verticalVelocity.y += gravity * Time.deltaTime;

            Vector3 movement = (horizontalVelocity + verticalVelocity) * Time.deltaTime;
            characterController.Move(movement);

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
            if (transform.Find("CyberAnalystBody") != null)
            {
                return;
            }

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "CyberAnalystBody";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);

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
    }
}
