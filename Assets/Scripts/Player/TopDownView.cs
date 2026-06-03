using UnityEngine;

/// <summary>
/// Chế độ nhìn từ trên xuống (top-down 2D) để kéo thả speaker trên mặt sàn.
/// F2 → vào top-down, F2 lần nữa → về FPS.
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
    [SerializeField] private Color selectColor    = new Color(0.2f, 1f, 0.4f);
    [SerializeField] private float selectEmission = 0.8f;

    // ── State ──────────────────────────────────────────────────────────────
    public bool IsActive { get; private set; }

    private FPSController  fps;
    private SpeakerGrabber grabber;
    private Camera         cam;

    // Camera save/restore
    private Vector3    savedLocalPos;
    private Quaternion savedLocalRot;
    private Transform  savedParent;

    // Top-down camera
    private Vector3 topDownPos;
    private float   currentHeight;

    // Selection
    private SpatialSoundObject    selected;
    private AudioReactiveObject   selectedARO;
    private MaterialPropertyBlock selectPropBlock;
    private static readonly int   EmissionColorID = Shader.PropertyToID("_EmissionColor");

    // Drag
    private SpatialSoundObject  dragging;
    private AudioReactiveObject draggingARO;
    private Vector3             dragOffset;
    private bool                isDragging;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Start()
    {
        cam     = GetComponent<Camera>() ?? Camera.main;
        fps     = FindObjectOfType<FPSController>();
        grabber = GetComponent<SpeakerGrabber>();
        selectPropBlock = new MaterialPropertyBlock();
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

        // Save camera
        savedParent   = cam.transform.parent;
        savedLocalPos = cam.transform.localPosition;
        savedLocalRot = cam.transform.localRotation;

        cam.transform.SetParent(null, true);

        currentHeight = defaultHeight;
        Vector3 playerPos = savedParent != null ? savedParent.position : cam.transform.position;
        topDownPos = new Vector3(playerPos.x, currentHeight, playerPos.z);

        cam.transform.position = topDownPos;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.orthographic       = true;
        cam.orthographicSize   = currentHeight * 0.5f;

        // Hiện cursor để có thể click/drag speaker
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (fps != null)
        {
            fps.enabled = false;
            var cc = fps.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }
    }

    private void ExitTopDown()
    {
        IsActive = false;
        ClearSelection();

        cam.orthographic = false;
        cam.transform.SetParent(savedParent, false);
        cam.transform.localPosition = savedLocalPos;
        cam.transform.localRotation = savedLocalRot;

        // QUAN TRỌNG: lock cursor lại trước khi enable FPSController
        // Nếu không làm, cursor vẫn visible và FPS mouse-look không hoạt động
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (fps != null)
        {
            var cc = fps.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;
            fps.enabled = true;
            // Gọi sau khi enable để FPSController nhận đúng trạng thái cursor
            fps.SetCursorLock(true);
        }

        if (grabber != null) grabber.enabled = true;
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

        topDownPos.y           = currentHeight;
        cam.transform.position = topDownPos;
        cam.orthographicSize   = currentHeight * 0.5f;
    }

    private void HandlePan()
    {
        if (!Input.GetMouseButton(2)) return;
        float dx = -Input.GetAxis("Mouse X") * panSpeed * currentHeight * 0.1f;
        float dz = -Input.GetAxis("Mouse Y") * panSpeed * currentHeight * 0.1f;
        topDownPos            += new Vector3(dx, 0f, dz);
        cam.transform.position = topDownPos;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  MOUSE INTERACTION
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
            UpdateDrag();

        if (Input.GetMouseButtonUp(0) && isDragging)
            EndDrag();
    }

    private SpatialSoundObject RaycastSpeaker()
    {
        Ray ray  = cam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 200f);

        SpatialSoundObject closest     = null;
        float              closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var sso = hit.collider.GetComponent<SpatialSoundObject>()
                      ?? hit.collider.GetComponentInParent<SpatialSoundObject>();
            if (sso != null && hit.distance < closestDist)
            {
                closest     = sso;
                closestDist = hit.distance;
            }
        }
        return closest;
    }

    private Vector3 MouseToPlane(float targetY)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Mathf.Abs(ray.direction.y) > 0.001f)
        {
            float t = (targetY - ray.origin.y) / ray.direction.y;
            if (t > 0f) return ray.origin + ray.direction * t;
        }
        return ray.origin;
    }

    // ── Select ────────────────────────────────────────────────────────────

    private void Select(SpatialSoundObject speaker)
    {
        if (selected == speaker) return;
        ClearSelection();

        selected    = speaker;
        selectedARO = speaker.GetComponent<AudioReactiveObject>()
                      ?? speaker.GetComponentInChildren<AudioReactiveObject>();

        if (selectedARO != null)
            selectedARO.SetHighlighted(true);
        else
        {
            var r = speaker.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                r.GetPropertyBlock(selectPropBlock);
                selectPropBlock.SetColor(EmissionColorID, selectColor * selectEmission);
                r.SetPropertyBlock(selectPropBlock);
            }
        }
    }

    private void ClearSelection()
    {
        if (selected == null) return;

        if (selectedARO != null)
        {
            selectedARO.SetHighlighted(false);
            selectedARO = null;
        }
        else
        {
            var r = selected.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                r.GetPropertyBlock(selectPropBlock);
                selectPropBlock.SetColor(EmissionColorID, Color.black);
                r.SetPropertyBlock(selectPropBlock);
            }
        }

        selected = null;
    }

    // ── Drag ──────────────────────────────────────────────────────────────

    private void StartDrag(SpatialSoundObject speaker)
    {
        dragging    = speaker;
        isDragging  = true;
        draggingARO = speaker.GetComponent<AudioReactiveObject>()
                      ?? speaker.GetComponentInChildren<AudioReactiveObject>();

        float   speakerY = speaker.transform.position.y;
        Vector3 floor    = MouseToPlane(speakerY);
        dragOffset       = speaker.transform.position - floor;
    }

    private void UpdateDrag()
    {
        float   speakerY = dragging.transform.position.y;
        Vector3 floor    = MouseToPlane(speakerY);
        Vector3 newPos   = floor + dragOffset;

        dragging.transform.position = newPos;

        // Cập nhật basePosition mỗi frame để AudioReactiveObject.UpdateFloat()
        // bob quanh vị trí mới thay vì vị trí cũ
        if (draggingARO != null)
            draggingARO.UpdateBasePosition();
    }

    private void EndDrag()
    {
        if (dragging != null)
        {
            if (RoomBounds.Instance != null)
                dragging.transform.position =
                    RoomBounds.Instance.ClampInside(dragging.transform.position);

            if (draggingARO != null)
                draggingARO.UpdateBasePosition();

            var p = dragging.transform.position;
            Debug.Log($"[TopDownView] {dragging.SourceID} → ({p.x:F1}, {p.y:F1}, {p.z:F1})");
        }

        isDragging  = false;
        dragging    = null;
        draggingARO = null;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void FocusOn(Vector3 worldPos)
    {
        if (!IsActive) return;
        topDownPos             = new Vector3(worldPos.x, currentHeight, worldPos.z);
        cam.transform.position = topDownPos;
    }
}