// Assets/Scripts/FPSController.cs
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed        = 5f;
    public float sprintMultiplier = 1.5f;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public bool  invertY          = false;

    [Header("Cursor Control")]
    public KeyCode unlockCursorKey    = KeyCode.Escape;
    public KeyCode toggleInteractMode = KeyCode.Tab;

    // ─────────────────────────────────────────────────────────────────────
    // Private state
    // ─────────────────────────────────────────────────────────────────────

    private CharacterController controller;
    private Camera              cam;
    private float               verticalRotation = 0f;
    private bool                cursorLocked     = true;

    // FIX 6: Thêm biến tích lũy vận tốc rơi.
    // Trước đây dùng Vector3.down * 9.81f * deltaTime — tức rơi với tốc độ cố định 9.81 m/s,
    // không gia tốc theo thời gian. Nhân vật rơi xuống vực như thang máy, không phải rơi tự do.
    // Thêm vào: vận tốc tăng dần theo gravity, reset về 0 khi chạm đất.
    private float verticalVelocity = 0f;
    private const float Gravity    = -20f;  // m/s² — âm = hướng xuống
                                             // -20 thay vì -9.81 để cảm giác game snappy hơn,
                                             // đổi lại -9.81 nếu muốn physics thực tế hơn

    public bool interactMode = false; // mode tương tác: unlock cursor, disable look

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam        = GetComponentInChildren<Camera>();
        SetCursorLock(true);
    }

    void Update()
    {
        HandleCursorToggle();

        if (!interactMode)
        {
            HandleMovement();
            HandleLook();
        }
        else
        {
            // Interact mode: chỉ di chuyển, không xoay camera
            HandleMovement();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Cursor toggle
    // ─────────────────────────────────────────────────────────────────────

    void HandleCursorToggle()
    {
        // ESC: unlock/lock cursor tạm thời
        if (Input.GetKeyDown(unlockCursorKey))
            SetCursorLock(!cursorLocked);

        // Tab: toggle interact mode (dành cho piano)
        if (Input.GetKeyDown(toggleInteractMode))
        {
            interactMode = !interactMode;
            SetCursorLock(!interactMode);
            Debug.Log(interactMode
                ? "Interact Mode ON — Cursor free, camera locked"
                : "Interact Mode OFF — FPS control enabled");
        }

        // Click chuột khi cursor free (và không trong interact mode) → lock lại
        if (Input.GetMouseButtonDown(0) && !cursorLocked && !interactMode)
            SetCursorLock(true);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Movement + Gravity
    // ─────────────────────────────────────────────────────────────────────

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        float speed = Input.GetKey(KeyCode.LeftShift)
            ? moveSpeed * sprintMultiplier
            : moveSpeed;

        // Di chuyển ngang
        Vector3 move = transform.right * h + transform.forward * v;
        controller.Move(move * speed * Time.deltaTime);

        // FIX 7: Gravity đúng vật lý — tích lũy vận tốc theo thời gian.
        // isGrounded: nếu đang đứng trên mặt đất thì giữ vận tốc nhỏ âm
        // để CharacterController tiếp tục detect isGrounded chính xác ở frame sau.
        // Nếu để = 0 thì frame sau isGrounded trả false và gravity lại tích lũy → giật cục.
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += Gravity * Time.deltaTime;

        controller.Move(new Vector3(0f, verticalVelocity * Time.deltaTime, 0f));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Mouse look
    // ─────────────────────────────────────────────────────────────────────

    void HandleLook()
    {
        if (!cursorLocked) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Xoay ngang toàn bộ Player
        transform.Rotate(0f, mouseX, 0f);

        // Xoay dọc Camera (clamp để không lật ngược đầu)
        verticalRotation -= mouseY * (invertY ? -1f : 1f);
        verticalRotation  = Mathf.Clamp(verticalRotation, -90f, 90f);
        cam.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Cursor helper
    // ─────────────────────────────────────────────────────────────────────

    void SetCursorLock(bool locked)
    {
        cursorLocked          = locked;
        Cursor.lockState      = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible        = !locked;
    }
}