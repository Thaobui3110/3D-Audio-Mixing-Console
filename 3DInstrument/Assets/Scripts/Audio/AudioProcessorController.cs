using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Điều khiển tất cả DSP parameters trên AudioMixer.
///
/// HIERARCHY:
///   Systems → AudioManager → AudioProcessorController (component này)
///
/// ── AudioMixer Setup (Music group, theo thứ tự từ trên xuống) ──
///
///   Effect              │ Expose parameter     │ Tên expose
///   ────────────────────┼──────────────────────┼─────────────────
///   Highpass            │ Cutoff freq          │ HighPassCutoff
///   Lowpass             │ Cutoff freq          │ LowPassCutoff
///   ParamEQ (Mid)       │ Gain                 │ MidEQGain
///   SFX Reverb          │ Room (wet)           │ ReverbWet
///                       │ Decay Time           │ ReverbDecayTime
///   Compressor          │ Threshold            │ CompThreshold
///                       │ Ratio                │ CompRatio
///   Attenuation (Master)│ Volume               │ MasterVolume
///
/// ParamEQ (Mid): đặt Center Freq ~1000 Hz, Bandwidth ~1.0 trong Inspector.
/// Chỉ expose Gain — đây là dải mid boost/cut duy nhất.
/// </summary>
[DisallowMultipleComponent]
public class AudioProcessorController : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Mixer")]
    [SerializeField] private AudioMixer mixer;

    [Header("Exposed Parameter Names — phải khớp tên trong AudioMixer")]
    [SerializeField] private string pMasterVolume  = "MasterVolume";
    [SerializeField] private string pLowPassCutoff = "LowPassCutoff";
    [SerializeField] private string pHighPassCutoff= "HighPassCutoff";
    [SerializeField] private string pMidEQGain     = "MidEQGain";
    [SerializeField] private string pReverbWet     = "ReverbWet";
    [SerializeField] private string pReverbDecay   = "ReverbDecayTime";
    [SerializeField] private string pCompThreshold = "CompThreshold";
    [SerializeField] private string pCompMakeupGain = "CompMakeupGain";

    // ── Cached defaults ────────────────────────────────────────────────────
    private float _masterVol   = 1f;
    private float _lowPass     = 22000f;
    private float _highPass    = 20f;
    private float _midEQGain   = 0f;
    private float _reverbWet   = 0f;
    private float _reverbDecay = 1f;
    private float _compThresh  = 0f;
    private float _compMakeupGain = 0f;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        if (mixer == null)
            Debug.LogError("[AudioProcessorController] mixer chưa gán!", this);
    }

    private void Start() => ApplyAllDefaults();

    // ── Public API ─────────────────────────────────────────────────────────

    /// <param name="linear">0–1</param>
    public void SetMasterVolume(float linear)
    {
        _masterVol = Mathf.Clamp01(linear);
        Set(pMasterVolume, ToDecibel(_masterVol));
    }

    /// <param name="hz">200–22000 Hz — cắt tần số cao hơn giá trị này</param>
    public void SetLowPassCutoff(float hz)
    {
        _lowPass = Mathf.Clamp(hz, 200f, 22000f);
        Set(pLowPassCutoff, _lowPass);
    }

    /// <param name="hz">20–2000 Hz — cắt tần số thấp hơn giá trị này</param>
    public void SetHighPassCutoff(float hz)
    {
        _highPass = Mathf.Clamp(hz, 20f, 2000f);
        Set(pHighPassCutoff, _highPass);
    }

    /// <param name="dB">-12 đến +12 dB — boost/cut dải mid (~1 kHz)</param>
    public void SetMidEQGain(float dB)
    {
        _midEQGain = Mathf.Clamp(dB, -12f, 12f);
        Set(pMidEQGain, _midEQGain);
    }

    /// <param name="linear">0–1</param>
    public void SetReverbWet(float linear)
    {
        _reverbWet = Mathf.Clamp01(linear);
        Set(pReverbWet, ToDecibel(_reverbWet));
    }

    /// <param name="seconds">0.1–10 s</param>
    public void SetReverbDecay(float seconds)
    {
        _reverbDecay = Mathf.Clamp(seconds, 0.1f, 10f);
        Set(pReverbDecay, _reverbDecay);
    }

    /// <param name="dB">-60–0 dB</param>
    public void SetCompressorThreshold(float dB)
    {
        _compThresh = Mathf.Clamp(dB, -60f, 0f);
        Set(pCompThreshold, _compThresh);
    }

    /// <param name="gain">0 dB</param>
    public void SetCompressorMakeupGain(float gain)
    {
        _compMakeupGain = Mathf.Clamp(gain, 0f, 20f);
        Set(pCompMakeupGain, _compMakeupGain);
    }

    // ── Getters ────────────────────────────────────────────────────────────
    public float GetMasterVolume()        => _masterVol;
    public float GetLowPassCutoff()       => _lowPass;
    public float GetHighPassCutoff()      => _highPass;
    public float GetMidEQGain()           => _midEQGain;
    public float GetReverbWet()           => _reverbWet;
    public float GetReverbDecay()         => _reverbDecay;
    public float GetCompressorThreshold() => _compThresh;
    public float GetCompressorMakeupGain()     => _compMakeupGain;

    // ── Private ────────────────────────────────────────────────────────────
    private void Set(string param, float value)
    {
        if (mixer == null || string.IsNullOrEmpty(param)) return;
        if (!mixer.SetFloat(param, value))
            Debug.LogWarning($"[AudioProcessorController] '{param}' không tìm thấy — kiểm tra tên Exposed parameter trong AudioMixer.", this);
    }

    private static float ToDecibel(float linear)
        => linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;

    private void ApplyAllDefaults()
    {
        SetMasterVolume(_masterVol);
        SetLowPassCutoff(_lowPass);
        SetHighPassCutoff(_highPass);
        SetMidEQGain(_midEQGain);
        SetReverbWet(_reverbWet);
        SetReverbDecay(_reverbDecay);
        SetCompressorThreshold(_compThresh);
        SetCompressorMakeupGain(_compMakeupGain);
    }

#if UNITY_EDITOR
    [ContextMenu("Apply Defaults to Mixer")]
    private void EditorApplyDefaults() => ApplyAllDefaults();
#endif
}
