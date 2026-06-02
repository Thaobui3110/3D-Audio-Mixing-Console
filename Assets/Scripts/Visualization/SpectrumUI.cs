using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Frequency spectrum — UI panel có thể drag và collapse/expand.
/// Click title bar để thu gọn / mở rộng.
/// Drag title bar để di chuyển.
/// </summary>
public class SpectrumUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private Vector2 panelSize      = new Vector2(420f, 180f);
    [SerializeField] private Vector2 panelPosition  = new Vector2(20f, 20f);
    [SerializeField] private float   titleBarHeight = 26f;
    [SerializeField] private float   dropdownHeight = 24f;

    [Header("Bars")]
    [SerializeField, Range(32, 256)] private int   barCount        = 128;
    [SerializeField]                 private float scaleMultiplier = 120f;
    [SerializeField]                 private bool  logScale        = true;

    [Header("Colors")]
    [SerializeField] private Color panelBgColor = new Color(0.08f, 0.08f, 0.12f, 0.92f);
    [SerializeField] private Color titleColor   = new Color(0.12f, 0.12f, 0.18f, 1f);
    [SerializeField] private Color bassColor    = new Color(1f,    0.25f, 0.1f,  1f);
    [SerializeField] private Color midColor     = new Color(0.2f,  1f,    0.35f, 1f);
    [SerializeField] private Color trebleColor  = new Color(0.5f,  0.35f, 1f,   1f);

    // ── Runtime refs ───────────────────────────────────────────────────────
    private RectTransform[]     bars;
    private Image[]             barImages;
    private Canvas              rootCanvas;
    private RectTransform       panelRect;
    private GameObject          bodyGO;       // everything below title bar
    private TextMeshProUGUI     titleLabel;
    private TMP_Dropdown        sourceDropdown;
    private SpatialAudioManager audioManager;

    // State
    private bool    isCollapsed  = false;
    private bool    isDragging   = false;
    private bool    didDrag      = false;      // distinguish click vs drag
    private Vector2 dragOffset;
    private string  selectedSourceID = null;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Start()
    {
        audioManager = FindObjectOfType<SpatialAudioManager>();
        BuildPanel();
        RefreshDropdown();

        if (audioManager != null)
        {
            audioManager.OnSourceSpawned += _ => RefreshDropdown();
            audioManager.OnSourceRemoved += _ => RefreshDropdown();
        }
    }

    private void Update()
    {
        HandleTitleInteraction();
        if (!isCollapsed) UpdateBars();
    }

    // ── Build ──────────────────────────────────────────────────────────────

    private void BuildPanel()
    {
        rootCanvas = FindObjectOfType<Canvas>();
        if (rootCanvas == null)
        {
            var cgo = new GameObject("SpectrumCanvas");
            rootCanvas = cgo.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cgo.AddComponent<CanvasScaler>();
            cgo.AddComponent<GraphicRaycaster>();
        }

        // Panel root — starts at full size
        var panelGO = MakeImage("SpectrumPanel", rootCanvas.transform, panelBgColor);
        panelRect   = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = Vector2.zero;
        panelRect.sizeDelta        = panelSize;
        panelRect.anchoredPosition = panelPosition;

        // ── Title bar ───────────────────────────────────────────────────
        var titleGO = MakeImage("TitleBar", panelRect, titleColor);
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = Vector2.one;
        titleRT.pivot     = new Vector2(0.5f, 1f);
        titleRT.offsetMin = new Vector2(0f, -titleBarHeight);
        titleRT.offsetMax = Vector2.zero;
        // Needs raycast for click detection
        titleGO.GetComponent<Image>().raycastTarget = true;

        // Title label + collapse indicator
        var lblGO = new GameObject("TitleLabel");
        lblGO.transform.SetParent(titleRT, false);
        titleLabel            = lblGO.AddComponent<TextMeshProUGUI>();
        titleLabel.fontSize   = 11f;
        titleLabel.color      = new Color(0.8f, 0.8f, 0.9f);
        titleLabel.alignment  = TextAlignmentOptions.MidlineLeft;
        titleLabel.margin     = new Vector4(6f, 0f, 0f, 0f);
        titleLabel.text       = "▼  SPECTRUM";
        StretchRT(lblGO.GetComponent<RectTransform>());

        // ── Body (dropdown + bars) — hidden when collapsed ─────────────
        bodyGO = new GameObject("Body");
        bodyGO.transform.SetParent(panelRect, false);
        var bodyRT = bodyGO.AddComponent<RectTransform>();
        bodyRT.anchorMin = Vector2.zero;
        bodyRT.anchorMax = Vector2.one;
        bodyRT.offsetMin = new Vector2(0f, 0f);
        bodyRT.offsetMax = new Vector2(0f, -titleBarHeight);

        // Dropdown
        sourceDropdown = BuildDropdown("SourceDropdown", bodyRT, 0f, dropdownHeight);
        sourceDropdown.onValueChanged.AddListener(OnSourceDropdownChanged);

        // Bar area
        var areaGO = new GameObject("BarArea");
        areaGO.transform.SetParent(bodyRT, false);
        var areaRT = areaGO.AddComponent<RectTransform>();
        areaRT.anchorMin = Vector2.zero;
        areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = new Vector2(2f, 2f);
        areaRT.offsetMax = new Vector2(-2f, -dropdownHeight);

        // Bars
        bars      = new RectTransform[barCount];
        barImages = new Image[barCount];
        float barW = (panelSize.x - 4f) / barCount;

        for (int i = 0; i < barCount; i++)
        {
            var barGO = MakeImage("b" + i, areaRT, bassColor);
            var barRT = barGO.GetComponent<RectTransform>();
            barRT.anchorMin = barRT.anchorMax = barRT.pivot = Vector2.zero;
            barRT.sizeDelta        = new Vector2(Mathf.Max(1f, barW - 1f), 1f);
            barRT.anchoredPosition = new Vector2(i * barW, 0f);
            bars[i]      = barRT;
            barImages[i] = barGO.GetComponent<Image>();
        }
    }

    // ── Title interaction: drag + click-to-collapse ────────────────────────

    private void HandleTitleInteraction()
    {
        if (panelRect == null) return;
        Vector2 mouse = Input.mousePosition;

        // Compute title bar screen rect
        Vector3[] corners = new Vector3[4];
        panelRect.GetWorldCorners(corners);
        float top = corners[2].y;
        float bot = top - titleBarHeight;
        float L   = corners[0].x;
        float R   = corners[2].x;
        bool overTitle = mouse.x >= L && mouse.x <= R && mouse.y >= bot && mouse.y <= top;

        if (Input.GetMouseButtonDown(0) && overTitle)
        {
            isDragging = true;
            didDrag    = false;
            dragOffset = (Vector2)panelRect.position - mouse;
        }

        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector2 delta = mouse + dragOffset - (Vector2)panelRect.position;
            if (delta.magnitude > 4f) didDrag = true;   // moved enough to count as drag

            if (didDrag)
            {
                Vector2 p = mouse + dragOffset;
                p.x = Mathf.Clamp(p.x, panelSize.x * 0.5f, Screen.width  - panelSize.x * 0.5f);
                p.y = Mathf.Clamp(p.y, titleBarHeight * 0.5f, Screen.height - titleBarHeight * 0.5f);
                panelRect.position = p;
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            if (!didDrag && overTitle)
                ToggleCollapse();   // it was a click, not a drag
            isDragging = false;
        }
    }

    private void ToggleCollapse()
    {
        isCollapsed = !isCollapsed;
        bodyGO.SetActive(!isCollapsed);

        // Shrink / restore panel height
        panelRect.sizeDelta = isCollapsed
            ? new Vector2(panelSize.x, titleBarHeight)
            : panelSize;

        titleLabel.text = isCollapsed ? "▶  SPECTRUM" : "▼  SPECTRUM";
    }

    // ── Dropdown ───────────────────────────────────────────────────────────

    private void RefreshDropdown()
    {
        if (sourceDropdown == null) return;
        sourceDropdown.onValueChanged.RemoveListener(OnSourceDropdownChanged);
        sourceDropdown.ClearOptions();
        sourceDropdown.options.Add(new TMP_Dropdown.OptionData("All (Average)"));

        if (audioManager != null)
            foreach (var id in audioManager.GetAllIDs())
                sourceDropdown.options.Add(new TMP_Dropdown.OptionData(id));

        int restore = 0;
        if (selectedSourceID != null)
            for (int i = 1; i < sourceDropdown.options.Count; i++)
                if (sourceDropdown.options[i].text == selectedSourceID) { restore = i; break; }

        sourceDropdown.value = restore;
        sourceDropdown.RefreshShownValue();
        sourceDropdown.onValueChanged.AddListener(OnSourceDropdownChanged);
    }

    private void OnSourceDropdownChanged(int index)
    {
        selectedSourceID = index == 0 ? null : sourceDropdown.options[index].text;
    }

    // ── Update bars ────────────────────────────────────────────────────────

    private void UpdateBars()
    {
        float[] data = GetSpectrumData();
        if (data == null) return;

        float areaH = panelSize.y - titleBarHeight - dropdownHeight - 4f;
        int   count = Mathf.Min(barCount, data.Length);

        for (int i = 0; i < count; i++)
        {
            float h = logScale
                ? scaleMultiplier * Mathf.Log10(1f + data[i] * 9f)
                : scaleMultiplier * Mathf.Sqrt(data[i]);
            h = Mathf.Clamp(h, 1f, areaH);

            bars[i].sizeDelta = new Vector2(bars[i].sizeDelta.x, h);

            float t = (float)i / count;
            barImages[i].color = t < 0.5f
                ? Color.Lerp(bassColor, midColor,    t * 2f)
                : Color.Lerp(midColor,  trebleColor, (t - 0.5f) * 2f);
        }
    }

    private float[] GetSpectrumData()
    {
        if (selectedSourceID != null && audioManager != null)
        {
            var sso = audioManager.Get(selectedSourceID);
            if (sso != null)
            {
                var src = sso.GetComponent<AudioSource>()
                          ?? sso.GetComponentInChildren<AudioSource>();
                if (src != null && src.isPlaying)
                {
                    float[] buf = new float[512];
                    src.GetSpectrumData(buf, 0, FFTWindow.BlackmanHarris);
                    return buf;
                }
            }
        }

        // All (Average)
        if (audioManager != null)
        {
            int     srcCount = 0;
            float[] avg      = new float[512];
            foreach (var id in audioManager.GetAllIDs())
            {
                var sso = audioManager.Get(id);
                if (sso == null) continue;
                var src = sso.GetComponent<AudioSource>() ?? sso.GetComponentInChildren<AudioSource>();
                if (src == null || !src.isPlaying) continue;
                float[] buf = new float[512];
                src.GetSpectrumData(buf, 0, FFTWindow.BlackmanHarris);
                for (int i = 0; i < 512; i++) avg[i] += buf[i];
                srcCount++;
            }
            if (srcCount > 0)
            {
                for (int i = 0; i < 512; i++) avg[i] /= srcCount;
                return avg;
            }
        }

        return SpectrumAnalyzer.Instance?.SpectrumData;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static GameObject MakeImage(string name, Transform parent, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
        return go;
    }

    private static void StretchRT(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private TMP_Dropdown BuildDropdown(string name, Transform parent, float offsetFromTop, float height)
    {
        var ddGO = MakeImage(name, parent, new Color(0.15f, 0.15f, 0.22f, 1f));
        ddGO.GetComponent<Image>().raycastTarget = true;
        var ddRT = ddGO.GetComponent<RectTransform>();
        ddRT.anchorMin = new Vector2(0f, 1f);
        ddRT.anchorMax = Vector2.one;
        ddRT.pivot     = new Vector2(0.5f, 1f);
        ddRT.offsetMin = new Vector2(0f, -(offsetFromTop + height));
        ddRT.offsetMax = new Vector2(0f, -offsetFromTop);

        var capGO  = new GameObject("Label");
        capGO.transform.SetParent(ddRT, false);
        var capTMP = capGO.AddComponent<TextMeshProUGUI>();
        capTMP.fontSize  = 11f;
        capTMP.color     = Color.white;
        capTMP.alignment = TextAlignmentOptions.MidlineLeft;
        capTMP.margin    = new Vector4(6f, 0f, 6f, 0f);
        StretchRT(capGO.GetComponent<RectTransform>());

        var templateGO  = new GameObject("Template");
        templateGO.transform.SetParent(ddRT, false);
        var templateImg = templateGO.AddComponent<Image>();
        templateImg.color = new Color(0.12f, 0.12f, 0.18f, 1f);
        var templateRT  = templateGO.GetComponent<RectTransform>();
        templateRT.anchorMin = new Vector2(0f, 0f);
        templateRT.anchorMax = new Vector2(1f, 0f);
        templateRT.pivot     = new Vector2(0.5f, 1f);
        templateRT.sizeDelta = new Vector2(0f, 120f);
        templateGO.AddComponent<ScrollRect>().content = templateRT;
        templateGO.SetActive(false);

        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(templateRT, false);
        viewportGO.AddComponent<Image>().color = Color.clear;
        viewportGO.AddComponent<Mask>().showMaskGraphic = false;
        StretchRT(viewportGO.GetComponent<RectTransform>());

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = Vector2.one;
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = Vector2.zero;
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight    = false;
        vlg.childForceExpandWidth = true;
        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var itemGO     = new GameObject("Item");
        itemGO.transform.SetParent(contentGO.transform, false);
        var itemToggle = itemGO.AddComponent<Toggle>();
        itemGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 22f);

        var itemBgGO  = new GameObject("Item Background");
        itemBgGO.transform.SetParent(itemGO.transform, false);
        var itemBgImg = itemBgGO.AddComponent<Image>();
        itemBgImg.color = new Color(0.2f, 0.2f, 0.3f, 1f);
        StretchRT(itemBgGO.GetComponent<RectTransform>());

        var itemCkGO  = new GameObject("Item Checkmark");
        itemCkGO.transform.SetParent(itemGO.transform, false);
        var itemCkImg = itemCkGO.AddComponent<Image>();
        itemCkImg.color = new Color(0.4f, 0.8f, 1f, 1f);
        var ckRT = itemCkGO.GetComponent<RectTransform>();
        ckRT.anchorMin = ckRT.anchorMax = ckRT.pivot = new Vector2(0f, 0.5f);
        ckRT.sizeDelta = new Vector2(16f, 16f);
        ckRT.anchoredPosition = new Vector2(4f, 0f);

        var itemLblGO = new GameObject("Item Label");
        itemLblGO.transform.SetParent(itemGO.transform, false);
        var itemTMP = itemLblGO.AddComponent<TextMeshProUGUI>();
        itemTMP.fontSize  = 11f;
        itemTMP.color     = Color.white;
        itemTMP.alignment = TextAlignmentOptions.MidlineLeft;
        itemTMP.margin    = new Vector4(22f, 0f, 4f, 0f);
        StretchRT(itemLblGO.GetComponent<RectTransform>());

        itemToggle.targetGraphic = itemBgImg;
        itemToggle.graphic       = itemCkImg;
        itemToggle.isOn          = false;

        var dd           = ddGO.AddComponent<TMP_Dropdown>();
        dd.targetGraphic = ddGO.GetComponent<Image>();
        dd.captionText   = capTMP;
        dd.itemText      = itemTMP;
        dd.template      = templateRT;
        dd.options.Add(new TMP_Dropdown.OptionData("All (Average)"));
        return dd;
    }
}