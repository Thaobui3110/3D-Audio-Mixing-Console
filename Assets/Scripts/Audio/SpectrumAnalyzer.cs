using UnityEngine;

/// <summary>
/// Lấy spectrum và waveform data từ Unity AudioListener.
/// Thay thế ChunityAudioInput + ChunityFFT — không cần Chunity.
///
/// HIERARCHY:
///   Systems → AudioManager → SpectrumAnalyzer (component này)
///
/// Truy cập:
///   SpectrumAnalyzer.Instance.SpectrumData   — float[spectrumSize]
///   SpectrumAnalyzer.Instance.WaveformData   — float[waveformSize]
///   SpectrumAnalyzer.Instance.RMS            — amplitude tức thì (0-1)
///   SpectrumAnalyzer.Instance.Bass           — năng lượng bass (0-1)
/// </summary>
[DisallowMultipleComponent]
public class SpectrumAnalyzer : MonoBehaviour
{
    public static SpectrumAnalyzer Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("FFT Settings")]
    [SerializeField] private FFTWindow fftWindow    = FFTWindow.BlackmanHarris;
    [SerializeField] private int       spectrumSize = 512;
    [SerializeField] private int       waveformSize = 1024;
    [SerializeField] private int       audioChannel = 0;

    [Header("Smoothing")]
    [SerializeField, Range(0f, 1f)] private float spectrumSmooth = 0.85f;

    // ── Public data ────────────────────────────────────────────────────────
    public float[] SpectrumData { get; private set; }
    public float[] WaveformData { get; private set; }

    /// <summary>RMS amplitude tức thì (0–1). Dùng cho AudioReactiveObject.</summary>
    public float RMS  { get; private set; }
    /// <summary>Năng lượng bass (bins 0–10%). Dùng cho scale animation.</summary>
    public float Bass { get; private set; }

    // ── Private ────────────────────────────────────────────────────────────
    private float[] rawSpectrum;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        // Snap sizes đến power-of-2
        spectrumSize = Mathf.NextPowerOfTwo(spectrumSize);
        waveformSize = Mathf.NextPowerOfTwo(waveformSize);

        SpectrumData = new float[spectrumSize];
        WaveformData = new float[waveformSize];
        rawSpectrum  = new float[spectrumSize];
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Spectrum (FFT)
        AudioListener.GetSpectrumData(rawSpectrum, audioChannel, fftWindow);
        for (int i = 0; i < spectrumSize; i++)
            SpectrumData[i] = Mathf.Lerp(SpectrumData[i], rawSpectrum[i], 1f - spectrumSmooth);

        // Waveform
        AudioListener.GetOutputData(WaveformData, audioChannel);

        // Derived values
        ComputeRMS();
        ComputeBass();
    }

    // ── Private ────────────────────────────────────────────────────────────
    private void ComputeRMS()
    {
        float sum = 0f;
        for (int i = 0; i < waveformSize; i++)
            sum += WaveformData[i] * WaveformData[i];
        RMS = Mathf.Sqrt(sum / waveformSize);
    }

    private void ComputeBass()
    {
        int   bassBins = Mathf.Max(1, spectrumSize / 10);
        float sum      = 0f;
        for (int i = 0; i < bassBins; i++)
            sum += SpectrumData[i];
        Bass = sum / bassBins;
    }
}
