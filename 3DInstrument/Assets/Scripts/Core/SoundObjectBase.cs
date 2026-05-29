using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Base class chứa toàn bộ logic chung: AudioSource, highlight, spatial audio.
/// Kế thừa class này cho mọi loại object phát âm thanh trong scene.
///
/// Hierarchy:
///   SoundObjectBase  (class này)
///   ├── SoundButton  — placeholder 3D mặc định (cube/mesh bất kỳ)
///   └── PianoKey     — từng phím đàn con của Piano GameObject
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Collider))]
public abstract class SoundObjectBase : MonoBehaviour, ISoundObject
{
    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Identity")]
    [SerializeField] private string soundID      = "";
    [SerializeField] private string displayName  = "Sound Object";

    [Header("Audio")]
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)]   private float volume = 1f;
    [SerializeField, Range(0.5f, 2f)] private float pitch  = 1f;
    [SerializeField] private AudioMixerGroup mixerGroup;

    [Header("3D Spatial")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 20f;

    [Header("Visual Feedback")]
    [SerializeField] private Color highlightColor    = new Color(1f, 0.9f, 0.2f);
    [SerializeField] private Color defaultColor      = Color.white;
    [SerializeField] private float highlightDuration = 0.12f;

    // ── Private state ──────────────────────────────────────────────────────
    protected AudioSource   audioSource;
    private   Renderer      objectRenderer;
    private   MaterialPropertyBlock propBlock;
    private   float         highlightTimer;
    private   bool          isHighlighted;

    // ── ISoundObject Properties ────────────────────────────────────────────
    public string SoundID
    {
        get => soundID;
        set => soundID = value;
    }

    public virtual string DisplayName => displayName;

    public AudioClip Clip
    {
        get => clip;
        set { clip = value; if (audioSource) audioSource.clip = clip; }
    }

    public float Volume
    {
        get => volume;
        set { volume = Mathf.Clamp01(value); if (audioSource) audioSource.volume = volume; }
    }

    public float Pitch
    {
        get => pitch;
        set { pitch = Mathf.Clamp(value, 0.5f, 2f); if (audioSource) audioSource.pitch = pitch; }
    }

    public AudioMixerGroup MixerGroup
    {
        get => mixerGroup;
        set { mixerGroup = value; if (audioSource) audioSource.outputAudioMixerGroup = value; }
    }

    public bool IsPlaying => audioSource != null && audioSource.isPlaying;

    // ── Unity Lifecycle ────────────────────────────────────────────────────
    protected virtual void Awake()
    {
        audioSource    = GetComponent<AudioSource>();
        objectRenderer = GetComponentInChildren<Renderer>();
        propBlock      = new MaterialPropertyBlock();

        audioSource.clip                  = clip;
        audioSource.playOnAwake           = false;
        audioSource.spatialBlend          = spatialBlend;
        audioSource.minDistance           = minDistance;
        audioSource.maxDistance           = maxDistance;
        audioSource.rolloffMode           = AudioRolloffMode.Logarithmic;
        audioSource.dopplerLevel          = 0.3f;
        audioSource.outputAudioMixerGroup = mixerGroup;

        if (string.IsNullOrEmpty(soundID))
            soundID = gameObject.name;
    }

    protected virtual void Update()
    {
        if (isHighlighted)
        {
            highlightTimer -= Time.deltaTime;
            if (highlightTimer <= 0f)
                SetHighlight(false);
        }
    }

    // ── ISoundObject Methods ───────────────────────────────────────────────
    public virtual void TriggerSound(float velocity = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning($"[SoundObject] '{soundID}' không có AudioClip.", this);
            return;
        }
        audioSource.volume = Mathf.Clamp01(volume * velocity);
        audioSource.pitch  = pitch;
        audioSource.PlayOneShot(clip);
        SetHighlight(true);
        OnSoundTriggered(velocity);
    }

    public virtual void StopSound() => audioSource.Stop();

    public void SetHighlight(bool active)
    {
        if (objectRenderer == null) return;

        objectRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", active ? highlightColor : defaultColor);
        objectRenderer.SetPropertyBlock(propBlock);

        isHighlighted  = active;
        if (active) highlightTimer = highlightDuration;
    }

    // ── IInteractable ──────────────────────────────────────────────────────
    public virtual void Interact(RaycastHit hit) => TriggerSound(1f);

    public virtual string GetInteractPrompt() => $"[E] {displayName}";

    // ── Hook cho subclass ──────────────────────────────────────────────────
    protected virtual void OnSoundTriggered(float velocity) { }
}
