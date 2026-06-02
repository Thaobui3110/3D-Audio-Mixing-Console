using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Panel controller: kéo di chuyển + đóng/mở (collapse/expand).
/// Gắn trên AudioProcessorPanel — UIBuilder tự tạo title bar + toggle button.
///
/// Cấu trúc:
///   AudioProcessorPanel        [UIPanelController]
///   ├── TitleBar               ← kéo tại đây để di chuyển panel
///   │   ├── TitleLabel         "Audio Processor"
///   │   └── ToggleButton       "▼" / "▲"
///   └── Content                ← ẩn/hiện khi toggle
///       ├── UploadSection
///       ├── TransportSection
///       ├── Sliders...
///       └── StatusLabel
/// </summary>
[DisallowMultipleComponent]
public class UIPanelController : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    // ── Inspector ──────────────────────────────────────────────────────────
    [SerializeField] private RectTransform contentArea;
    [SerializeField] private Button        toggleButton;
    [SerializeField] private TMP_Text      toggleLabel;
    [SerializeField] private RectTransform panelRT;

    // ── State ──────────────────────────────────────────────────────────────
    private bool    isExpanded = true;
    private float   expandedHeight;
    private float   collapsedHeight;
    private Vector2 dragOffset;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        if (panelRT == null) panelRT = GetComponent<RectTransform>();

        // Lưu chiều cao expanded (ban đầu)
        expandedHeight = panelRT.sizeDelta.y;

        if (toggleButton != null)
            toggleButton.onClick.AddListener(TogglePanel);
    }

    // ── Collapse / Expand ─────────────────────────────────────────────────
    public void TogglePanel()
    {
        isExpanded = !isExpanded;

        if (contentArea != null)
            contentArea.gameObject.SetActive(isExpanded);

        // Resize panel
        if (isExpanded)
        {
            panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, expandedHeight);
        }
        else
        {
            // Thu nhỏ còn title bar height
            if (collapsedHeight < 1f)
                collapsedHeight = 36f; // title bar height
            panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, collapsedHeight);
        }

        // Update icon
        if (toggleLabel != null)
            toggleLabel.text = isExpanded ? "\u25BC" : "\u25B2"; // ▼ / ▲
    }

    /// <summary>Set collapsed height (gọi bởi UIBuilder sau khi tạo title bar).</summary>
    public void SetCollapsedHeight(float h) => collapsedHeight = h;

    // ── Drag ──────────────────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panelRT.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localMouse);

        dragOffset = panelRT.anchoredPosition - localMouse;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (panelRT.parent == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panelRT.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localMouse);

        panelRT.anchoredPosition = localMouse + dragOffset;
    }
}