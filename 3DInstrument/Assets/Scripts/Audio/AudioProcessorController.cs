using UnityEngine;
using UnityEngine.Audio;

public class AudioProcessorController : MonoBehaviour
{
    [Header("Audio Mixer Assignment")]
    [SerializeField] private AudioMixer pianoMixer;

    [Header("Exposed Parameter Names — phải khớp chính xác tên trong AudioMixer window")]
    [SerializeField] private string masterVolumeParam  = "MasterVolume";
    [SerializeField] private string lowPassCutoffParam = "LowPassCutoff";
    [SerializeField] private string reverbWetParam     = "ReverbWet";
    [SerializeField] private string reverbDecayParam   = "ReverbDecayTime";
    [SerializeField] private string compThresholdParam = "CompThreshold";
    [SerializeField] private string compRatioParam     = "CompRatio";

    [Header("EQ Band Params (để trống nếu dùng ChucK)")]
    [SerializeField] private string lowShelfGainParam  = "";
    [SerializeField] private string midPeakGainParam   = "";
    [SerializeField] private string highShelfGainParam = "";

    // FIX: Chuck.Main không tồn tại trong Chunity API.
    // Đúng cách là kéo ChuckSubInstance (hoặc ChuckMainInstance) từ scene vào đây.
    // ChuckSubInstance là component gắn trên GameObject có PianoSynth.ck chạy.
    // ChuckMainInstance dùng khi chỉ có một instance ChucK duy nhất trong scene.
    [Header("ChucK Instance — kéo GameObject có ChuckSubInstance vào đây")]
    [SerializeField] private ChuckSubInstance chuckInstance;

    private void Awake()
    {
        if (pianoMixer == null)
            Debug.LogError($"[{gameObject.name}] Piano Mixer chưa được gán trong Inspector!");

        if (chuckInstance == null)
            Debug.LogWarning($"[{gameObject.name}] ChucK Instance chưa được gán — 3 band EQ sẽ không hoạt động.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Master Volume
    // ─────────────────────────────────────────────────────────────────────

    public void SetMasterVolume(float linearValue)
    {
        float dB = linearValue > 0.0001f ? Mathf.Log10(linearValue) * 20f : -80f;
        SetMixerParam(masterVolumeParam, dB);
    }

    // ─────────────────────────────────────────────────────────────────────
    // EQ
    // ─────────────────────────────────────────────────────────────────────

    public void SetLowPassCutoff(float frequency)
    {
        SetMixerParam(lowPassCutoffParam, frequency);
    }

    public void SetLowShelfGain(float gain)
    {
        if (!string.IsNullOrEmpty(lowShelfGainParam))
            SetMixerParam(lowShelfGainParam, gain);

        // Gửi sang ChucK — khớp với "global float unity_LowShelfGain" trong PianoSynth.ck.txt
        SetChuckFloat("unity_LowShelfGain", gain);
    }

    public void SetMidPeakGain(float gain)
    {
        if (!string.IsNullOrEmpty(midPeakGainParam))
            SetMixerParam(midPeakGainParam, gain);

        SetChuckFloat("unity_MidPeakGain", gain);
    }

    public void SetLowPass(float value)
    {
        Debug.Log("Slider: " + value);

        bool ok =
            pianoMixer.SetFloat("LowPassCutoff", value);

        Debug.Log("SetFloat: " + ok);
    }

    public void SetHighShelfGain(float gain)
    {
        if (!string.IsNullOrEmpty(highShelfGainParam))
            SetMixerParam(highShelfGainParam, gain);

        SetChuckFloat("unity_HighShelfGain", gain);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Reverb
    // ─────────────────────────────────────────────────────────────────────

    public void SetReverbWet(float linearValue)
    {
        float dB = linearValue > 0.0001f ? Mathf.Log10(linearValue) * 20f : -80f;
        SetMixerParam(reverbWetParam, dB);
    }

    public void SetReverbDecay(float decayTimeSeconds)
    {
        SetMixerParam(reverbDecayParam, decayTimeSeconds);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Compressor
    // ─────────────────────────────────────────────────────────────────────

    public void SetCompressorThreshold(float dB)
    {
        SetMixerParam(compThresholdParam, dB);
    }

    public void SetCompressorRatio(float ratio)
    {
        SetMixerParam(compRatioParam, ratio);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ─────────────────────────────────────────────────────────────────────

    private void SetMixerParam(string paramName, float value)
    {
        if (pianoMixer == null) return;

        bool success = pianoMixer.SetFloat(paramName, value);
        if (!success)
        {
            Debug.LogWarning(
                $"[AudioProcessorController] Không tìm thấy exposed parameter '{paramName}' " +
                $"trong AudioMixer '{pianoMixer.name}'. " +
                "Kiểm tra lại tên đã Expose trong cửa sổ AudioMixer (chuột phải vào param → Expose)."
            );
        }
    }

    private void SetChuckFloat(string variableName, float value)
    {
        if (chuckInstance == null) return;
        chuckInstance.SetFloat(variableName, value);
    }
}