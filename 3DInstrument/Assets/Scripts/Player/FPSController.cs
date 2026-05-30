using UnityEngine;

/// <summary>
/// FPS Controller cho Spatial Audio Sandbox.
/// Người dùng di chuyển quanh AudioWorld, nghe spatial audio thay đổi realtime.
///
/// HIERARCHY:
///   Player                   ← GameObject + CharacterController + FPSController
///   └── Main Camera          ← Camera + AudioListener
///
/// Không còn InstrumentInteractor — tương tác đã chuyển sang UI hoàn toàn.
///
/// KEYBINDS:
///   WASD / Arrows  — di chuyển
///   Shift          — chạy nhanh
///   Mouse          — nhìn
///   Escape         — unlock/lock cursor
///   Tab            — toggle UI mode (cursor free để dùng sliders)
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class FPSController : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField, Range(1f, 20f)] private float moveSpeed        = 5f;
    [SerializeField, Range(1f, 3f)]  private float sprintMultiplier = 1.6f;

    [Header("Look")]
    [SerializeField, Range(0.5f, 10f)] private float mouseSensitivity = 2f;
    [SerializeField] private bool invertY = false;

    [Header("Gravity")]
    [SerializeField, Range(-50f, -5f)] private float gravity = -20f;

    [Header("Keybinds")]
    [SerializeField] private KeyCode unlockKey   = KeyCode.Escape;
    [SerializeField] private KeyCode uiModeKey   = KeyCode.Tab;

    // ── Public state ───────────────────────────────────────────────────────
    /// <summary>True khi cursor free để dùng UI sliders.</summary>
    public bool IsUIMode     { get; private set; }
    public bool IsCursorLocked => cursorLocked;

    // ── Private ────────────────────────────────────────────────────────────
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
            Debug.LogError("[FPSController] Không tìm thấy Camera child!", this);

        SetCursorLock(true);
    }

    private void Update()
    {
        HandleCursorToggle();
        if (!IsUIMode) HandleMovement();
        if (!IsUIMode) HandleLook();
    }

    // ── Input ──────────────────────────────────────────────────────────────
    private void HandleCursorToggle()
    {
        if (Input.GetKeyDown(unlockKey))
            SetCursorLock(!cursorLocked);

        // Tab = UI Mode: cursor free, camera không xoay → dùng sliders thoải mái
        if (Input.GetKeyDown(uiModeKey))
        {
            IsUIMode = !IsUIMode;
            SetCursorLock(!IsUIMode);
            // Disable CharacterController khi UI mode để không block mouse events của Canvas
            if (controller != null) controller.enabled = !IsUIMode;
        }

        // Click để lock lại — chỉ khi cursor free VÀ không phải UI mode
        // Không lock khi IsUIMode vì user đang dùng chuột để kéo slider
        if (Input.GetMouseButtonDown(0) && !cursorLocked && !IsUIMode)
            SetCursorLock(true);
    }

    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        float speed = Input.GetKey(KeyCode.LeftShift)
            ? moveSpeed * sprintMultiplier
            : moveSpeed;

        controller.Move((transform.right * h + transform.forward * v) * (speed * Time.deltaTime));

        // Gravity
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(new Vector3(0f, verticalVelocity * Time.deltaTime, 0f));
    }

    private void HandleLook()
    {
        if (!cursorLocked) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mx, 0f);

        verticalAngle -= invertY ? -my : my;
        verticalAngle  = Mathf.Clamp(verticalAngle, -85f, 85f);
        if (cam != null)
            cam.transform.localRotation = Quaternion.Euler(verticalAngle, 0f, 0f);
    }

    // ── Public ─────────────────────────────────────────────────────────────
    public void SetCursorLock(bool locked)
    {
        cursorLocked     = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }
}