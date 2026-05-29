using UnityEngine;

/// <summary>
/// Refactor PianoKeyController + PianoAudioSource thành một component duy nhất.
/// Gán lên từng child object (Key_C3, Key_D3...) của Piano.
///
/// Migration:
///   - Xóa PianoKeyController và PianoAudioSource khỏi mỗi Key_XX
///   - Add PianoKey thay thế
///   - Kéo AudioClip vào field Clip
///   - Set đúng KeyboardKey và MidiNote
/// </summary>
public class PianoKey : SoundObjectBase
{
    [Header("Piano Key")]
    [SerializeField] private KeyCode keyboardKey = KeyCode.None;
    [SerializeField] private int     midiNote    = 60;
    [SerializeField] private bool    isBlackKey  = false;

    [Header("Key Animation")]
    [SerializeField] private float rotateAngle = 3f;
    [SerializeField] private float animSpeed   = 16f;

    // Keyboard → Note mapping mặc định (C3–C4)
    // A=C3  S=D3  D=E3  F=F3  G=G3  H=A3  J=B3  K=C4
    private static readonly (KeyCode key, int midi, string note)[] DefaultMap =
    {
        (KeyCode.A, 48, "C3"), (KeyCode.S, 50, "D3"), (KeyCode.D, 52, "E3"),
        (KeyCode.F, 53, "F3"), (KeyCode.G, 55, "G3"), (KeyCode.H, 57, "A3"),
        (KeyCode.J, 59, "B3"), (KeyCode.K, 60, "C4"),
    };

    public KeyCode KeyboardKey => keyboardKey;
    public int     MidiNote    => midiNote;
    public bool    IsBlackKey  => isBlackKey;

    private Quaternion restRotation;
    private Quaternion pressedRotation;
    private bool       pressing;
    private float      pressT;

    protected override void Awake()
    {
        base.Awake();
        restRotation    = transform.localRotation;
        pressedRotation = Quaternion.Euler(
            transform.localEulerAngles + new Vector3(rotateAngle, 0f, 0f)
        );
    }

    protected override void Update()
    {
        base.Update();

        if (keyboardKey != KeyCode.None && Input.GetKeyDown(keyboardKey))
            TriggerSound(0.85f);

        AnimateKey();
    }

    protected override void OnSoundTriggered(float velocity)
    {
        pressing = true;
        pressT   = 0f;
    }

    private void AnimateKey()
    {
        if (!pressing) return;
        pressT += Time.deltaTime * animSpeed;
        float t = Mathf.Sin(pressT * Mathf.PI);
        transform.localRotation = Quaternion.Lerp(restRotation, pressedRotation, t);
        if (pressT >= 1f)
        {
            transform.localRotation = restRotation;
            pressing = false;
        }
    }

    // Gọi từ Editor script để tự động điền KeyCode theo tên object (Key_C3, Key_D3...)
    [ContextMenu("Auto-map from GameObject name")]
    private void AutoMapFromName()
    {
        foreach (var (key, midi, note) in DefaultMap)
        {
            if (gameObject.name.Contains(note))
            {
                keyboardKey = key;
                midiNote    = midi;
                SoundID     = gameObject.name;
                Debug.Log($"[PianoKey] Auto-mapped {gameObject.name} → {key} / MIDI {midi}");
                return;
            }
        }
        Debug.LogWarning($"[PianoKey] Cannot auto-map '{gameObject.name}'");
    }
}
