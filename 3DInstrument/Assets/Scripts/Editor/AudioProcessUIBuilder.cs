// ============================================================
// AudioProcessorUIBuilder.cs
// Đặt file này vào: Assets/Scripts/Editor/
//
// Cách dùng:
//   Menu Unity trên cùng → Tools → Build Audio Processor UI
//
// Script sẽ tự động:
//   1. Tạo Canvas + Panel trong Hierarchy
//   2. Tạo 9 SliderRow (Label tên + Slider + Label giá trị)
//   3. Tạo StatusLabel ở cuối
//   4. Tìm hoặc tạo AudioProcessorUI component
//   5. Liên kết tất cả Slider + Label vào đúng field trong Inspector
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

public class AudioProcessorUIBuilder : EditorWindow
{
    // ── Cấu trúc mô tả từng slider row ─────────────────────────────────
    private struct SliderConfig
    {
        public string fieldNameSlider;   // Tên SerializedProperty trên AudioProcessorUI
        public string fieldNameLabel;    // Tên SerializedProperty label trên AudioProcessorUI
        public string displayName;       // Text hiển thị bên trái slider
        public string defaultValueText;  // Text mặc định của label giá trị
        public float  minValue;
        public float  maxValue;
        public float  defaultValue;
        public string section;           // Tiêu đề section (null = không tạo mới)
    }

    private static readonly SliderConfig[] Sliders = new SliderConfig[]
    {
        // ── EQ ──────────────────────────────────────────────────────────
        new SliderConfig {
            section          = "EQ",
            fieldNameSlider  = "sliderLowPass",
            fieldNameLabel   = "labelLowPass",
            displayName      = "Low Pass Cutoff",
            defaultValueText = "22000 Hz",
            minValue         = 200f, maxValue = 22000f, defaultValue = 22000f
        },
        new SliderConfig {
            fieldNameSlider  = "sliderLowShelf",
            fieldNameLabel   = "labelLowShelf",
            displayName      = "Low Shelf Gain",
            defaultValueText = "0.0 dB",
            minValue         = -12f, maxValue = 12f, defaultValue = 0f
        },
        new SliderConfig {
            fieldNameSlider  = "sliderMidPeak",
            fieldNameLabel   = "labelMidPeak",
            displayName      = "Mid Peak Gain",
            defaultValueText = "0.0 dB",
            minValue         = -12f, maxValue = 12f, defaultValue = 0f
        },
        new SliderConfig {
            fieldNameSlider  = "sliderHighShelf",
            fieldNameLabel   = "labelHighShelf",
            displayName      = "High Shelf Gain",
            defaultValueText = "0.0 dB",
            minValue         = -12f, maxValue = 12f, defaultValue = 0f
        },
        // ── Reverb ──────────────────────────────────────────────────────
        new SliderConfig {
            section          = "Reverb",
            fieldNameSlider  = "sliderReverbWet",
            fieldNameLabel   = "labelReverbWet",
            displayName      = "Wet Level",
            defaultValueText = "0%",
            minValue         = 0f, maxValue = 1f, defaultValue = 0f
        },
        new SliderConfig {
            fieldNameSlider  = "sliderReverbDecay",
            fieldNameLabel   = "labelReverbDecay",
            displayName      = "Decay Time",
            defaultValueText = "1.00 s",
            minValue         = 0.1f, maxValue = 10f, defaultValue = 1f
        },
        // ── Compressor ──────────────────────────────────────────────────
        new SliderConfig {
            section          = "Compressor",
            fieldNameSlider  = "sliderCompThreshold",
            fieldNameLabel   = "labelCompThreshold",
            displayName      = "Threshold",
            defaultValueText = "0.0 dB",
            minValue         = -60f, maxValue = 0f, defaultValue = 0f
        },
        new SliderConfig {
            fieldNameSlider  = "sliderCompRatio",
            fieldNameLabel   = "labelCompRatio",
            displayName      = "Ratio",
            defaultValueText = "1.0:1",
            minValue         = 1f, maxValue = 20f, defaultValue = 1f
        },
        // ── Master ──────────────────────────────────────────────────────
        new SliderConfig {
            section          = "Master",
            fieldNameSlider  = "sliderMasterVolume",
            fieldNameLabel   = "labelMasterVolume",
            displayName      = "Master Volume",
            defaultValueText = "100%",
            minValue         = 0f, maxValue = 1f, defaultValue = 1f
        },
    };

    // ── Màu sắc panel ───────────────────────────────────────────────────
    private Color panelColor      = new Color(0.08f, 0.08f, 0.10f, 0.95f);
    private Color sectionColor    = new Color(0.18f, 0.45f, 0.85f, 1f);
    private Color rowBgColor      = new Color(0.14f, 0.14f, 0.17f, 1f);
    private Color sliderFillColor = new Color(0.25f, 0.55f, 1.00f, 1f);
    private Color labelColor      = new Color(0.75f, 0.80f, 0.90f, 1f);
    private Color valueColor      = new Color(1.00f, 1.00f, 1.00f, 1f);

    // ── Layout ──────────────────────────────────────────────────────────
    private float panelWidth     = 480f;
    private float rowHeight      = 52f;
    private float sectionHeight  = 28f;
    private float padding        = 12f;
    private float fontSize       = 14f;

    private bool replaceExisting = false;

    // ────────────────────────────────────────────────────────────────────
    // Window
    // ────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Build Audio Processor UI")]
    public static void OpenWindow()
    {
        var w = GetWindow<AudioProcessorUIBuilder>("Audio Processor UI Builder");
        w.minSize = new Vector2(380, 420);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        GUIStyle title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        EditorGUILayout.LabelField("Audio Processor UI Builder", title);
        EditorGUILayout.LabelField("Tự động tạo Canvas + Slider + Label và liên kết vào AudioProcessorUI", EditorStyles.miniLabel);
        EditorGUILayout.Space(8);

        DrawSeparator();
        EditorGUILayout.LabelField("Panel", EditorStyles.boldLabel);
        panelWidth    = EditorGUILayout.FloatField("Chiều rộng panel", panelWidth);
        rowHeight     = EditorGUILayout.FloatField("Chiều cao mỗi row", rowHeight);
        padding       = EditorGUILayout.FloatField("Padding ngang", padding);
        fontSize      = EditorGUILayout.FloatField("Cỡ chữ", fontSize);

        EditorGUILayout.Space(4);
        DrawSeparator();
        EditorGUILayout.LabelField("Màu sắc", EditorStyles.boldLabel);
        panelColor      = EditorGUILayout.ColorField("Nền panel",       panelColor);
        sectionColor    = EditorGUILayout.ColorField("Tiêu đề section", sectionColor);
        rowBgColor      = EditorGUILayout.ColorField("Nền row",         rowBgColor);
        sliderFillColor = EditorGUILayout.ColorField("Fill slider",     sliderFillColor);
        labelColor      = EditorGUILayout.ColorField("Chữ label",       labelColor);
        valueColor      = EditorGUILayout.ColorField("Chữ giá trị",     valueColor);

        EditorGUILayout.Space(4);
        DrawSeparator();
        replaceExisting = EditorGUILayout.Toggle("Xóa panel cũ nếu đã có", replaceExisting);

        EditorGUILayout.Space(10);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("⚡  Tạo UI và liên kết", GUILayout.Height(38)))
            Build();
        GUI.backgroundColor = Color.white;
    }

    // ────────────────────────────────────────────────────────────────────
    // Build
    // ────────────────────────────────────────────────────────────────────

    private void Build()
    {
        // ── 1. Tìm hoặc tạo Canvas ──────────────────────────────────────
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("UI Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
            Debug.Log("[UIBuilder] Đã tạo Canvas mới.");
        }

        // ── 2. Xử lý panel cũ ──────────────────────────────────────────
        Transform oldPanel = canvas.transform.Find("AudioProcessorPanel");
        if (oldPanel != null)
        {
            if (replaceExisting)
            {
                Undo.DestroyObjectImmediate(oldPanel.gameObject);
                Debug.Log("[UIBuilder] Đã xóa panel cũ.");
            }
            else
            {
                EditorUtility.DisplayDialog("Đã tồn tại",
                    "Panel 'AudioProcessorPanel' đã có trong Canvas.\nBật 'Xóa panel cũ' để build lại.", "OK");
                return;
            }
        }

        // ── 3. Tạo Panel gốc ────────────────────────────────────────────
        var panel = CreateUIObject("AudioProcessorPanel", canvas.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot     = new Vector2(0f, 0.5f);
        panelRect.anchoredPosition = new Vector2(20f, 0f);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = panelColor;

        // ── 4. Tính tổng chiều cao ───────────────────────────────────────
        int sectionCount = 0;
        foreach (var cfg in Sliders)
            if (cfg.section != null) sectionCount++;

        float totalH = padding
            + sectionCount * (sectionHeight + 4f)
            + Sliders.Length * (rowHeight + 4f)
            + 40f  // StatusLabel
            + padding;

        panelRect.sizeDelta = new Vector2(panelWidth, totalH);

        // ── 5. Tạo Content (vertical layout) ────────────────────────────
        var content = CreateUIObject("Content", panel.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(padding, padding);
        contentRect.offsetMax = new Vector2(-padding, -padding);

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.childControlHeight  = false;
        vlg.childControlWidth   = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        // ── 6. Tìm hoặc tạo AudioProcessorUI ────────────────────────────
        AudioProcessorUI uiScript = FindObjectOfType<AudioProcessorUI>();
        if (uiScript == null)
        {
            var uiGO = new GameObject("AudioProcessorUIController");
            uiScript = uiGO.AddComponent<AudioProcessorUI>();
            Undo.RegisterCreatedObjectUndo(uiGO, "Create AudioProcessorUI");
            Debug.Log("[UIBuilder] Đã tạo AudioProcessorUIController mới.");
        }

        var so = new SerializedObject(uiScript);

        // ── 7. Tạo từng row ──────────────────────────────────────────────
        foreach (var cfg in Sliders)
        {
            // Section header
            if (cfg.section != null)
                CreateSectionHeader(content.transform, cfg.section);

            // Row
            var (sliderComp, valueLabel) = CreateSliderRow(content.transform, cfg);

            // Liên kết vào SerializedObject
            var sliderProp = so.FindProperty(cfg.fieldNameSlider);
            if (sliderProp != null)
                sliderProp.objectReferenceValue = sliderComp;

            var labelProp = so.FindProperty(cfg.fieldNameLabel);
            if (labelProp != null)
                labelProp.objectReferenceValue = valueLabel;
        }

        // ── 8. Status label ──────────────────────────────────────────────
        var statusGO = CreateUIObject("StatusLabel", content.transform);
        SetHeight(statusGO, 36f);
        var statusText = statusGO.AddComponent<TextMeshProUGUI>();
        statusText.text      = "Console initialized. Ready.";
        statusText.fontSize  = fontSize - 2f;
        statusText.fontStyle = FontStyles.Italic;
        statusText.color     = new Color(0.55f, 0.60f, 0.70f, 1f);
        statusText.alignment = TextAlignmentOptions.Left;

        var statusProp = so.FindProperty("statusLabel");
        if (statusProp != null)
            statusProp.objectReferenceValue = statusText;

        // ── 9. Apply và hoàn thành ───────────────────────────────────────
        so.ApplyModifiedProperties();
        Undo.RegisterCreatedObjectUndo(panel, "Build Audio Processor UI");
        EditorUtility.SetDirty(uiScript);
        Selection.activeGameObject = panel;

        Debug.Log($"[UIBuilder] Hoàn thành! Đã tạo {Sliders.Length} sliders và liên kết vào AudioProcessorUI.");
        EditorUtility.DisplayDialog("Thành công!",
            $"Đã tạo {Sliders.Length} slider rows.\nPanel: AudioProcessorPanel\nController: {uiScript.gameObject.name}", "OK");
    }

    // ────────────────────────────────────────────────────────────────────
    // Helpers tạo UI elements
    // ────────────────────────────────────────────────────────────────────

    private void CreateSectionHeader(Transform parent, string text)
    {
        var go = CreateUIObject("Section_" + text, parent);
        SetHeight(go, sectionHeight);
        var img = go.AddComponent<Image>();
        img.color = sectionColor;

        var labelGO = CreateUIObject("Text", go.transform);
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = text.ToUpper();
        tmp.fontSize  = fontSize - 2f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
    }

    private (Slider slider, TextMeshProUGUI valueLabel) CreateSliderRow(Transform parent, SliderConfig cfg)
    {
        // Row container
        var row = CreateUIObject("Row_" + cfg.fieldNameSlider, parent);
        SetHeight(row, rowHeight);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = rowBgColor;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth   = false;
        hlg.childControlHeight  = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.padding  = new RectOffset(10, 10, 0, 0);
        hlg.spacing  = 8f;

        // -- Tên slider (bên trái) --
        var nameGO = CreateUIObject("Name", row.transform);
        SetWidth(nameGO, 140f);
        var nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
        nameTmp.text      = cfg.displayName;
        nameTmp.fontSize  = fontSize;
        nameTmp.color     = labelColor;
        nameTmp.alignment = TextAlignmentOptions.Left;
        nameTmp.verticalAlignment = VerticalAlignmentOptions.Middle;

        // -- Slider (giữa) --
        var sliderGO = CreateSliderObject("Slider", row.transform, cfg);
        SetWidth(sliderGO, panelWidth - 140f - 70f - 36f);  // còn lại sau name + value label + padding
        var sliderComp = sliderGO.GetComponent<Slider>();

        // -- Label giá trị (bên phải) --
        var valueGO = CreateUIObject("ValueLabel", row.transform);
        SetWidth(valueGO, 70f);
        var valueTmp = valueGO.AddComponent<TextMeshProUGUI>();
        valueTmp.text      = cfg.defaultValueText;
        valueTmp.fontSize  = fontSize;
        valueTmp.color     = valueColor;
        valueTmp.fontStyle = FontStyles.Bold;
        valueTmp.alignment = TextAlignmentOptions.Right;
        valueTmp.verticalAlignment = VerticalAlignmentOptions.Middle;

        return (sliderComp, valueTmp);
    }

    private GameObject CreateSliderObject(string name, Transform parent, SliderConfig cfg)
    {
        var go = CreateUIObject(name, parent);
        var slider = go.AddComponent<Slider>();
        slider.minValue = cfg.minValue;
        slider.maxValue = cfg.maxValue;
        slider.value    = cfg.defaultValue;

        // Background
        var bgGO = CreateUIObject("Background", go.transform);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.25f, 0.25f, 0.30f, 1f);

        // Fill Area
        var fillAreaGO = CreateUIObject("Fill Area", go.transform);
        var fillAreaRect = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(5f, 0f);
        fillAreaRect.offsetMax = new Vector2(-5f, 0f);

        var fillGO = CreateUIObject("Fill", fillAreaGO.transform);
        var fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = new Vector2(10f, 0f);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = sliderFillColor;

        // Handle Slide Area
        var handleAreaGO = CreateUIObject("Handle Slide Area", go.transform);
        var handleAreaRect = handleAreaGO.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        var handleGO = CreateUIObject("Handle", handleAreaGO.transform);
        var handleRect = handleGO.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(0f, 1f);
        handleRect.offsetMin = new Vector2(-10f, 0f);
        handleRect.offsetMax = new Vector2(10f, 0f);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = Color.white;

        // Liên kết references nội bộ của Slider
        slider.fillRect   = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;
        slider.direction  = Slider.Direction.LeftToRight;

        // Màu transition
        ColorBlock colors = slider.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.90f, 1f);
        colors.pressedColor     = sliderFillColor;
        slider.colors = colors;

        return go;
    }

    // ── Utility ──────────────────────────────────────────────────────────

    private GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        return go;
    }

    private void SetHeight(GameObject go, float h)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = h;
        le.minHeight = h;
    }

    private void SetWidth(GameObject go, float w)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.minWidth = w;
    }

    private void DrawSeparator()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.4f));
        EditorGUILayout.Space(2);
    }
}