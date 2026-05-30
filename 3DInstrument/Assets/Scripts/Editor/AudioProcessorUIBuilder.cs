// Assets/Scripts/Editor/AudioProcessorUIBuilder.cs
// Menu: Tools → Build Audio Processor UI

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

public class AudioProcessorUIBuilder : EditorWindow
{
    private struct SliderConfig
    {
        public string fieldSlider, fieldLabel, displayName, defaultText, section;
        public float  min, max, defaultVal;
    }

    private static readonly SliderConfig[] Sliders =
    {
        new SliderConfig { section="EQ",         fieldSlider="sliderLowPass",       fieldLabel="labelLowPass",       displayName="Low Pass",       defaultText="22.0 kHz", min=200f,  max=22000f, defaultVal=22000f },
        new SliderConfig { section=null,         fieldSlider="sliderHighPass",      fieldLabel="labelHighPass",      displayName="High Pass",      defaultText="20 Hz",    min=20f,   max=2000f,  defaultVal=20f    },
        new SliderConfig { section=null,         fieldSlider="sliderMidEQ",         fieldLabel="labelMidEQ",         displayName="Mid EQ (~1kHz)", defaultText="0.0 dB",   min=-12f,  max=12f,    defaultVal=0f     },
        new SliderConfig { section="Reverb",     fieldSlider="sliderReverbWet",     fieldLabel="labelReverbWet",     displayName="Wet Level",      defaultText="0%",       min=0f,    max=1f,     defaultVal=0f     },
        new SliderConfig { section=null,         fieldSlider="sliderReverbDecay",   fieldLabel="labelReverbDecay",   displayName="Decay Time",     defaultText="1.00 s",   min=0.1f,  max=10f,    defaultVal=1f     },
        new SliderConfig { section="Compressor", fieldSlider="sliderCompThreshold", fieldLabel="labelCompThreshold", displayName="Threshold",      defaultText="0.0 dB",   min=-60f,  max=0f,     defaultVal=0f     },
        new SliderConfig { section=null,         fieldSlider="sliderCompMakeupGain",fieldLabel="labelCompMakeupGain",displayName="Make Up Gain",   defaultText="0.0 dB",   min=0f,    max=20f,    defaultVal=0f     },
        new SliderConfig { section="Master",     fieldSlider="sliderMasterVolume",  fieldLabel="labelMasterVolume",  displayName="Master Volume",  defaultText="100%",     min=0f,    max=1f,     defaultVal=1f     },
    };

    // ── Colors ──────────────────────────────────────────────────────────
    private Color panelBg     = new Color(0.08f, 0.08f, 0.11f, 0.97f);
    private Color sectionBg   = new Color(0.15f, 0.40f, 0.80f, 1f);
    private Color rowBg       = new Color(0.13f, 0.13f, 0.17f, 1f);
    private Color transportBg = new Color(0.10f, 0.25f, 0.10f, 1f);
    private Color uploadBg    = new Color(0.22f, 0.14f, 0.08f, 1f);

    private float panelWidth  = 500f;
    private float rowH        = 52f;
    private float sectionH    = 26f;
    private float buttonH     = 38f;
    private float padding     = 12f;
    private bool  replace     = false;

    [MenuItem("Tools/Build Audio Processor UI")]
    public static void Open()
    {
        GetWindow<AudioProcessorUIBuilder>("Audio UI Builder").minSize = new Vector2(340, 260);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Audio Processor UI Builder",
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });
        EditorGUILayout.Space(4);

        panelWidth = EditorGUILayout.FloatField("Panel Width", panelWidth);
        rowH       = EditorGUILayout.FloatField("Row Height", rowH);
        replace    = EditorGUILayout.Toggle("Replace Existing", replace);

        EditorGUILayout.Space(8);

        var prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
        if (GUILayout.Button("⚡  Build UI", GUILayout.Height(36)))
        {
            Build();
        }
        GUI.backgroundColor = prev;
    }

    private void Build()
    {
        // ── Canvas ──────────────────────────────────────────────────────
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var cGO   = new GameObject("Canvas");
            canvas    = cGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs    = cGO.AddComponent<CanvasScaler>();
            cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cGO.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(cGO, "Create Canvas");
        }

        // ── EventSystem ─────────────────────────────────────────────────
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // ── Remove old ──────────────────────────────────────────────────
        if (replace)
        {
            var old = GameObject.Find("AudioProcessorPanel");
            if (old != null) Undo.DestroyObjectImmediate(old);
        }

        // ── Panel ───────────────────────────────────────────────────────
        var panel   = MakeGO("AudioProcessorPanel", canvas.transform);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0f, 0.5f);
        panelRT.anchorMax        = new Vector2(0f, 0.5f);
        panelRT.pivot            = new Vector2(0f, 0.5f);
        panelRT.anchoredPosition = new Vector2(16f, 0f);
        panel.AddComponent<Image>().color = panelBg;

        // ── AudioProcessorUI component ───────────────────────────────────
        var uiComp = panel.AddComponent<AudioProcessorUI>();
        var so     = new SerializedObject(uiComp);

        // ── Content ──────────────────────────────────────────────────────
        var content   = MakeGO("Content", panel.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = Vector2.zero;
        contentRT.anchorMax = Vector2.one;
        contentRT.offsetMin = new Vector2(padding, padding);
        contentRT.offsetMax = new Vector2(-padding, -padding);
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing            = 5f;
        vlg.childControlHeight = false;
        vlg.childControlWidth  = true;
        vlg.childForceExpandHeight = false;

        float totalH = padding * 2f;

        // ── Upload section ───────────────────────────────────────────────
        totalH += MakeUploadSection(content.transform, so);

        // ── Transport section ────────────────────────────────────────────
        totalH += MakeTransportSection(content.transform, so);

        // ── Slider rows ──────────────────────────────────────────────────
        foreach (var cfg in Sliders)
        {
            if (cfg.section != null)
                totalH += MakeSectionHeader(content.transform, cfg.section);
            totalH += MakeSliderRow(content.transform, so, cfg);
        }

        // ── Status label ─────────────────────────────────────────────────
        var statusGO  = MakeGO("StatusLabel", content.transform);
        SetH(statusGO, 26f);
        var statusTMP = statusGO.AddComponent<TextMeshProUGUI>();
        statusTMP.text      = "Ready.";
        statusTMP.fontSize  = 12f;
        statusTMP.fontStyle = FontStyles.Italic;
        statusTMP.color     = new Color(0.55f, 0.62f, 0.72f);
        statusTMP.alignment = TextAlignmentOptions.Left;
        SafeSet(so, "statusLabel", statusTMP);
        totalH += 30f;

        panelRT.sizeDelta = new Vector2(panelWidth, totalH);
        so.ApplyModifiedProperties();

        Selection.activeGameObject = panel;
        Debug.Log("[AudioProcessorUIBuilder] ✓ Build complete.");
    }

    // ── Upload ───────────────────────────────────────────────────────────
    private float MakeUploadSection(Transform parent, SerializedObject so)
    {
        float h = sectionH + buttonH + buttonH * 0.5f + padding * 2f + 12f;
        var sec = MakeSection("UploadSection", parent, h, uploadBg);

        MakeSectionLabel("Upload Audio", sec.transform);

        // Row: InputField + Browse + Load buttons
        var row = MakeGO("Row_Upload", sec.transform);
        SetH(row, buttonH);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childControlHeight    = true;
        hlg.childControlWidth     = true;
        hlg.childForceExpandWidth = false;

        // InputField
        var inputGO  = MakeGO("PathInput", row.transform);
        SetH(inputGO, buttonH);
        var inputLE  = inputGO.GetComponent<LayoutElement>();
        inputLE.flexibleWidth = 1f;
        inputGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

        // Text Area (viewport) — cần RectMask2D để TMP_InputField hoạt động
        var textArea = MakeGO("Text Area", inputGO.transform);
        StretchRT(textArea, 6f, 2f);
        textArea.AddComponent<RectMask2D>();

        var textGO  = MakeGO("Text", textArea.transform);
        StretchRT(textGO, 0f, 0f);
        var textTMP = textGO.AddComponent<TextMeshProUGUI>();
        textTMP.fontSize  = 12f;
        textTMP.color     = Color.white;
        textTMP.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;

        var phGO  = MakeGO("Placeholder", textArea.transform);
        StretchRT(phGO, 0f, 0f);
        var phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text      = "Nhấn Browse hoặc nhập path...";
        phTMP.fontSize  = 11f;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.color     = new Color(0.4f, 0.4f, 0.5f);
        phTMP.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;

        var inputField           = inputGO.AddComponent<TMP_InputField>();
        inputField.textViewport  = textArea.GetComponent<RectTransform>();
        inputField.textComponent = textTMP;
        inputField.placeholder   = phTMP;
        inputField.targetGraphic = inputGO.GetComponent<Image>();
        inputField.lineType      = TMP_InputField.LineType.SingleLine;
        SafeSet(so, "pathInputField", inputField);

        // Browse button
        var browseBtn = MakeButton("Browse", row.transform, new Color(0.35f, 0.45f, 0.65f), 70f);
        SafeSet(so, "browseButton", browseBtn);

        // Load button
        var loadBtn = MakeButton("Load", row.transform, new Color(0.8f, 0.5f, 0.1f), 100f);
        SafeSet(so, "loadButton", loadBtn);

        // Loading bar
        var barGO = MakeGO("LoadingBar", sec.transform);
        SetH(barGO, buttonH * 0.5f);
        barGO.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f);
        var barFillArea = MakeGO("Fill Area", barGO.transform);
        StretchRT(barFillArea, 5f, 0f);
        var barFill    = MakeGO("Fill", barFillArea.transform);
        barFill.AddComponent<Image>().color = new Color(0.25f, 0.75f, 0.35f);
        var barFillRT  = barFill.GetComponent<RectTransform>();
        barFillRT.anchorMin = new Vector2(0f, 0f);
        barFillRT.anchorMax = new Vector2(0f, 1f);
        barFillRT.sizeDelta = Vector2.zero;
        var barSlider       = barGO.AddComponent<Slider>();
        barSlider.fillRect  = barFillRT;
        barSlider.interactable = false;
        barSlider.value     = 0f;
        SafeSet(so, "loadingBar", barSlider);

        return h + 5f;
    }

    // ── Transport ────────────────────────────────────────────────────────
    private float MakeTransportSection(Transform parent, SerializedObject so)
    {
        float h = sectionH + buttonH + buttonH + padding * 2f + 12f;
        var sec = MakeSection("TransportSection", parent, h, transportBg);

        MakeSectionLabel("Transport", sec.transform);

        // Play Stop Spawn buttons
        var btnRow = MakeGO("ButtonRow", sec.transform);
        SetH(btnRow, buttonH);
        var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childControlHeight    = true;
        hlg.childControlWidth     = true;
        hlg.childForceExpandWidth = false;

        float bw = (panelWidth - padding * 2f - 12f) / 3f;
        var play  = MakeButton("▶ Play",  btnRow.transform, new Color(0.2f, 0.7f, 0.3f), bw);
        var stop  = MakeButton("■ Stop",  btnRow.transform, new Color(0.7f, 0.2f, 0.2f), bw);
        var spawn = MakeButton("+ Spawn", btnRow.transform, new Color(0.2f, 0.4f, 0.7f), bw);
        SafeSet(so, "playButton",  play);
        SafeSet(so, "stopButton",  stop);
        SafeSet(so, "spawnButton", spawn);

        // Dropdown
        var ddRow = MakeGO("DropdownRow", sec.transform);
        SetH(ddRow, buttonH);
        var ddHL = ddRow.AddComponent<HorizontalLayoutGroup>();
        ddHL.childControlHeight    = true;
        ddHL.childForceExpandWidth = true;

        var ddGO  = MakeGO("SourceDropdown", ddRow.transform);
        ddGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.20f);
        var ddLblGO = MakeGO("Label", ddGO.transform);
        StretchRT(ddLblGO, 8f, 2f);
        var ddTMP   = ddLblGO.AddComponent<TextMeshProUGUI>();
        ddTMP.fontSize  = 13f;
        ddTMP.color     = Color.white;
        ddTMP.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        var dd          = ddGO.AddComponent<TMP_Dropdown>();
        dd.targetGraphic = ddGO.GetComponent<Image>();
        dd.captionText   = ddTMP;
        dd.options.Add(new TMP_Dropdown.OptionData("— All Sources —"));
        SafeSet(so, "sourceDropdown", dd);

        return h + 5f;
    }

    // ── Section header ───────────────────────────────────────────────────
    private float MakeSectionHeader(Transform parent, string text)
    {
        var go  = MakeGO("Section_" + text, parent);
        SetH(go, sectionH);
        go.AddComponent<Image>().color = sectionBg;

        var lGO  = MakeGO("Label", go.transform);
        StretchRT(lGO, 10f, 0f);
        var tmp  = lGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = text.ToUpper();
        tmp.fontSize  = 12f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
        return sectionH + 5f;
    }

    // ── Slider row ───────────────────────────────────────────────────────
    private float MakeSliderRow(Transform parent, SerializedObject so, SliderConfig cfg)
    {
        var row = MakeGO("Row_" + cfg.fieldSlider, parent);
        SetH(row, rowH);
        row.AddComponent<Image>().color = rowBg;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding            = new RectOffset(8, 8, 0, 0);
        hlg.spacing            = 6f;
        hlg.childControlHeight = true;
        hlg.childControlWidth  = true;
        hlg.childForceExpandWidth = false;

        // Name label
        var nameGO  = MakeGO("Label_" + cfg.displayName, row.transform);
        SetH(nameGO, rowH);
        nameGO.AddComponent<LayoutElement>().preferredWidth = 130f;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = cfg.displayName;
        nameTMP.fontSize  = 13f;
        nameTMP.color     = new Color(0.75f, 0.82f, 0.92f);
        nameTMP.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;

        // Slider
        var sliderGO = MakeSlider(cfg.displayName + "_Slider", row.transform, cfg.min, cfg.max, cfg.defaultVal);
        sliderGO.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // Value label
        var valGO  = MakeGO("Val_" + cfg.fieldLabel, row.transform);
        SetH(valGO, rowH);
        valGO.AddComponent<LayoutElement>().preferredWidth = 80f;
        var valTMP = valGO.AddComponent<TextMeshProUGUI>();
        valTMP.text      = cfg.defaultText;
        valTMP.fontSize  = 13f;
        valTMP.color     = Color.white;
        valTMP.alignment = TextAlignmentOptions.Right | TextAlignmentOptions.Midline;

        // Link to SerializedObject
        SafeSet(so, cfg.fieldSlider, sliderGO.GetComponent<Slider>());
        SafeSet(so, cfg.fieldLabel,  valTMP);

        return rowH + 5f;
    }

    // ── Slider primitive ─────────────────────────────────────────────────
    private GameObject MakeSlider(string name, Transform parent, float min, float max, float val)
    {
        var go = MakeGO(name, parent);
        SetH(go, 20f);
        go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f);

        // Fill Area
        var fillArea   = MakeGO("Fill Area", go.transform);
        var fillAreaRT = fillArea.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0f);
        fillAreaRT.anchorMax = new Vector2(1f, 1f);
        fillAreaRT.offsetMin = new Vector2(5f,  0f);
        fillAreaRT.offsetMax = new Vector2(-5f, 0f);

        // Fill
        var fill    = MakeGO("Fill", fillArea.transform);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.25f, 0.55f, 1f);
        var fillRT    = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.sizeDelta = Vector2.zero;
        fillRT.pivot     = new Vector2(0f, 0.5f);

        // Handle Slide Area
        var handleArea   = MakeGO("Handle Slide Area", go.transform);
        var handleAreaRT = handleArea.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(10f,  0f);
        handleAreaRT.offsetMax = new Vector2(-10f, 0f);

        // Handle
        var handle    = MakeGO("Handle", handleArea.transform);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.9f, 0.9f, 1f);
        var handleRT  = handle.GetComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0f, 0.5f);
        handleRT.anchorMax = new Vector2(0f, 0.5f);
        handleRT.pivot     = new Vector2(0.5f, 0.5f);
        handleRT.sizeDelta = new Vector2(20f, 20f);

        // Slider component
        var slider        = go.AddComponent<Slider>();
        slider.fillRect   = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.direction  = Slider.Direction.LeftToRight;
        slider.minValue   = min;
        slider.maxValue   = max;
        slider.value      = val;

        var colors = slider.colors;
        colors.normalColor      = new Color(0.9f, 0.9f, 1.0f);
        colors.highlightedColor = new Color(0.6f, 0.8f, 1.0f);
        colors.pressedColor     = new Color(0.3f, 0.6f, 1.0f);
        colors.colorMultiplier  = 1f;
        slider.colors = colors;

        return go;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private GameObject MakeSection(string name, Transform parent, float height, Color color)
    {
        var go = MakeGO(name, parent);
        SetH(go, height);
        go.AddComponent<Image>().color = color;
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding            = new RectOffset((int)padding, (int)padding, 6, 6);
        vlg.spacing            = 4f;
        vlg.childControlHeight = false;
        vlg.childControlWidth  = true;
        return go;
    }

    private void MakeSectionLabel(string text, Transform parent)
    {
        var go  = MakeGO("SectionLabel_" + text, parent);
        SetH(go, sectionH);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 12f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
    }

    private Button MakeButton(string label, Transform parent, Color color, float width)
    {
        var go  = MakeGO("Btn_" + label, parent);
        SetH(go, buttonH);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGO = MakeGO("Text", go.transform);
        StretchRT(textGO, 0f, 0f);
        var tmp    = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 13f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center | TextAlignmentOptions.Midline;

        return btn;
    }

    private static void SetH(GameObject go, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.minHeight       = h;
        le.preferredHeight = h;
    }

    private static void StretchRT(GameObject go, float hPad, float vPad)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(hPad,  vPad);
        rt.offsetMax = new Vector2(-hPad, -vPad);
    }

    private static void SafeSet(SerializedObject so, string propName, UnityEngine.Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null)
            prop.objectReferenceValue = value;
        else
            Debug.LogWarning($"[UIBuilder] Property '{propName}' not found on AudioProcessorUI.");
    }
}

internal static class GameObjectExtensions
{
    public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        => go.GetComponent<T>() ?? go.AddComponent<T>();
}