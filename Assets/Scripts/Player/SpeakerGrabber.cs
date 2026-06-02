using UnityEngine;

/// <summary>
/// Grab & Place cho SpatialSoundObject.
/// Gắn trên Main Camera (cùng hierarchy với AudioListener).
///
/// FPS Mode (Tab OFF):
///   - Nhìn vào speaker → highlight (emission sáng lên)
///   - Nhấn E → grab: speaker bám theo tầm nhìn
///   - Di chuyển WASD + xoay chuột → speaker di chuyển theo
///   - Nhấn E lần nữa → release: speaker đặt tại vị trí hiện tại
///
/// UI Mode (Tab ON) / Top-Down (F2): Grab bị tắt.
///
/// SETUP: Main Camera → Add Component → SpeakerGrabber
/// </summary>
[DisallowMultipleComponent]
public class SpeakerGrabber : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Controls")]
    [SerializeField] private KeyCode grabKey       = KeyCode.E;
    [SerializeField] private float   grabDistance   = 4f;
    [SerializeField] private float   rayMaxDistance = 20f;
    [SerializeField] private float   moveSmooth     = 12f;

    [Header("Visual Feedback")]
    [SerializeField] private Color highlightColor    = new Color(1f, 1f, 0.5f, 1f);
    [SerializeField] private float highlightEmission = 0.6f;

    // ── State ──────────────────────────────────────────────────────────────
    private FPSController       fps;
    private TopDownView         topDown;
    private SpatialSoundObject  lookedAt;
    private SpatialSoundObject  grabbed;
    private MaterialData        originalMat;
    private Camera              cam;

    private struct MaterialData
    {
        public Color    color;
        public Color    emission;
        public bool     hadEmission;
        public Renderer renderer;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Start()
    {
        cam     = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        fps     = FindObjectOfType<FPSController>();
        topDown = GetComponent<TopDownView>();
    }

    private void Update()
    {
        // Không hoạt động trong UI Mode hoặc Top-Down Mode
        if (fps != null && fps.IsUIMode)          { ClearHighlight(); return; }
        if (topDown != null && topDown.IsActive)   { ClearHighlight(); return; }

        if (grabbed != null)
        {
            UpdateGrabbedPosition();

            if (Input.GetKeyDown(grabKey))
                Release();
        }
        else
        {
            UpdateLookDetection();

            if (Input.GetKeyDown(grabKey) && lookedAt != null)
                Grab(lookedAt);
        }
    }

    // ── Look detection (RaycastAll — xuyên qua Room colliders) ────────────
    private void UpdateLookDetection()
    {
        var speaker = RaycastSpeaker();

        if (speaker != null && speaker != lookedAt)
        {
            ClearHighlight();
            lookedAt = speaker;
            ApplyHighlight(speaker);
        }
        else if (speaker == null && lookedAt != null)
        {
            ClearHighlight();
        }
    }

    /// <summary>
    /// RaycastAll từ camera forward — tìm SpatialSoundObject gần nhất,
    /// bỏ qua Room walls/floor/ceiling.
    /// </summary>
    private SpatialSoundObject RaycastSpeaker()
    {
        var hits = Physics.RaycastAll(
            cam.transform.position, cam.transform.forward,
            rayMaxDistance);

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

    // ── Grab / Release ────────────────────────────────────────────────────
    private void Grab(SpatialSoundObject speaker)
    {
        grabbed = speaker;
        ApplyGrabVisual(speaker);
        Debug.Log($"[SpeakerGrabber] Grabbed {speaker.SourceID} — nhấn E để thả.");
    }

    private void Release()
    {
        if (grabbed == null) return;

        // Clamp vào trong phòng nếu có RoomBounds
        if (RoomBounds.Instance != null)
            grabbed.transform.position = RoomBounds.Instance.ClampInside(grabbed.transform.position);

        string id = grabbed.SourceID;
        Vector3 pos = grabbed.transform.position;

        RestoreMaterial();
        grabbed  = null;
        lookedAt = null;

        Debug.Log($"[SpeakerGrabber] Released {id} tại ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
    }

    private void UpdateGrabbedPosition()
    {
        Vector3 camForward = cam.transform.forward;
        camForward.y = 0f;
        if (camForward.sqrMagnitude < 0.01f) camForward = Vector3.forward;
        camForward.Normalize();

        Vector3 targetPos = cam.transform.position + camForward * grabDistance;
        targetPos.y = grabbed.transform.position.y;

        grabbed.transform.position = Vector3.Lerp(
            grabbed.transform.position,
            targetPos,
            moveSmooth * Time.deltaTime
        );
    }

    // ── Visual feedback ───────────────────────────────────────────────────
    private void ApplyHighlight(SpatialSoundObject speaker)
    {
        var renderer = speaker.GetComponent<Renderer>();
        if (renderer == null) return;

        SaveMaterial(renderer);

        var mat = renderer.material;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", highlightColor * highlightEmission);
    }

    private void ApplyGrabVisual(SpatialSoundObject speaker)
    {
        var renderer = speaker.GetComponent<Renderer>();
        if (renderer == null) return;

        if (originalMat.renderer == null) SaveMaterial(renderer);

        var mat = renderer.material;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", highlightColor * (highlightEmission * 1.5f));
    }

    private void SaveMaterial(Renderer renderer)
    {
        var mat = renderer.material;
        originalMat = new MaterialData
        {
            renderer    = renderer,
            color       = mat.color,
            emission    = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black,
            hadEmission = mat.IsKeywordEnabled("_EMISSION")
        };
    }

    private void RestoreMaterial()
    {
        if (originalMat.renderer == null) return;

        var mat = originalMat.renderer.material;
        mat.SetColor("_EmissionColor", originalMat.emission);
        if (!originalMat.hadEmission)
            mat.DisableKeyword("_EMISSION");

        originalMat = default;
    }

    private void ClearHighlight()
    {
        if (lookedAt == null) return;
        if (grabbed == null) RestoreMaterial();
        lookedAt = null;
    }
}