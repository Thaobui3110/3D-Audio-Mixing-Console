using UnityEngine;

/// <summary>
/// Chế độ nhìn từ trên xuống (top-down 2D) để kéo thả speaker trên mặt sàn.
/// Gắn trên Main Camera (cùng với SpeakerGrabber).
///
/// F2 → camera chuyển lên cao nhìn xuống (orthographic)
///   - Click speaker → chọn (highlight)
///   - Kéo chuột → di chuyển speaker trên mặt phẳng Y
///   - Scroll wheel → zoom in/out
///   - Middle mouse drag → pan camera
///   - F2 lần nữa → về FPS mode
///
/// SETUP: Main Camera → Add Component → TopDownView
/// </summary>
[DisallowMultipleComponent]
public class TopDownView : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Controls")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F2;

    [Header("Camera")]
    [SerializeField] private float defaultHeight = 15f;
    [SerializeField] private float minHeight     = 5f;
    [SerializeField] private float maxHeight     = 40f;
    [SerializeField] private float zoomSpeed     = 3f;
    [SerializeField] private float panSpeed      = 0.3f;

    [Header("Visual")]
    [SerializeField] private Color selectColor   = new Color(0.2f, 1f, 0.4f);
    [SerializeField] private float selectEmission = 0.8f;

    // ── State ──────────────────────────────────────────────────────────────
    public bool IsActive { get; private set; }

    private FPSController  fps;
    private SpeakerGrabber grabber;
    private Camera         cam;

    // Camera state trước khi vào top-down
    private Vector3    savedLocalPos;
    private Quaternion savedLocalRot;
    private Transform  savedParent;

    // Top-down camera
    private Vector3 topDownPos;
    private float   currentHeight;

    // Drag state
    private SpatialSoundObject selected;
    private SpatialSoundObject dragging;
    private Vector3            dragOffset;
    private bool               isDragging;

    // Material backup
    private Renderer savedRenderer;
    private Color    savedColor;
    private Color    savedEmission;
    private bool     savedHadEmission;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Start()
    {
        cam     = GetComponent<Camera>() ?? Camera.main;
        fps     = FindObjectOfType<FPSController>();
        grabber = GetComponent<SpeakerGrabber>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (!IsActive) EnterTopDown();
            else           ExitTopDown();
        }

        if (!IsActive) return;

        HandleZoom();
        HandlePan();
        HandleMouseInteraction();
    }

    private void OnDisable()
    {
        if (IsActive) ExitTopDown();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ENTER / EXIT
    // ══════════════════════════════════════════════════════════════════════

    private void EnterTopDown()
    {
        IsActive = true;

        if (grabber != null) grabber.enabled = false;

        // Save camera state
        savedParent   = cam.transform.parent;
        savedLocalPos = cam.transform.localPosition;
        savedLocalRot = cam.transform.localRotation;

        // Tách camera khỏi Player hierarchy
        cam.transform.SetParent(null, true);

        // Top-down position
        currentHeight = defaultHeight;
        Vector3 playerPos = savedParent != null ? savedParent.position : cam.transform.position;
        topDownPos = new Vector3(playerPos.x, currentHeight, playerPos.z);

        cam.transform.position = topDownPos;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.orthographic       = true;
        cam.orthographicSize   = currentHeight * 0.5f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (fps != null)
        {
            fps.enabled = false;
            var cc = fps.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        Debug.Log("[TopDownView] ON — kéo thả speaker, scroll zoom, F2 thoát.");
    }

    private void ExitTopDown()
    {
        IsActive = false;
        ClearSelection();

        cam.orthographic = false;
        cam.transform.SetParent(savedParent, false);
        cam.transform.localPosition = savedLocalPos;
        cam.transform.localRotation = savedLocalRot;

        if (fps != null)
        {
            fps.enabled = true;
            var cc = fps.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            fps.SetCursorLock(true);
        }

        if (grabber != null) grabber.enabled = true;

        Debug.Log("[TopDownView] OFF — về FPS mode.");
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ZOOM & PAN
    // ══════════════════════════════════════════════════════════════════════

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.001f) return;

        currentHeight -= scroll * zoomSpeed * currentHeight * 0.3f;
        currentHeight  = Mathf.Clamp(currentHeight, minHeight, maxHeight);

        topDownPos.y = currentHeight;
        cam.transform.position   = topDownPos;
        cam.orthographicSize     = currentHeight * 0.5f;
    }

    private void HandlePan()
    {
        if (!Input.GetMouseButton(2)) return;

        float dx = -Input.GetAxis("Mouse X") * panSpeed * currentHeight * 0.1f;
        float dz = -Input.GetAxis("Mouse Y") * panSpeed * currentHeight * 0.1f;

        topDownPos += new Vector3(dx, 0f, dz);
        cam.transform.position = topDownPos;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  MOUSE INTERACTION — Select & Drag
    // ══════════════════════════════════════════════════════════════════════

    private void HandleMouseInteraction()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var speaker = RaycastSpeaker();
            if (speaker != null)
            {
                Select(speaker);
                StartDrag(speaker);
            }
            else
            {
                ClearSelection();
            }
        }

        if (isDragging && dragging != null && Input.GetMouseButton(0))
        {
            UpdateDrag();
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            EndDrag();
        }
    }

    /// <summary>
    /// RaycastAll từ mouse → tìm SpatialSoundObject gần nhất,
    /// bỏ qua Room walls/floor/ceiling.
    /// </summary>
    private SpatialSoundObject RaycastSpeaker()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 200f);

        SpatialSoundObject closest = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var sso = hit.collider.GetComponent<SpatialSoundObject>();
            if (sso != null && hit.distance < closestDist)
            {
                closest     = sso;
                closestDist = hit.distance;
            }
        }
        return closest;
    }

    private Vector3 MouseToFloor()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (ray.direction.y != 0f)
        {
            float t = -ray.origin.y / ray.direction.y;
            if (t > 0f) return ray.origin + ray.direction * t;
        }
        return Vector3.zero;
    }

    // ── Select / Deselect ─────────────────────────────────────────────────
    private void Select(SpatialSoundObject speaker)
    {
        if (selected == speaker) return;
        ClearSelection();

        selected = speaker;
        var r = speaker.GetComponent<Renderer>();
        if (r != null)
        {
            savedRenderer    = r;
            savedColor       = r.material.color;
            savedHadEmission = r.material.IsKeywordEnabled("_EMISSION");
            savedEmission    = r.material.HasProperty("_EmissionColor")
                             ? r.material.GetColor("_EmissionColor")
                             : Color.black;

            r.material.EnableKeyword("_EMISSION");
            r.material.SetColor("_EmissionColor", selectColor * selectEmission);
        }
    }

    private void ClearSelection()
    {
        if (selected != null && savedRenderer != null)
        {
            savedRenderer.material.SetColor("_EmissionColor", savedEmission);
            if (!savedHadEmission) savedRenderer.material.DisableKeyword("_EMISSION");
        }
        selected      = null;
        savedRenderer = null;
    }

    // ── Drag ──────────────────────────────────────────────────────────────
    private void StartDrag(SpatialSoundObject speaker)
    {
        dragging   = speaker;
        isDragging = true;
        Vector3 floor = MouseToFloor();
        dragOffset = speaker.transform.position
                   - new Vector3(floor.x, speaker.transform.position.y, floor.z);
    }

    private void UpdateDrag()
    {
        Vector3 floor  = MouseToFloor();
        Vector3 newPos = new Vector3(floor.x, dragging.transform.position.y, floor.z) + dragOffset;
        dragging.transform.position = newPos;
    }

    private void EndDrag()
    {
        if (dragging != null)
        {
            // Clamp vào trong phòng
            if (RoomBounds.Instance != null)
                dragging.transform.position = RoomBounds.Instance.ClampInside(dragging.transform.position);

            var p = dragging.transform.position;
            Debug.Log($"[TopDownView] {dragging.SourceID} → ({p.x:F1}, {p.y:F1}, {p.z:F1})");
        }
        isDragging = false;
        dragging   = null;
    }
}