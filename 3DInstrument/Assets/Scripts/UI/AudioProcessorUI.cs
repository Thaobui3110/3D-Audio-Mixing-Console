using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AudioProcessorUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────
    // Inspector fields
    // ─────────────────────────────────────────────────────────────────────

    [Header("Core Reference")]
    [SerializeField] private AudioProcessorController processor;

    [Header("UI Slider Components")]
    [SerializeField] private Slider sliderLowPass;
    [SerializeField] private Slider sliderLowShelf;
    [SerializeField] private Slider sliderMidPeak;
    [SerializeField] private Slider sliderHighShelf;
    [SerializeField] private Slider sliderReverbWet;
    [SerializeField] private Slider sliderReverbDecay;
    [SerializeField] private Slider sliderCompThreshold;
    [SerializeField] private Slider sliderCompRatio;
    [SerializeField] private Slider sliderMasterVolume;

    // FIX 4: Thêm label riêng cho từng slider để hiển thị giá trị hiện tại.
    // Kéo một TMP_Text vào mỗi field này trong Inspector (đặt cạnh slider tương ứng).
    [Header("Value Labels — kéo TMP_Text cạnh mỗi slider vào đây")]
    [SerializeField] private TMP_Text labelLowPass;
    [SerializeField] private TMP_Text labelLowShelf;
    [SerializeField] private TMP_Text labelMidPeak;
    [SerializeField] private TMP_Text labelHighShelf;
    [SerializeField] private TMP_Text labelReverbWet;
    [SerializeField] private TMP_Text labelReverbDecay;
    [SerializeField] private TMP_Text labelCompThreshold;
    [SerializeField] private TMP_Text labelCompRatio;
    [SerializeField] private TMP_Text labelMasterVolume;

    [Header("Status Bar (dùng chung — hiển thị thay đổi gần nhất)")]
    [SerializeField] private TMP_Text statusLabel;

    // ─────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (processor == null)
            processor = FindObjectOfType<AudioProcessorController>();

        InitializeSliders();
    }

    private void InitializeSliders()
    {
        // FIX 5: Set min/max/value trước khi gắn listener.
        // Trước đây bỏ qua hoàn toàn nên tất cả slider mặc định 0–1 bắt đầu ở 0,
        // khiến Low Pass = 0 Hz (im lặng), Master Volume = 0%, Reverb = -80 dB ngay lúc khởi động.

        // Low Pass Cutoff: tai người nghe được 20Hz–20kHz, mở hoàn toàn = 22000Hz
        ConfigSlider(sliderLowPass,       minValue: 200f,  maxValue: 22000f, defaultValue: 22000f);

        // EQ Gains: -12dB đến +12dB, mặc định 0 = không thay đổi âm sắc
        ConfigSlider(sliderLowShelf,      minValue: -12f,  maxValue: 12f,    defaultValue: 0f);
        ConfigSlider(sliderMidPeak,       minValue: -12f,  maxValue: 12f,    defaultValue: 0f);
        ConfigSlider(sliderHighShelf,     minValue: -12f,  maxValue: 12f,    defaultValue: 0f);

        // Reverb Wet: 0 = khô hoàn toàn, 1 = 100% reverb
        ConfigSlider(sliderReverbWet,     minValue: 0f,    maxValue: 1f,     defaultValue: 0f);

        // Reverb Decay: 0.1s rất ngắn (phòng nhỏ), 10s rất dài (hang động), mặc định 1s
        ConfigSlider(sliderReverbDecay,   minValue: 0.1f,  maxValue: 10f,    defaultValue: 1f);

        // Compressor Threshold: -60dB đến 0dB, mặc định 0 = không kích hoạt compressor
        ConfigSlider(sliderCompThreshold, minValue: -60f,  maxValue: 0f,     defaultValue: 0f);

        // Compressor Ratio: 1:1 = không nén, 20:1 = nén cứng
        ConfigSlider(sliderCompRatio,     minValue: 1f,    maxValue: 20f,    defaultValue: 1f);

        // Master Volume: 0 = câm, 1 = 100%
        ConfigSlider(sliderMasterVolume,  minValue: 0f,    maxValue: 1f,     defaultValue: 1f);

        // Gắn listeners sau khi đã set giá trị để tránh callback bắn ngay lúc set
        if (sliderLowPass      != null) sliderLowPass.onValueChanged.AddListener(OnLowPassChanged);
        if (sliderLowShelf     != null) sliderLowShelf.onValueChanged.AddListener(OnLowShelfChanged);
        if (sliderMidPeak      != null) sliderMidPeak.onValueChanged.AddListener(OnMidPeakChanged);
        if (sliderHighShelf    != null) sliderHighShelf.onValueChanged.AddListener(OnHighShelfChanged);
        if (sliderReverbWet    != null) sliderReverbWet.onValueChanged.AddListener(OnReverbWetChanged);
        if (sliderReverbDecay  != null) sliderReverbDecay.onValueChanged.AddListener(OnReverbDecayChanged);
        if (sliderCompThreshold != null) sliderCompThreshold.onValueChanged.AddListener(OnCompThresholdChanged);
        if (sliderCompRatio    != null) sliderCompRatio.onValueChanged.AddListener(OnCompRatioChanged);
        if (sliderMasterVolume != null) sliderMasterVolume.onValueChanged.AddListener(OnMasterVolumeChanged);

        // Cập nhật label với giá trị mặc định ngay khi khởi động
        SetLabel(labelLowPass,        "22000 Hz");
        SetLabel(labelLowShelf,       "0.0 dB");
        SetLabel(labelMidPeak,        "0.0 dB");
        SetLabel(labelHighShelf,      "0.0 dB");
        SetLabel(labelReverbWet,      "0%");
        SetLabel(labelReverbDecay,    "1.00 s");
        SetLabel(labelCompThreshold,  "0.0 dB");
        SetLabel(labelCompRatio,      "1.0:1");
        SetLabel(labelMasterVolume,   "100%");

        UpdateStatusText("Console initialized. Ready.");
    }

    // Helper: set range và giá trị mặc định cho một slider
    private void ConfigSlider(Slider slider, float minValue, float maxValue, float defaultValue)
    {
        if (slider == null) return;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value    = defaultValue;
    }

    // Helper: set text an toàn
    private void SetLabel(TMP_Text label, string text)
    {
        if (label != null) label.text = text;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Callbacks
    // ─────────────────────────────────────────────────────────────────────

    public void OnLowPassChanged(float value)
    {
        if (processor != null) processor.SetLowPassCutoff(value);
        SetLabel(labelLowPass, $"{value:F0} Hz");
        UpdateStatusText($"Low Pass Cutoff: {value:F0} Hz");
    }

    public void OnLowShelfChanged(float value)
    {
        if (processor != null) processor.SetLowShelfGain(value);
        string sign = value >= 0f ? "+" : "";
        SetLabel(labelLowShelf, $"{sign}{value:F1} dB");
        UpdateStatusText($"Low Shelf Gain: {sign}{value:F1} dB");
    }

    public void OnMidPeakChanged(float value)
    {
        if (processor != null) processor.SetMidPeakGain(value);
        string sign = value >= 0f ? "+" : "";
        SetLabel(labelMidPeak, $"{sign}{value:F1} dB");
        UpdateStatusText($"Mid Peak Gain: {sign}{value:F1} dB");
    }

    public void OnHighShelfChanged(float value)
    {
        if (processor != null) processor.SetHighShelfGain(value);
        string sign = value >= 0f ? "+" : "";
        SetLabel(labelHighShelf, $"{sign}{value:F1} dB");
        UpdateStatusText($"High Shelf Gain: {sign}{value:F1} dB");
    }

    public void OnReverbWetChanged(float value)
    {
        if (processor != null) processor.SetReverbWet(value);
        int percent = Mathf.RoundToInt(value * 100f);
        SetLabel(labelReverbWet, $"{percent}%");
        UpdateStatusText($"Reverb Wet Level: {percent}%");
    }

    public void OnReverbDecayChanged(float value)
    {
        if (processor != null) processor.SetReverbDecay(value);
        SetLabel(labelReverbDecay, $"{value:F2} s");
        UpdateStatusText($"Reverb Decay Time: {value:F2} s");
    }

    public void OnCompThresholdChanged(float value)
    {
        if (processor != null) processor.SetCompressorThreshold(value);
        SetLabel(labelCompThreshold, $"{value:F1} dB");
        UpdateStatusText($"Compressor Threshold: {value:F1} dB");
    }

    public void OnCompRatioChanged(float value)
    {
        if (processor != null) processor.SetCompressorRatio(value);
        SetLabel(labelCompRatio, $"{value:F1}:1");
        UpdateStatusText($"Compressor Ratio: {value:F1}:1");
    }

    public void OnMasterVolumeChanged(float value)
    {
        if (processor != null) processor.SetMasterVolume(value);
        int percent = Mathf.RoundToInt(value * 100f);
        SetLabel(labelMasterVolume, $"{percent}%");
        UpdateStatusText($"Master Volume: {percent}%");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private void UpdateStatusText(string text)
    {
        if (statusLabel != null)
            statusLabel.text = text;
    }

    private void OnDestroy()
    {
        if (sliderLowPass      != null) sliderLowPass.onValueChanged.RemoveListener(OnLowPassChanged);
        if (sliderLowShelf     != null) sliderLowShelf.onValueChanged.RemoveListener(OnLowShelfChanged);
        if (sliderMidPeak      != null) sliderMidPeak.onValueChanged.RemoveListener(OnMidPeakChanged);
        if (sliderHighShelf    != null) sliderHighShelf.onValueChanged.RemoveListener(OnHighShelfChanged);
        if (sliderReverbWet    != null) sliderReverbWet.onValueChanged.RemoveListener(OnReverbWetChanged);
        if (sliderReverbDecay  != null) sliderReverbDecay.onValueChanged.RemoveListener(OnReverbDecayChanged);
        if (sliderCompThreshold != null) sliderCompThreshold.onValueChanged.RemoveListener(OnCompThresholdChanged);
        if (sliderCompRatio    != null) sliderCompRatio.onValueChanged.RemoveListener(OnCompRatioChanged);
        if (sliderMasterVolume != null) sliderMasterVolume.onValueChanged.RemoveListener(OnMasterVolumeChanged);
    }
}