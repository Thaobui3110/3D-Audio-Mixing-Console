using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Một "loa" 3D trong AudioWorld.
/// Thay thế hoàn toàn Piano / PianoKey / SoundButton.
///
/// HIERARCHY PREFAB:
///   Speaker_01  (GameObject)
///   ├── SpatialSoundObject    ← component này
///   ├── AudioSource           ← tự tạo bởi RequireComponent
///   ├── AudioLowPassFilter    ← tự tạo bởi RequireComponent
///   ├── SphereCollider        ← tự tạo bởi RequireComponent
///   └── AudioReactiveObject  ← component riêng, optional
///
/// AudioSource Settings:
///   spatialBlend = 1 (full 3D)
///   loop         = true
///   playOnAwake  = false
///   minDistance  = 1, maxDistance = 30
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter))]
[RequireComponent(typeof(SphereCollider))]
[DisallowMultipleComponent]
public class SpatialSoundObject : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Identity")]
    [SerializeField] private string sourceID    = "";
    [SerializeField] private string displayName = "Speaker";

    [Header("Audio")]
    [SerializeField] private AudioClip       clip;
    [SerializeField, Range(0f, 1f)]   private float volume     = 0.8f;
    [SerializeField] private AudioMixerGroup mixerGroup;

    [Header("3D Spatial")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    [SerializeField] private float minDistance  = 1f;
    [SerializeField] private float maxDistance  = 30f;

    [Header("Occlusion")]
    [SerializeField] private bool      useOcclusion   = true;
    [SerializeField] private LayerMask occlusionMask  = ~0;
    [SerializeField, Range(200f, 22000f)] private float openCutoff    = 22000f;
    [SerializeField, Range(200f, 22000f)] private float blockedCutoff = 600f;
    [SerializeField, Range(1f, 20f)]      private float occSmooth     = 5f;

    // ── Public readonly ────────────────────────────────────────────────────
    public string SourceID    => sourceID;
    public string DisplayName => displayName;
    public bool   IsPlaying   => audioSource != null && audioSource.isPlaying;

    /// <summary>Gán sourceID sau khi spawn. Gọi bởi SpatialAudioManager.</summary>
    internal void SetSourceID(string id)
    {
        sourceID    = id;
        displayName = id;
    }

    // ── Runtime ────────────────────────────────────────────────────────────
    private AudioSource        audioSource;
    private AudioLowPassFilter lpFilter;
    private Transform          listener;
    private float              currentOcclusion;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        lpFilter    = GetComponent<AudioLowPassFilter>();

        ConfigureAudioSource();
        ConfigureCollider();

        if (string.IsNullOrEmpty(sourceID))
            sourceID = gameObject.name;
    }

    private void Start()
    {
        var al = FindObjectOfType<AudioListener>();
        if (al != null) listener = al.transform;
    }

    private void Update()
    {
        if (useOcclusion) UpdateOcclusion();
    }

    // ── Public API ─────────────────────────────────────────────────────────
    public void SetClip(AudioClip newClip)
    {
        bool wasPlaying = IsPlaying;
        audioSource.Stop();
        clip = newClip;
        audioSource.clip = clip;
        if (wasPlaying && clip != null) audioSource.Play();
    }

    public void Play()
    {
        if (clip == null) { Debug.LogWarning($"[{name}] Chưa có AudioClip.", this); return; }
        audioSource.Play();
    }

    public void Stop()   => audioSource.Stop();
    public void Pause()  => audioSource.Pause();
    public void Resume() => audioSource.UnPause();

    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        audioSource.volume = volume;
    }

    public void SetMixerGroup(AudioMixerGroup group)
    {
        mixerGroup = group;
        audioSource.outputAudioMixerGroup = group;
    }

    // ── Private ────────────────────────────────────────────────────────────
    private void ConfigureAudioSource()
    {
        audioSource.clip                  = clip;
        audioSource.volume                = volume;
        audioSource.playOnAwake           = false;
        audioSource.loop                  = true;
        audioSource.spatialBlend          = spatialBlend;
        audioSource.minDistance           = minDistance;
        audioSource.maxDistance           = maxDistance;
        audioSource.rolloffMode           = AudioRolloffMode.Logarithmic;
        audioSource.dopplerLevel          = 0.3f;
        audioSource.outputAudioMixerGroup = mixerGroup;
    }

    private void ConfigureCollider()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = 0.6f;
    }

    private void UpdateOcclusion()
    {
        float target = 0f;
        if (listener != null)
        {
            Vector3 dir  = listener.position - transform.position;
            float   dist = dir.magnitude;
            if (dist > 0.01f && Physics.Raycast(transform.position, dir.normalized, dist, occlusionMask,
                                                  QueryTriggerInteraction.Ignore))
                target = 1f;
        }

        currentOcclusion = Mathf.Lerp(currentOcclusion, target, Time.deltaTime * occSmooth);
        lpFilter.cutoffFrequency = Mathf.Lerp(openCutoff, blockedCutoff, currentOcclusion);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var src = GetComponent<AudioSource>();
        if (src == null) return;
        src.spatialBlend = spatialBlend;
        src.minDistance  = minDistance;
        src.maxDistance  = maxDistance;
        src.volume       = volume;
    }

    private void OnDrawGizmosSelected()
    {
        // Min / Max distance rings
        Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, minDistance);
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.12f);
        Gizmos.DrawWireSphere(transform.position, maxDistance);

        // Occlusion line to listener
        if (listener != null)
        {
            Gizmos.color = Color.Lerp(Color.green, Color.red, currentOcclusion);
            Gizmos.DrawLine(transform.position, listener.position);
        }
    }
#endif
}