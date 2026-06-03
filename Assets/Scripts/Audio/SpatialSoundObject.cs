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
    // BUG FIX: default -1000 → 0f agar slider 0% = fully dry
    [SerializeField, Range(-10000f, 0f)] private float reverbRoom      = 0f;
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
    private Transform           listenerRoot;
    private float               currentOcclusion;

    // ── Biquad (Mid EQ) — thread-safe double buffer ───────────────────────
    // Main thread ghi vào _pending*, audio thread đọc từ _live*.
    // _bqPending flag báo audio thread swap vào khi bắt đầu buffer mới.
    private float _pa0, _pa1, _pa2, _pb1, _pb2;    // pending (main thread writes)
    private float _la0, _la1, _la2, _lb1, _lb2;    // live    (audio thread reads)
    private volatile bool _bqPending = false;

    private float bq_x1L, bq_x2L, bq_y1L, bq_y2L;
    private float bq_x1R, bq_x2R, bq_y1R, bq_y2R;

    // Compressor envelope
    private float compEnvelope;

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
        reverbFilter = GetOrAddReverbFilter();

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
            listenerRoot = listener.root;
        }
    }

    private void Update()
    {
        if (useOcclusion) UpdateOcclusion();
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
        // Tính coefficients trên main thread, audio thread sẽ swap vào
        RecalcBiquad();
    }

    // ── Per-Object Reverb ─────────────────────────────────────────────────

    public void SetReverbRoom(float room)
    {
        reverbRoom = Mathf.Clamp(room, -10000f, 0f);
        if (reverbFilter != null)
        {
            // Preset phải là User — chỉ khi đó Unity mới cho phép set room
            EnsureReverbPresetUser();
            reverbFilter.room = reverbRoom;
        }
    }

    public void SetReverbDecay(float decay)
    {
        reverbDecayTime = Mathf.Clamp(decay, 0.1f, 20f);
        if (reverbFilter != null)
        {
            EnsureReverbPresetUser();
            reverbFilter.decayTime = reverbDecayTime;
        }
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

    private AudioReverbFilter GetOrAddReverbFilter()
    {
        var rf = GetComponent<AudioReverbFilter>();
        if (rf == null) rf = gameObject.AddComponent<AudioReverbFilter>();
        return rf;
    }

    private void EnsureReverbPresetUser()
    {
        if (reverbFilter != null && reverbFilter.reverbPreset != AudioReverbPreset.User)
            reverbFilter.reverbPreset = AudioReverbPreset.User;
    }

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
        col.isTrigger = false;
        col.radius    = 0.6f;
    }

    private void ApplyAllDSP()
    {
        // LP / HP filters
        if (lpFilter != null) lpFilter.cutoffFrequency = eqLowPassCutoff;
        if (hpFilter != null)
        {
            hpFilter.enabled         = true;
            hpFilter.cutoffFrequency = eqHighPassCutoff;
        }

        // BUG FIX: dùng AudioReverbPreset.User thay vì Off
        // Preset.Off lock tất cả parameters — Unity sẽ ignore mọi set sau đó.
        // Chỉ Preset.User mới cho phép thay đổi room, decayTime, v.v. tự do.
        if (reverbFilter != null)
        {
            reverbFilter.reverbPreset = AudioReverbPreset.User;
            reverbFilter.room         = reverbRoom;        // -10000..0  (fully dry = 0)
            reverbFilter.decayTime    = reverbDecayTime;
            reverbFilter.dryLevel     = 0f;               // 0 dB dry path luôn rõ
            reverbFilter.reverbLevel  = -10000f;          // bắt đầu fully dry
        }

        // Mid EQ — tính coefficients ban đầu
        RecalcBiquad();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PRIVATE — Occlusion
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
                var hits = Physics.RaycastAll(
                    transform.position, dir.normalized, dist,
                    occlusionMask, QueryTriggerInteraction.Ignore);

                foreach (var hit in hits)
                {
                    if (listenerRoot != null && hit.transform.IsChildOf(listenerRoot)) continue;
                    if (hit.transform == transform) continue;
                    if (hit.collider.GetComponent<SpatialSoundObject>() != null) continue;
                    target = 1f;
                    break;
                }
            }
        }

        currentOcclusion = Mathf.Lerp(currentOcclusion, target, Time.deltaTime * occSmoothing);

        if (lpFilter != null)
        {
            float occCutoff = Mathf.Lerp(openCutoff, blockedCutoff, currentOcclusion);
            lpFilter.cutoffFrequency = Mathf.Min(eqLowPassCutoff, occCutoff);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PRIVATE — Mid EQ Biquad
    // ══════════════════════════════════════════════════════════════════════

    // Gọi từ main thread: tính hệ số mới vào _pending*, set flag cho audio thread
    private void RecalcBiquad()
    {
        float gainAbs = Mathf.Pow(10f, eqMidGain / 40f);
        float w0  = 2f * Mathf.PI * eqMidFreq / sampleRate;
        float sinW = Mathf.Sin(w0);
        float cosW = Mathf.Cos(w0);
        float alpha = sinW / (2f * eqMidQ);

        float a0_inv;
        if (eqMidGain >= 0f)
        {
            float norm = 1f + alpha / gainAbs;
            a0_inv = 1f / norm;
            _pa0 = (1f + alpha * gainAbs) * a0_inv;
            _pa1 = (-2f * cosW)           * a0_inv;
            _pa2 = (1f - alpha * gainAbs) * a0_inv;
            _pb1 = _pa1;
            _pb2 = (1f - alpha / gainAbs) * a0_inv;
        }
        else
        {
            float norm = 1f + alpha * gainAbs;
            a0_inv = 1f / norm;
            _pa0 = (1f + alpha / gainAbs) * a0_inv;
            _pa1 = (-2f * cosW)            * a0_inv;
            _pa2 = (1f - alpha / gainAbs)  * a0_inv;
            _pb1 = _pa1;
            _pb2 = (1f - alpha * gainAbs)  * a0_inv;
        }

        // Signal audio thread: new coefficients ready
        _bqPending = true;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  OnAudioFilterRead — Mid EQ + Compressor (Audio Thread)
    // ══════════════════════════════════════════════════════════════════════

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (channels < 1) return;

        // Swap pending coefficients — đầu mỗi buffer để tránh mid-buffer glitch
        if (_bqPending)
        {
            _la0 = _pa0; _la1 = _pa1; _la2 = _pa2;
            _lb1 = _pb1; _lb2 = _pb2;
            _bqPending = false;
        }

        bool doEQ   = Mathf.Abs(eqMidGain) > 0.1f;
        bool doComp = compThreshold > -59.9f;
        if (!doEQ && !doComp) return;

        float attackCoeff  = Mathf.Exp(-1f / (compAttack  * sampleRate));
        float releaseCoeff = Mathf.Exp(-1f / (compRelease * sampleRate));
        float threshLin    = Mathf.Pow(10f, compThreshold / 20f);
        float makeupLin    = Mathf.Pow(10f, compMakeupGain / 20f);

        for (int i = 0; i < data.Length; i += channels)
        {
            // ── Mid EQ (biquad) ──
            if (doEQ)
            {
                float xL = data[i];
                float yL = _la0 * xL + _la1 * bq_x1L + _la2 * bq_x2L
                                     - _lb1 * bq_y1L  - _lb2 * bq_y2L;
                bq_x2L = bq_x1L; bq_x1L = xL;
                bq_y2L = bq_y1L; bq_y1L = yL;
                data[i] = yL;

                if (channels >= 2)
                {
                    float xR = data[i + 1];
                    float yR = _la0 * xR + _la1 * bq_x1R + _la2 * bq_x2R
                                         - _lb1 * bq_y1R  - _lb2 * bq_y2R;
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

                float coeff = peak > compEnvelope ? attackCoeff : releaseCoeff;
                compEnvelope = coeff * compEnvelope + (1f - coeff) * peak;

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
        // Validate reverb preset nếu component tồn tại
        var rf = GetComponent<AudioReverbFilter>();
        if (rf != null && rf.reverbPreset != AudioReverbPreset.User)
            rf.reverbPreset = AudioReverbPreset.User;
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