using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Một "loa" 3D trong AudioWorld.
///
/// Per-object DSP: Low Pass, High Pass, Mid EQ (biquad), Reverb, Compressor
/// Global (mixer): Master Volume
/// Occlusion: RaycastAll → filter Player hierarchy
///
/// Collider: SphereCollider (isTrigger = FALSE) để raycast từ
/// TopDownView và SpeakerGrabber có thể detect được.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter))]
[RequireComponent(typeof(AudioHighPassFilter))]
[RequireComponent(typeof(SphereCollider))]
[DisallowMultipleComponent]
public class SpatialSoundObject : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════
    //  INSPECTOR
    // ══════════════════════════════════════════════════════════════════════

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
    [SerializeField] private float maxDistance  = 20f;

    [Header("Per-Object EQ")]
    [SerializeField, Range(200f, 22000f)]  private float eqLowPassCutoff  = 22000f;
    [SerializeField, Range(20f, 2000f)]    private float eqHighPassCutoff = 20f;
    [SerializeField, Range(-12f, 12f)]     private float eqMidGain        = 0f;
    [SerializeField, Range(500f, 6000f)]   private float eqMidFreq        = 1500f;
    [SerializeField, Range(0.2f, 5f)]      private float eqMidQ           = 1f;

    [Header("Per-Object Reverb")]
    [SerializeField, Range(-10000f, 0f)] private float reverbRoom      = -1000f;
    [SerializeField, Range(0.1f, 20f)]   private float reverbDecayTime = 1.5f;

    [Header("Per-Object Compressor")]
    [SerializeField, Range(-60f, 0f)]  private float compThreshold  = -20f;
    [SerializeField, Range(0f, 20f)]   private float compMakeupGain = 0f;
    [SerializeField, Range(0.001f, 1f)] private float compAttack    = 0.01f;
    [SerializeField, Range(0.01f, 2f)]  private float compRelease   = 0.15f;

    [Header("Occlusion")]
    [SerializeField] private bool      useOcclusion    = true;
    [SerializeField] private LayerMask occlusionMask   = ~0;
    [SerializeField, Range(200f, 22000f)] private float openCutoff    = 22000f;
    [SerializeField, Range(200f, 22000f)] private float blockedCutoff = 600f;
    [SerializeField, Range(1f, 20f)]      private float occSmoothing  = 5f;

    // ══════════════════════════════════════════════════════════════════════
    //  PUBLIC API — Properties
    // ══════════════════════════════════════════════════════════════════════

    public string SourceID    => sourceID;
    public string DisplayName => displayName;
    public bool   IsPlaying   => audioSource != null && audioSource.isPlaying;

    public float Volume           => volume;
    public float EQLowPassCutoff  => eqLowPassCutoff;
    public float EQHighPassCutoff => eqHighPassCutoff;
    public float EQMidGain        => eqMidGain;
    public float ReverbRoom       => reverbRoom;
    public float ReverbDecayTime  => reverbDecayTime;
    public float CompThreshold    => compThreshold;
    public float CompMakeupGain   => compMakeupGain;

    // ══════════════════════════════════════════════════════════════════════
    //  RUNTIME STATE
    // ══════════════════════════════════════════════════════════════════════

    private AudioSource         audioSource;
    private AudioLowPassFilter  lpFilter;
    private AudioHighPassFilter hpFilter;
    private AudioReverbFilter   reverbFilter;
    private Transform           listener;
    private Transform           listenerRoot;   // Player root — loại khỏi occlusion
    private float               currentOcclusion;

    // Mid EQ biquad coefficients (cho OnAudioFilterRead)
    private float bq_a0, bq_a1, bq_a2, bq_b1, bq_b2;
    private float bq_x1L, bq_x2L, bq_y1L, bq_y2L;
    private float bq_x1R, bq_x2R, bq_y1R, bq_y2R;
    private bool  bqDirty = true;

    // Compressor envelope
    private float compEnvelope;

    // Cache sample rate — OnAudioFilterRead chạy trên audio thread,
    // không được gọi AudioSettings.outputSampleRate
    private float sampleRate;

    // ══════════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        sampleRate   = AudioSettings.outputSampleRate;
        audioSource  = GetComponent<AudioSource>();
        lpFilter     = GetComponent<AudioLowPassFilter>();
        hpFilter     = GetComponent<AudioHighPassFilter>();
        reverbFilter = GetComponent<AudioReverbFilter>();

        ConfigureAudioSource();
        ConfigureCollider();
        ApplyAllDSP();

        if (string.IsNullOrEmpty(sourceID))
            sourceID = gameObject.name;
    }

    private void Start()
    {
        var al = FindObjectOfType<AudioListener>();
        if (al != null)
        {
            listener     = al.transform;
            listenerRoot = listener.root;   // Player GO chứa CharacterController
        }
    }

    private void Update()
    {
        if (useOcclusion) UpdateOcclusion();
        if (bqDirty)      RecalcBiquad();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PUBLIC API — Methods
    // ══════════════════════════════════════════════════════════════════════

    internal void SetSourceID(string id)
    {
        sourceID    = id;
        displayName = id;
    }

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

    // ── Per-Object EQ ─────────────────────────────────────────────────────

    public void SetEQLowPass(float cutoff)
    {
        eqLowPassCutoff = Mathf.Clamp(cutoff, 200f, 22000f);
        if (!useOcclusion && lpFilter != null)
            lpFilter.cutoffFrequency = eqLowPassCutoff;
    }

    public void SetEQHighPass(float cutoff)
    {
        eqHighPassCutoff = Mathf.Clamp(cutoff, 20f, 2000f);
        if (hpFilter != null)
            hpFilter.cutoffFrequency = eqHighPassCutoff;
    }

    public void SetEQMidGain(float dB)
    {
        eqMidGain = Mathf.Clamp(dB, -12f, 12f);
        bqDirty = true;
    }

    // ── Per-Object Reverb ─────────────────────────────────────────────────

    public void SetReverbRoom(float room)
    {
        reverbRoom = Mathf.Clamp(room, -10000f, 0f);
        if (reverbFilter != null) reverbFilter.room = reverbRoom;
    }

    public void SetReverbDecay(float decay)
    {
        reverbDecayTime = Mathf.Clamp(decay, 0.1f, 20f);
        if (reverbFilter != null) reverbFilter.decayTime = reverbDecayTime;
    }

    // ── Per-Object Compressor ─────────────────────────────────────────────

    public void SetCompThreshold(float dB)
    {
        compThreshold = Mathf.Clamp(dB, -60f, 0f);
    }

    public void SetCompMakeupGain(float dB)
    {
        compMakeupGain = Mathf.Clamp(dB, 0f, 20f);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PRIVATE — Setup
    // ══════════════════════════════════════════════════════════════════════

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
        // ═══ QUAN TRỌNG: isTrigger = FALSE ═══
        // Nếu true → Physics.Raycast mặc định bỏ qua → TopDownView và
        // SpeakerGrabber không thể click/select speaker.
        col.isTrigger = false;
        col.radius    = 0.6f;
    }

    private void ApplyAllDSP()
    {
        if (lpFilter != null) lpFilter.cutoffFrequency = eqLowPassCutoff;
        if (hpFilter != null)
        {
            hpFilter.enabled = true;
            hpFilter.cutoffFrequency = eqHighPassCutoff;
        }
        if (reverbFilter != null)
        {
            reverbFilter.reverbPreset = AudioReverbPreset.Off;
            reverbFilter.room         = reverbRoom;
            reverbFilter.decayTime    = reverbDecayTime;
        }
        bqDirty = true;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PRIVATE — Occlusion (RaycastAll + filter Player)
    // ══════════════════════════════════════════════════════════════════════

    private void UpdateOcclusion()
    {
        float target = 0f;
        if (listener != null)
        {
            Vector3 dir  = listener.position - transform.position;
            float   dist = dir.magnitude;
            if (dist > 0.01f)
            {
                // RaycastAll để filter — bỏ qua Player hierarchy và chính speaker
                var hits = Physics.RaycastAll(
                    transform.position, dir.normalized, dist,
                    occlusionMask, QueryTriggerInteraction.Ignore);

                foreach (var hit in hits)
                {
                    // Bỏ qua Player (CharacterController, Camera, etc.)
                    if (listenerRoot != null && hit.transform.IsChildOf(listenerRoot))
                        continue;
                    // Bỏ qua chính speaker này
                    if (hit.transform == transform)
                        continue;
                    // Bỏ qua các speaker khác (SphereCollider)
                    if (hit.collider.GetComponent<SpatialSoundObject>() != null)
                        continue;

                    target = 1f;
                    break;
                }
            }
        }

        currentOcclusion = Mathf.Lerp(currentOcclusion, target, Time.deltaTime * occSmoothing);

        if (lpFilter != null)
        {
            // Kết hợp EQ low-pass + occlusion — lấy giá trị nhỏ hơn
            float occCutoff = Mathf.Lerp(openCutoff, blockedCutoff, currentOcclusion);
            lpFilter.cutoffFrequency = Mathf.Min(eqLowPassCutoff, occCutoff);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PRIVATE — Mid EQ Biquad (chạy trên Audio Thread)
    // ══════════════════════════════════════════════════════════════════════

    private void RecalcBiquad()
    {
        bqDirty = false;
        float gainAbs = Mathf.Pow(10f, eqMidGain / 40f);
        float w0 = 2f * Mathf.PI * eqMidFreq / sampleRate;
        float sinW = Mathf.Sin(w0);
        float cosW = Mathf.Cos(w0);
        float alpha = sinW / (2f * eqMidQ);

        float a0_inv;
        if (eqMidGain >= 0f)
        {
            float norm = 1f + alpha / gainAbs;
            a0_inv = 1f / norm;
            bq_a0 = (1f + alpha * gainAbs) * a0_inv;
            bq_a1 = (-2f * cosW)           * a0_inv;
            bq_a2 = (1f - alpha * gainAbs) * a0_inv;
            bq_b1 = bq_a1;
            bq_b2 = (1f - alpha / gainAbs) * a0_inv;
        }
        else
        {
            float norm = 1f + alpha * gainAbs;
            a0_inv = 1f / norm;
            bq_a0 = (1f + alpha / gainAbs) * a0_inv;
            bq_a1 = (-2f * cosW)            * a0_inv;
            bq_a2 = (1f - alpha / gainAbs)  * a0_inv;
            bq_b1 = bq_a1;
            bq_b2 = (1f - alpha * gainAbs)  * a0_inv;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  OnAudioFilterRead — Mid EQ + Compressor (Audio Thread)
    // ══════════════════════════════════════════════════════════════════════

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (channels < 1) return;
        bool doEQ   = Mathf.Abs(eqMidGain) > 0.1f;
        bool doComp = compThreshold > -59.9f;
        if (!doEQ && !doComp) return;

        float attackCoeff  = Mathf.Exp(-1f / (compAttack  * sampleRate));
        float releaseCoeff = Mathf.Exp(-1f / (compRelease * sampleRate));
        float threshLin    = Mathf.Pow(10f, compThreshold / 20f);
        float makeupLin    = Mathf.Pow(10f, compMakeupGain / 20f);

        for (int i = 0; i < data.Length; i += channels)
        {
            // ── Mid EQ (biquad per channel) ──
            if (doEQ)
            {
                // Left
                float xL = data[i];
                float yL = bq_a0 * xL + bq_a1 * bq_x1L + bq_a2 * bq_x2L
                                       - bq_b1 * bq_y1L - bq_b2 * bq_y2L;
                bq_x2L = bq_x1L; bq_x1L = xL;
                bq_y2L = bq_y1L; bq_y1L = yL;
                data[i] = yL;

                // Right
                if (channels >= 2)
                {
                    float xR = data[i + 1];
                    float yR = bq_a0 * xR + bq_a1 * bq_x1R + bq_a2 * bq_x2R
                                           - bq_b1 * bq_y1R - bq_b2 * bq_y2R;
                    bq_x2R = bq_x1R; bq_x1R = xR;
                    bq_y2R = bq_y1R; bq_y1R = yR;
                    data[i + 1] = yR;
                }
            }

            // ── Compressor ──
            if (doComp)
            {
                float peak = Mathf.Abs(data[i]);
                if (channels >= 2) peak = Mathf.Max(peak, Mathf.Abs(data[i + 1]));

                // Envelope follower
                float coeff = peak > compEnvelope ? attackCoeff : releaseCoeff;
                compEnvelope = coeff * compEnvelope + (1f - coeff) * peak;

                // Gain reduction
                float gain = 1f;
                if (compEnvelope > threshLin && compEnvelope > 0.0001f)
                    gain = threshLin / compEnvelope;
                gain *= makeupLin;

                for (int c = 0; c < channels; c++)
                    data[i + c] *= gain;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  EDITOR
    // ══════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnValidate()
    {
        var src = GetComponent<AudioSource>();
        if (src == null) return;
        src.spatialBlend = spatialBlend;
        src.minDistance  = minDistance;
        src.maxDistance  = maxDistance;
        src.volume       = volume;
        bqDirty = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, minDistance);
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.12f);
        Gizmos.DrawWireSphere(transform.position, maxDistance);

        if (listener != null)
        {
            Gizmos.color = Color.Lerp(Color.green, Color.red, currentOcclusion);
            Gizmos.DrawLine(transform.position, listener.position);
        }
    }
#endif
}