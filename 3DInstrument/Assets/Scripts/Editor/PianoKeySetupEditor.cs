using UnityEngine;
using UnityEngine.Audio;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor tool tự động setup toàn bộ PianoKey con của Piano.
///
/// Cách dùng:
///   1. Chọn GameObject Piano trong Hierarchy
///   2. Menu trên: Tools → Piano Key Setup
///   3. Nhấn "Auto Setup All Keys"
///
/// Hoặc right-click Piano GameObject → "Setup Piano Keys"
/// </summary>
public class PianoKeySetupEditor : EditorWindow
{
    // ── Note map: tên nốt → (KeyCode, MIDI, isBlack) ──────────────────────
    private static readonly Dictionary<string, (KeyCode key, int midi, bool isBlack)> NoteMap
        = new Dictionary<string, (KeyCode, int, bool)>
    {
        { "C3",  (KeyCode.A, 48, false) },
        { "Cs3", (KeyCode.None, 49, true)  }, // C#3 / Db3
        { "D3",  (KeyCode.S, 50, false) },
        { "Ds3", (KeyCode.None, 51, true)  }, // D#3
        { "E3",  (KeyCode.D, 52, false) },
        { "F3",  (KeyCode.F, 53, false) },
        { "Fs3", (KeyCode.None, 54, true)  }, // F#3
        { "G3",  (KeyCode.G, 55, false) },
        { "Gs3", (KeyCode.None, 56, true)  }, // G#3
        { "A3",  (KeyCode.H, 57, false) },
        { "As3", (KeyCode.None, 58, true)  }, // A#3
        { "B3",  (KeyCode.J, 59, false) },
        { "C4",  (KeyCode.K, 60, false) },
    };

    // ── Window state ───────────────────────────────────────────────────────
    private GameObject    pianoRoot;
    private AudioMixerGroup mixerGroup;
    private AudioClip[]   clips       = new AudioClip[13];    // C3–C4 gồm cả phím đen
    private bool          autoFindClips = true;
    private bool          addOcclusion  = true;
    private bool          addLowPass    = true;
    private float         spatialBlend  = 1f;
    private float         minDist       = 1f;
    private float         maxDist       = 20f;

    private Vector2 scroll;
    private string  log = "";

    private static readonly string[] NOTE_NAMES =
        { "C3","C#3","D3","D#3","E3","F3","F#3","G3","G#3","A3","A#3","B3","C4" };
    private static readonly string[] NOTE_KEYS =
        { "C3","Cs3","D3","Ds3","E3","F3","Fs3","G3","Gs3","A3","As3","B3","C4" };

    // ── Menu items ─────────────────────────────────────────────────────────
    [MenuItem("Tools/Piano Key Setup")]
    public static void OpenWindow()
    {
        var w = GetWindow<PianoKeySetupEditor>("Piano Key Setup");
        w.minSize = new Vector2(420, 580);
    }

    [MenuItem("GameObject/Setup Piano Keys", false, 20)]
    public static void SetupFromHierarchy()
    {
        var w = GetWindow<PianoKeySetupEditor>("Piano Key Setup");
        w.pianoRoot = Selection.activeGameObject;
        w.minSize   = new Vector2(420, 580);
    }

    // ── GUI ────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        // ── Header ──
        EditorGUILayout.Space(8);
        GUIStyle title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        EditorGUILayout.LabelField("Piano Key Auto Setup", title);
        EditorGUILayout.LabelField("Tự động điền SoundID, KeyCode, Clip cho tất cả Key_XX", EditorStyles.miniLabel);
        EditorGUILayout.Space(6);
        DrawSeparator();

        // ── Piano root ──
        EditorGUILayout.LabelField("1. Chọn Piano GameObject", EditorStyles.boldLabel);
        pianoRoot = (GameObject)EditorGUILayout.ObjectField("Piano Root", pianoRoot, typeof(GameObject), true);
        if (pianoRoot == null && Selection.activeGameObject != null)
        {
            if (GUILayout.Button("Dùng Selection hiện tại: " + Selection.activeGameObject.name))
                pianoRoot = Selection.activeGameObject;
        }
        EditorGUILayout.Space(4);

        // ── Mixer ──
        DrawSeparator();
        EditorGUILayout.LabelField("2. Audio Mixer Group", EditorStyles.boldLabel);
        mixerGroup = (AudioMixerGroup)EditorGUILayout.ObjectField("Mixer Group", mixerGroup, typeof(AudioMixerGroup), false);
        EditorGUILayout.Space(4);

        // ── Spatial settings ──
        DrawSeparator();
        EditorGUILayout.LabelField("3. Spatial Audio", EditorStyles.boldLabel);
        spatialBlend = EditorGUILayout.Slider("Spatial Blend", spatialBlend, 0f, 1f);
        minDist      = EditorGUILayout.FloatField("Min Distance", minDist);
        maxDist      = EditorGUILayout.FloatField("Max Distance", maxDist);
        addLowPass   = EditorGUILayout.Toggle("Add AudioLowPassFilter", addLowPass);
        addOcclusion = EditorGUILayout.Toggle("Add OcclusionProcessor", addOcclusion);
        EditorGUILayout.Space(4);

        // ── Clips ──
        DrawSeparator();
        EditorGUILayout.LabelField("4. Audio Clips", EditorStyles.boldLabel);
        autoFindClips = EditorGUILayout.Toggle("Tự tìm clip theo tên (C3, D3...)", autoFindClips);

        if (!autoFindClips)
        {
            EditorGUILayout.HelpBox("Kéo AudioClip theo thứ tự C3 → C4 (13 nốt kể cả phím đen)", MessageType.Info);
            for (int i = 0; i < 13; i++)
            {
                clips[i] = (AudioClip)EditorGUILayout.ObjectField(
                    NOTE_NAMES[i], clips[i], typeof(AudioClip), false
                );
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Script sẽ tìm clip trong project có tên chứa tên nốt (vd: 'Piano_C3', 'note_C3'...).\n" +
                "Đảm bảo AudioClip được đặt tên rõ ràng.", MessageType.Info);
        }
        EditorGUILayout.Space(8);

        // ── Buttons ──
        DrawSeparator();
        EditorGUILayout.Space(4);

        GUI.enabled = pianoRoot != null;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 36, fontSize = 13 };
        Color prev = GUI.backgroundColor;

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("⚡  Auto Setup All Keys", btnStyle))
            RunSetup();

        GUI.backgroundColor = prev;
        EditorGUILayout.Space(4);

        if (GUILayout.Button("🔍  Preview — chỉ hiện thông tin, không thay đổi gì"))
            RunPreview();

        if (GUILayout.Button("✕  Xóa toàn bộ PianoKey components (giữ AudioSource)"))
        {
            if (EditorUtility.DisplayDialog("Xác nhận", "Xóa toàn bộ PianoKey trên các child?", "Xóa", "Hủy"))
                RemoveAllPianoKeys();
        }

        GUI.enabled = true;

        // ── Log ──
        if (!string.IsNullOrEmpty(log))
        {
            EditorGUILayout.Space(8);
            DrawSeparator();
            EditorGUILayout.LabelField("Kết quả:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(log, MessageType.None);
        }

        EditorGUILayout.EndScrollView();
    }

    // ── Core logic ─────────────────────────────────────────────────────────
    private void RunSetup()
    {
        if (!pianoRoot) return;

        var children = GetKeyChildren(pianoRoot);
        if (children.Count == 0)
        {
            log = "❌ Không tìm thấy child nào có tên chứa nốt nhạc (C3, D3...).";
            return;
        }

        int success = 0;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        foreach (var child in children)
        {
            string noteKey = DetectNoteKey(child.name);
            if (noteKey == null)
            {
                sb.AppendLine($"⚠ Bỏ qua '{child.name}' — không nhận dạng được nốt");
                continue;
            }

            var info = NoteMap[noteKey];

            Undo.RecordObject(child, "Piano Key Setup");

            // ── PianoKey component ──
            var pianoKey = child.GetComponent<PianoKey>();
            if (!pianoKey)
                pianoKey = Undo.AddComponent<PianoKey>(child);

            // Dùng SerializedObject để set private SerializeField
            var so = new SerializedObject(pianoKey);
            so.FindProperty("soundID").stringValue     = child.name;
            so.FindProperty("displayName").stringValue = NoteDisplayName(noteKey);
            so.FindProperty("keyboardKey").enumValueIndex = (int)info.key;
            so.FindProperty("midiNote").intValue        = info.midi;
            so.FindProperty("isBlackKey").boolValue     = info.isBlack;
            so.FindProperty("spatialBlend").floatValue  = spatialBlend;
            so.FindProperty("minDistance").floatValue   = minDist;
            so.FindProperty("maxDistance").floatValue   = maxDist;

            // Mixer group
            if (mixerGroup)
                so.FindProperty("mixerGroup").objectReferenceValue = mixerGroup;

            // Clip
            AudioClip clip = FindClipForNote(noteKey);
            if (clip)
                so.FindProperty("clip").objectReferenceValue = clip;

            so.ApplyModifiedProperties();

            // ── AudioLowPassFilter ──
            if (addLowPass && !child.GetComponent<AudioLowPassFilter>())
            {
                var lpf = Undo.AddComponent<AudioLowPassFilter>(child);
                lpf.cutoffFrequency = 22000f;
            }

            // ── OcclusionProcessor ──
            if (addOcclusion && !child.GetComponent<OcclusionProcessor>())
                Undo.AddComponent<OcclusionProcessor>(child);

            // ── Layer "Instrument" nếu tồn tại ──
            int instrLayer = LayerMask.NameToLayer("Instrument");
            if (instrLayer >= 0) child.layer = instrLayer;

            string clipName = clip ? clip.name : "❌ không tìm thấy clip";
            sb.AppendLine($"✓ {child.name}  →  Key:{info.key}  MIDI:{info.midi}  Clip:{clipName}");
            success++;
        }

        log = $"Hoàn thành: {success}/{children.Count} keys\n\n" + sb.ToString();
        Debug.Log("[PianoKeySetup]\n" + log);
        AssetDatabase.SaveAssets();
    }

    private void RunPreview()
    {
        if (!pianoRoot) return;
        var children = GetKeyChildren(pianoRoot);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Preview — {children.Count} keys được tìm thấy:\n");

        foreach (var child in children)
        {
            string noteKey = DetectNoteKey(child.name);
            if (noteKey == null) { sb.AppendLine($"  ⚠ '{child.name}' — không nhận dạng"); continue; }
            var info = NoteMap[noteKey];
            var clip = FindClipForNote(noteKey);
            sb.AppendLine($"  {child.name}  →  Key:{info.key}  MIDI:{info.midi}  Clip:{(clip ? clip.name : "không tìm thấy")}");
        }

        log = sb.ToString();
    }

    private void RemoveAllPianoKeys()
    {
        if (!pianoRoot) return;
        int count = 0;
        foreach (Transform child in pianoRoot.transform)
        {
            var pk = child.GetComponent<PianoKey>();
            if (pk) { Undo.DestroyObjectImmediate(pk); count++; }
        }
        log = $"Đã xóa {count} PianoKey components.";
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Lấy tất cả child có tên chứa nốt nhạc nhận dạng được.</summary>
    private List<GameObject> GetKeyChildren(GameObject root)
    {
        var result = new List<GameObject>();
        foreach (Transform child in root.transform)
            if (DetectNoteKey(child.name) != null)
                result.Add(child.gameObject);
        return result;
    }

    /// <summary>Nhận dạng nốt từ tên GameObject. Trả về key trong NoteMap hoặc null.</summary>
    private string DetectNoteKey(string name)
    {
        // Thử từng note key — ưu tiên dài trước (Cs3 trước C3) để tránh nhầm
        string upper = name.ToUpper();
        foreach (string k in NOTE_KEYS)
        {
            // Chuẩn hoá: "C#3" → "Cs3", "Db3" → "Cs3", v.v.
            string pattern = k.Replace("s", "#").ToUpper();
            if (upper.Contains(k.ToUpper()) || upper.Contains(pattern))
                return k;
        }
        return null;
    }

    /// <summary>Tìm AudioClip trong project theo tên nốt.</summary>
    private AudioClip FindClipForNote(string noteKey)
    {
        // Nếu user gán tay thì dùng
        int idx = System.Array.IndexOf(NOTE_KEYS, noteKey);
        if (!autoFindClips && idx >= 0 && clips[idx] != null)
            return clips[idx];

        // Auto-find: tìm asset tên có chứa nốt
        string[] guids = AssetDatabase.FindAssets($"t:AudioClip {noteKey}");
        if (guids.Length == 0)
        {
            // Thử tên không có 's' (C#3 thay vì Cs3)
            string alt = noteKey.Replace("s", "#");
            guids = AssetDatabase.FindAssets($"t:AudioClip {alt}");
        }
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));

        return null;
    }

    private string NoteDisplayName(string noteKey) =>
        noteKey.Replace("s", "#");

    private void DrawSeparator()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorGUILayout.Space(2);
    }
}