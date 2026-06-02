using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Attached to Main Camera (FPS mode).
/// • Raycast every frame — highlights the speaker object being looked at.
/// • Shows a screen-space tooltip label with the speaker's name on hover.
/// • Press E to grab / release a speaker.
/// </summary>
public class SpeakerGrabber : MonoBehaviour
{
    [Header("Grab Settings")]
    [SerializeField] private float grabDistance    = 5f;
    [SerializeField] private float grabHoldDistance = 4.5f;
    [SerializeField] private LayerMask grabMask    = ~0;

    [Header("Highlight")]
    [SerializeField] private Color hoverTintColor  = new Color(1f, 1f, 0.4f, 1f);

    [Header("Tooltip UI")]
    [Tooltip("Assign a TextMeshProUGUI element in Canvas for the hover tooltip. " +
             "If null, one will be created at runtime.")]
    [SerializeField] private TextMeshProUGUI tooltipLabel;

    // ── Private ────────────────────────────────────────────────────────────
    private Transform          grabbedSpeaker;
    private SpatialSoundObject grabbedSSO;
    private Renderer           hoveredRenderer;
    private Color              originalEmission;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private RectTransform tooltipRect;
    private Canvas        rootCanvas;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Start()
    {
        EnsureTooltip();
        HideTooltip();
    }

    private void Update()
    {
        HandleHoverAndTooltip();
        HandleGrabInput();

        if (grabbedSpeaker != null)
            MoveGrabbedSpeaker();
    }

    // ── Tooltip Setup ──────────────────────────────────────────────────────

    private void EnsureTooltip()
    {
        if (tooltipLabel != null)
        {
            tooltipRect = tooltipLabel.GetComponent<RectTransform>();
            rootCanvas  = tooltipLabel.GetComponentInParent<Canvas>();
            return;
        }

        // Create a runtime tooltip if none assigned
        var canvasGO = new GameObject("GrabberTooltipCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        rootCanvas = canvas;

        var bgGO  = new GameObject("TooltipBG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bg    = bgGO.AddComponent<Image>();
        bg.color  = new Color(0f, 0f, 0f, 0.72f);
        tooltipRect = bgGO.GetComponent<RectTransform>();
        tooltipRect.sizeDelta = new Vector2(200f, 32f);
        tooltipRect.pivot     = new Vector2(0f, 0f);

        var lblGO = new GameObject("TooltipLabel");
        lblGO.transform.SetParent(bgGO.transform, false);
        tooltipLabel = lblGO.AddComponent<TextMeshProUGUI>();
        tooltipLabel.fontSize  = 14f;
        tooltipLabel.color     = Color.white;
        tooltipLabel.alignment = TextAlignmentOptions.MidlineLeft;
        tooltipLabel.margin    = new Vector4(6f, 0f, 6f, 0f);
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;
    }

    // ── Hover / Tooltip ────────────────────────────────────────────────────

    private void HandleHoverAndTooltip()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, grabDistance, grabMask);

        SpatialSoundObject hoveredSSO = null;
        if (hit)
            hoveredSSO = hitInfo.collider.GetComponentInParent<SpatialSoundObject>();

        if (hoveredSSO != null)
        {
            // Show tooltip at screen-space position slightly above crosshair
            string label = hoveredSSO.gameObject.name;  // e.g. "Speaker_01"
            ShowTooltip(label, Input.mousePosition + new Vector3(16f, 16f, 0f));

            // Tint emission to indicate hover (only when not grabbed)
            Renderer rend = hoveredSSO.GetComponentInChildren<Renderer>();
            if (rend != null && rend != hoveredRenderer)
            {
                ClearHoverTint();
                hoveredRenderer = rend;
                // Store original via MaterialPropertyBlock
                var pb = new MaterialPropertyBlock();
                rend.GetPropertyBlock(pb);
                originalEmission = pb.GetColor(EmissionColorID);
                pb.SetColor(EmissionColorID, hoverTintColor);
                rend.SetPropertyBlock(pb);
            }
        }
        else
        {
            HideTooltip();
            ClearHoverTint();
        }
    }

    private void ShowTooltip(string text, Vector3 screenPos)
    {
        if (tooltipLabel == null || tooltipRect == null) return;
        tooltipLabel.text = text;

        // Convert screen position for overlay canvas
        tooltipRect.anchoredPosition = ScreenToCanvasPosition(screenPos);
        tooltipLabel.gameObject.SetActive(true);
        if (tooltipRect != null) tooltipRect.gameObject.SetActive(true);
    }

    private void HideTooltip()
    {
        if (tooltipLabel == null) return;
        tooltipLabel.gameObject.SetActive(false);
        if (tooltipRect != null && tooltipRect != tooltipLabel.rectTransform)
            tooltipRect.gameObject.SetActive(false);
    }

    private Vector2 ScreenToCanvasPosition(Vector3 screenPos)
    {
        if (rootCanvas == null) return screenPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.transform as RectTransform,
            screenPos,
            rootCanvas.worldCamera,
            out Vector2 localPoint);
        return localPoint;
    }

    private void ClearHoverTint()
    {
        if (hoveredRenderer == null) return;
        var pb = new MaterialPropertyBlock();
        hoveredRenderer.GetPropertyBlock(pb);
        pb.SetColor(EmissionColorID, originalEmission);
        hoveredRenderer.SetPropertyBlock(pb);
        hoveredRenderer = null;
    }

    // ── Grab ───────────────────────────────────────────────────────────────

    private void HandleGrabInput()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (grabbedSpeaker != null)
        {
            ReleaseSpeaker();
            return;
        }

        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, grabDistance, grabMask))
        {
            SpatialSoundObject sso = hit.collider.GetComponentInParent<SpatialSoundObject>();
            if (sso != null)
            {
                grabbedSpeaker = sso.transform;
                grabbedSSO     = sso;
            }
        }
    }

    private void ReleaseSpeaker()
    {
        if (grabbedSpeaker == null) return;
        // Notify AudioReactiveObject so basePosition updates to dropped position
        var aro = grabbedSpeaker.GetComponent<AudioReactiveObject>();
        if (aro != null) aro.UpdateBasePosition();

        grabbedSpeaker = null;
        grabbedSSO     = null;
    }

    private void MoveGrabbedSpeaker()
    {
        Vector3 target = transform.position + transform.forward * grabHoldDistance;
        grabbedSpeaker.position = Vector3.Lerp(grabbedSpeaker.position, target, Time.deltaTime * 15f);
    }
}