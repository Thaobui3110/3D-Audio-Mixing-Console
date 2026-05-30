using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI chính của Audio Processor Sandbox.
/// Quản lý: upload file, play/stop, source dropdown, tất cả sliders DSP.
///
/// EQ section (Lựa chọn 2):
///   - Low Pass Cutoff  (Lowpass effect  → LowPassCutoff)
///   - High Pass Cutoff (Highpass effect → HighPassCutoff)
///   - Mid EQ Gain      (ParamEQ effect  → MidEQGain, ~1kHz)
/// </summary>
[DisallowMultipleComponent]
public class AudioProcessorUI : MonoBehaviour
{
    // ── Core refs ──────────────────────────────────────────────────────────
    [Header("Controllers")]
    [SerializeField] private AudioProcessorController processor;
    [SerializeField] private SpatialAudioManager      audioManager;
    [SerializeField] private AudioFileLoader          fileLoader;

    // ── Upload ──────────────────────────────────────────────────────────────
    [Header("Upload")]
    [SerializeField] private TMP_InputField pathInputField;
    [SerializeField] private Button         loadButton;
    [SerializeField] private Slider         loadingBar;
    [SerializeField] private TMP_Text       loadingLabel;

    // ── Transport ──────────────────────────────────────────────────────────
    [Header("Transport")]
    [SerializeField] private Button      playButton;
    [SerializeField] private Button      stopButton;
    [SerializeField] private Button      spawnButton;
    [SerializeField] private TMP_Dropdown sourceDropdown;

    // ── EQ (Lựa chọn 2: LowPass + HighPass + MidEQ) ───────────────────────
    [Header("EQ")]
    [SerializeField] private Slider   sliderLowPass;
    [SerializeField] private TMP_Text labelLowPass;
    [SerializeField] private Slider   sliderHighPass;
    [SerializeField] private TMP_Text labelHighPass;
    [SerializeField] private Slider   sliderMidEQ;
    [SerializeField] private TMP_Text labelMidEQ;

    // ── Reverb ─────────────────────────────────────────────────────────────
    [Header("Reverb")]
    [SerializeField] private Slider   sliderReverbWet;
    [SerializeField] private TMP_Text labelReverbWet;
    [SerializeField] private Slider   sliderReverbDecay;
    [SerializeField] private TMP_Text labelReverbDecay;

    // ── Compressor ─────────────────────────────────────────────────────────
    [Header("Compressor")]
    [SerializeField] private Slider   sliderCompThreshold;
    [SerializeField] private TMP_Text labelCompThreshold;
    [SerializeField] private Slider   sliderCompMakeupGain;
    [SerializeField] private TMP_Text labelCompMakeupGain;

    // ── Master ─────────────────────────────────────────────────────────────
    [Header("Master")]
    [SerializeField] private Slider   sliderMasterVolume;
    [SerializeField] private TMP_Text labelMasterVolume;

    // ── Status ─────────────────────────────────────────────────────────────
    [Header("Status")]
    [SerializeField] private TMP_Text statusLabel;

    // ── Private ────────────────────────────────────────────────────────────
    private string selectedSourceID;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        if (processor    == null) processor    = FindObjectOfType<AudioProcessorController>();
        if (audioManager == null) audioManager = FindObjectOfType<SpatialAudioManager>();
        if (fileLoader   == null) fileLoader   = FindObjectOfType<AudioFileLoader>();

        if (processor    == null) Debug.LogError("[AudioProcessorUI] AudioProcessorController không tìm thấy!", this);
        if (audioManager == null) Debug.LogError("[AudioProcessorUI] SpatialAudioManager không tìm thấy!", this);
    }

    private void Start()
    {
        InitSliders();
        InitButtons();
        InitDropdown();
        HookFileLoader();
        HookAudioManager();

        if (loadingBar != null) loadingBar.gameObject.SetActive(false);
        SetStatus("Ready — Upload audio hoặc chọn speaker.");
    }

    private void OnDestroy()
    {
        UnhookFileLoader();
        UnhookAudioManager();
        RemoveSliderListeners();
    }

    // ── Init ───────────────────────────────────────────────────────────────
    private void InitSliders()
    {
        // EQ: LowPass + HighPass + MidEQ
        ConfigSlider(sliderLowPass,       200f,  22000f, processor?.GetLowPassCutoff()        ?? 22000f);
        ConfigSlider(sliderHighPass,       20f,   2000f, processor?.GetHighPassCutoff()        ?? 20f);
        ConfigSlider(sliderMidEQ,         -12f,    12f,  processor?.GetMidEQGain()             ?? 0f);

        // Reverb
        ConfigSlider(sliderReverbWet,      0f,     1f,   processor?.GetReverbWet()             ?? 0f);
        ConfigSlider(sliderReverbDecay,    0.1f,  10f,   processor?.GetReverbDecay()           ?? 1f);

        // Compressor
        ConfigSlider(sliderCompThreshold, -60f,    0f,   processor?.GetCompressorThreshold()   ?? 0f);
        ConfigSlider(sliderCompMakeupGain, 0f,    20f,   processor?.GetCompressorMakeupGain()  ?? 0f);

        // Master
        ConfigSlider(sliderMasterVolume,   0f,     1f,   processor?.GetMasterVolume()          ?? 1f);

        // Labels
        UpdateAllLabels();

        // Listeners
        AddListener(sliderLowPass,       OnLowPassChanged);
        AddListener(sliderHighPass,      OnHighPassChanged);
        AddListener(sliderMidEQ,         OnMidEQChanged);
        AddListener(sliderReverbWet,     OnReverbWetChanged);
        AddListener(sliderReverbDecay,   OnReverbDecayChanged);
        AddListener(sliderCompThreshold, OnCompThresholdChanged);
        AddListener(sliderCompMakeupGain,OnCompMakeupGainChanged);
        AddListener(sliderMasterVolume,  OnMasterVolumeChanged);
    }

    private void InitButtons()
    {
        if (loadButton  != null) loadButton.onClick.AddListener(OnLoadClicked);
        if (playButton  != null) playButton.onClick.AddListener(OnPlayClicked);
        if (stopButton  != null) stopButton.onClick.AddListener(OnStopClicked);
        if (spawnButton != null) spawnButton.onClick.AddListener(OnSpawnClicked);
    }

    private void InitDropdown()
    {
        if (sourceDropdown != null)
        {
            sourceDropdown.onValueChanged.AddListener(OnDropdownChanged);
            RefreshDropdown();
        }
    }

    // ── Button callbacks ───────────────────────────────────────────────────
    private void OnLoadClicked()
    {
        if (fileLoader == null) return;
        string path = pathInputField != null ? pathInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(path)) { SetStatus("Nhập đường dẫn file trước khi load."); return; }

        if (!string.IsNullOrEmpty(selectedSourceID))
            audioManager?.LoadAndAssign(path, selectedSourceID);
        else
            audioManager?.LoadAndSpawn(path);

        if (loadingBar   != null) loadingBar.gameObject.SetActive(true);
        if (loadingLabel != null) loadingLabel.text = "Loading...";
        SetStatus($"Đang load: {System.IO.Path.GetFileName(path)}");
    }

    private void OnPlayClicked()
    {
        if (!string.IsNullOrEmpty(selectedSourceID)) audioManager?.Play(selectedSourceID);
        else audioManager?.PlayAll();
        SetStatus("Playing.");
    }

    private void OnStopClicked()
    {
        if (!string.IsNullOrEmpty(selectedSourceID)) audioManager?.Stop(selectedSourceID);
        else audioManager?.StopAll();
        SetStatus("Stopped.");
    }

    private void OnSpawnClicked()
    {
        audioManager?.SpawnSpeaker();
        RefreshDropdown();
        SetStatus("Speaker mới đã spawn trong AudioWorld.");
    }

    private void OnDropdownChanged(int index)
    {
        if (sourceDropdown == null || index < 0) return;
        selectedSourceID = index == 0 ? null : sourceDropdown.options[index].text;
        SetStatus(selectedSourceID != null ? $"Selected: {selectedSourceID}" : "Selected: All Sources");
    }

    // ── Slider callbacks ───────────────────────────────────────────────────

    // EQ
    public void OnLowPassChanged(float v)
    {
        processor?.SetLowPassCutoff(v);
        SetLabel(labelLowPass, FormatHz(v));
        SetStatus($"Low Pass: {FormatHz(v)}");
    }

    public void OnHighPassChanged(float v)
    {
        processor?.SetHighPassCutoff(v);
        SetLabel(labelHighPass, FormatHz(v));
        SetStatus($"High Pass: {FormatHz(v)}");
    }

    public void OnMidEQChanged(float v)
    {
        processor?.SetMidEQGain(v);
        SetLabel(labelMidEQ, FormatDB(v));
        SetStatus($"Mid EQ: {FormatDB(v)}");
    }

    // Reverb
    public void OnReverbWetChanged(float v)
    {
        processor?.SetReverbWet(v);
        SetLabel(labelReverbWet, FormatPercent(v));
        SetStatus($"Reverb Wet: {FormatPercent(v)}");
    }

    public void OnReverbDecayChanged(float v)
    {
        processor?.SetReverbDecay(v);
        SetLabel(labelReverbDecay, FormatSeconds(v));
        SetStatus($"Reverb Decay: {FormatSeconds(v)}");
    }

    // Compressor
    public void OnCompThresholdChanged(float v)
    {
        processor?.SetCompressorThreshold(v);
        SetLabel(labelCompThreshold, FormatDB(v));
        SetStatus($"Comp Threshold: {FormatDB(v)}");
    }

    public void OnCompMakeupGainChanged(float v)
    {
        processor?.SetCompressorMakeupGain(v);
        SetLabel(labelCompMakeupGain, FormatDB(v));
        SetStatus($"Comp Makeup Gain: {FormatDB(v)}");
    }

    // Master
    public void OnMasterVolumeChanged(float v)
    {
        processor?.SetMasterVolume(v);
        SetLabel(labelMasterVolume, FormatPercent(v));
        SetStatus($"Master Volume: {FormatPercent(v)}");
    }

    // ── FileLoader hooks ───────────────────────────────────────────────────
    private void HookFileLoader()
    {
        if (fileLoader == null) return;
        fileLoader.OnClipLoaded   += OnClipLoaded;
        fileLoader.OnLoadError    += OnLoadError;
        fileLoader.OnLoadProgress += OnLoadProgress;
    }

    private void UnhookFileLoader()
    {
        if (fileLoader == null) return;
        fileLoader.OnClipLoaded   -= OnClipLoaded;
        fileLoader.OnLoadError    -= OnLoadError;
        fileLoader.OnLoadProgress -= OnLoadProgress;
    }

    private void OnClipLoaded(AudioClip clip, string name)
    {
        if (loadingBar != null) loadingBar.gameObject.SetActive(false);
        RefreshDropdown();
        SetStatus($"✓ Loaded: {name} ({clip.length:F1}s)");
    }

    private void OnLoadError(string msg)
    {
        if (loadingBar != null) loadingBar.gameObject.SetActive(false);
        SetStatus($"✗ Lỗi: {msg}");
    }

    private void OnLoadProgress(float p)
    {
        if (loadingBar   != null) loadingBar.value = p;
        if (loadingLabel != null) loadingLabel.text = $"Loading {p * 100f:F0}%";
    }

    // ── AudioManager hooks ─────────────────────────────────────────────────
    private void HookAudioManager()
    {
        if (audioManager == null) return;
        audioManager.OnSourceSpawned += _ => RefreshDropdown();
        audioManager.OnSourceRemoved += _ => RefreshDropdown();
    }

    private void UnhookAudioManager()
    {
        if (audioManager == null) return;
        audioManager.OnSourceSpawned -= _ => RefreshDropdown();
        audioManager.OnSourceRemoved -= _ => RefreshDropdown();
    }

    // ── Dropdown ───────────────────────────────────────────────────────────
    private void RefreshDropdown()
    {
        if (sourceDropdown == null || audioManager == null) return;
        var opts = new List<TMP_Dropdown.OptionData> { new("— All Sources —") };
        foreach (var id in audioManager.GetAllIDs())
            opts.Add(new TMP_Dropdown.OptionData(id));
        sourceDropdown.options = opts;
        sourceDropdown.RefreshShownValue();
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private void UpdateAllLabels()
    {
        if (processor == null) return;
        SetLabel(labelLowPass,       FormatHz(processor.GetLowPassCutoff()));
        SetLabel(labelHighPass,      FormatHz(processor.GetHighPassCutoff()));
        SetLabel(labelMidEQ,         FormatDB(processor.GetMidEQGain()));
        SetLabel(labelReverbWet,     FormatPercent(processor.GetReverbWet()));
        SetLabel(labelReverbDecay,   FormatSeconds(processor.GetReverbDecay()));
        SetLabel(labelCompThreshold, FormatDB(processor.GetCompressorThreshold()));
        SetLabel(labelCompMakeupGain,FormatDB(processor.GetCompressorMakeupGain()));
        SetLabel(labelMasterVolume,  FormatPercent(processor.GetMasterVolume()));
    }

    private static void ConfigSlider(Slider s, float min, float max, float val)
    {
        if (s == null) return;
        s.minValue = min; s.maxValue = max; s.value = val;
    }

    private static void AddListener(Slider s, UnityEngine.Events.UnityAction<float> cb)
    { if (s != null) s.onValueChanged.AddListener(cb); }

    private static void RemoveListener(Slider s, UnityEngine.Events.UnityAction<float> cb)
    { if (s != null) s.onValueChanged.RemoveListener(cb); }

    private void RemoveSliderListeners()
    {
        RemoveListener(sliderLowPass,       OnLowPassChanged);
        RemoveListener(sliderHighPass,      OnHighPassChanged);
        RemoveListener(sliderMidEQ,         OnMidEQChanged);
        RemoveListener(sliderReverbWet,     OnReverbWetChanged);
        RemoveListener(sliderReverbDecay,   OnReverbDecayChanged);
        RemoveListener(sliderCompThreshold, OnCompThresholdChanged);
        RemoveListener(sliderCompMakeupGain, OnCompMakeupGainChanged);
        RemoveListener(sliderMasterVolume,  OnMasterVolumeChanged);
    }

    private void SetLabel(TMP_Text t, string s) { if (t != null) t.text = s; }
    private void SetStatus(string s)             { if (statusLabel != null) statusLabel.text = s; }

    private static string FormatHz(float hz)
        => hz >= 1000f ? $"{hz / 1000f:F1} kHz" : $"{hz:F0} Hz";

    private static string FormatDB(float dB)
        => dB >= 0f ? $"+{dB:F1} dB" : $"{dB:F1} dB";

    private static string FormatPercent(float v)
        => $"{Mathf.RoundToInt(v * 100f)}%";

    private static string FormatSeconds(float s)
        => s < 1f ? $"{s * 1000f:F0} ms" : $"{s:F2} s";

    private static string FormatRatio(float r)
        => $"{r:F1}:1";
}
