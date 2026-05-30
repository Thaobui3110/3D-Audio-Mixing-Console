using UnityEngine;

/// <summary>
/// FPS Controller cơ bản cho nhân vật trong scene nhạc cụ.
///
/// HIERARCHY SETUP:
///   Player (GameObject)                   ← FPSController + CharacterController
///   └── Camera (GameObject)               ← Camera component + AudioListener
///       └── InstrumentInteractor          ← raycast tương tác
///
/// KEYBINDS:
///   WASD / Arrow Keys  — di chuyển
///   Shift              — chạy
///   Mouse              — nhìn
///   Escape             — toggle cursor lock
///   Tab                — Interact Mode (cursor free, camera cố định)
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class FPSController : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField, Range(1f, 20f)] private float moveSpeed        = 5f;
    [SerializeField, Range(1f, 3f)]  private float sprintMultiplier = 1.5f;

    [Header("Look")]
    [SerializeField, Range(0.1f, 10f)] private float mouseSensitivity = 2f;
    [SerializeField] private bool invertY = false;
    [SerializeField, Range(-90f, 0f)]  private float minVerticalAngle = -80f;
    [SerializeField, Range(0f, 90f)]   private float maxVerticalAngle =  80f;

    [Header("Gravity")]
    [SerializeField, Range(-50f, -5f)] private float gravity = -20f;

    [Header("Keybinds")]
    [SerializeField] private KeyCode unlockCursorKey    = KeyCode.Escape;
    [SerializeField] private KeyCode toggleInteractKey  = KeyCode.Tab;

    // ── Public state (read bởi InstrumentInteractor) ───────────────────────
    public bool IsInteractMode { get; private set; }
    public bool IsCursorLocked => cursorLocked;

    // ── Private state ──────────────────────────────────────────────────────
    private CharacterController controller;
    private Camera              cam;
    private float               verticalAngle;
    private float               verticalVelocity;
    private bool                cursorLocked = true;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Start()
    {
        controller = GetComponent<CharacterController>();
        cam        = GetComponentInChildren<Camera>();

        if (cam == null)
            Debug.LogError("[FPSController] Camera child không tìm thấy!", this);

        SetCursorLock(true);
    }

    private void Update()
    {
        HandleCursorToggle();
        HandleMovement();

        if (!IsInteractMode)
            HandleLook();
    }

    // ── Input handlers ─────────────────────────────────────────────────────
    private void HandleCursorToggle()
    {
        // Escape: toggle cursor lock (tạm thời)
        if (Input.GetKeyDown(unlockCursorKey))
            SetCursorLock(!cursorLocked);

        // Tab: Interact Mode — cursor free, camera không di chuyển
        if (Input.GetKeyDown(toggleInteractKey))
        {
            IsInteractMode = !IsInteractMode;
            SetCursorLock(!IsInteractMode);
            Debug.Log(IsInteractMode
                ? "[FPS] Interact Mode ON — cursor free, camera locked"
                : "[FPS] Interact Mode OFF — FPS control enabled");
        }

        // Click chuột khi cursor free và không trong interact mode → lock lại
        if (Input.GetMouseButtonDown(0) && !cursorLocked && !IsInteractMode)
            SetCursorLock(true);
    }

    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        float speed = Input.GetKey(KeyCode.LeftShift)
            ? moveSpeed * sprintMultiplier
            : moveSpeed;

        Vector3 move = transform.right * h + transform.forward * v;
        controller.Move(move * (speed * Time.deltaTime));

        // Gravity với vận tốc tích lũy đúng vật lý
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f; // nhỏ âm để duy trì isGrounded chính xác

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(new Vector3(0f, verticalVelocity * Time.deltaTime, 0f));
    }

    private void HandleLook()
    {
        if (!cursorLocked) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Xoay ngang toàn bộ Player
        transform.Rotate(0f, mouseX, 0f);

        // Xoay dọc Camera (clamp để không lật ngược đầu)
        verticalAngle -= invertY ? -mouseY : mouseY;
        verticalAngle  = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);

        if (cam != null)
            cam.transform.localRotation = Quaternion.Euler(verticalAngle, 0f, 0f);
    }

    // ── Public helpers ─────────────────────────────────────────────────────
    public void SetCursorLock(bool locked)
    {
        cursorLocked     = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }
}
