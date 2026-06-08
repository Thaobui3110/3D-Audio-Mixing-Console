using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI chính của Audio Processor Sandbox.
/// Quản lý: upload file, play/stop, source dropdown, tất cả sliders DSP.
///
/// ROUTING:
///   Per-object (qua SpatialSoundObject): LP, HP, Mid EQ, Compressor
///   Global (qua AudioProcessorController → AudioMixer): Reverb Wet/Decay, Master Volume
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
    [SerializeField] private Button         browseButton;
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

    /// <summary>
    /// Lưu giá trị slider hiện tại.
    /// Per-object params (LP, HP, MidEQ, Comp): dùng khi spawn speaker mới ở mode "All Sources".
    /// Global params (reverbWet, reverbDecay, masterVolume): luôn đồng bộ với AudioMixer.
    /// </summary>
    private struct SliderState
    {
        public float lowPass, highPass, midEQ;
        public float reverbWet, reverbDecay;
        public float compThreshold, compMakeupGain;
        public float masterVolume;
    }
    private SliderState _globalState = new()
    {
        lowPass       = 22000f,
        highPass      = 20f,
        midEQ         = 0f,
        reverbWet     = 0f,       // 0..1 (không phải room value)
        reverbDecay   = 1f,
        compThreshold = -60f,
        compMakeupGain= 0f,
        masterVolume  = 1f,
    };

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

        // Global params (Reverb, MasterVolume) → AudioProcessorController.Start()
        // đã ApplyAllDefaults(). Giá trị khớp với _globalState defaults.
        // Per-object DSP (LP, HP, MidEQ, Comp) → áp qua SpatialSoundObject.

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
        // Fix #2: khởi tạo từ _globalState (không phụ thuộc processor)
        ConfigSlider(sliderLowPass,        200f,  22000f, _globalState.lowPass);
        ConfigSlider(sliderHighPass,        20f,   2000f, _globalState.highPass);
        ConfigSlider(sliderMidEQ,          -12f,    12f,  _globalState.midEQ);
        ConfigSlider(sliderReverbWet,        0f,     1f,  _globalState.reverbWet);
        ConfigSlider(sliderReverbDecay,    0.1f,   10f,   _globalState.reverbDecay);
        ConfigSlider(sliderCompThreshold,  -60f,    0f,   _globalState.compThreshold);
        ConfigSlider(sliderCompMakeupGain,   0f,   20f,   _globalState.compMakeupGain);
        ConfigSlider(sliderMasterVolume,     0f,    1f,   _globalState.masterVolume);

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
        if (browseButton != null) browseButton.onClick.AddListener(OnBrowseClicked);
        if (loadButton   != null) loadButton.onClick.AddListener(OnLoadClicked);
        if (playButton   != null) playButton.onClick.AddListener(OnPlayClicked);
        if (stopButton   != null) stopButton.onClick.AddListener(OnStopClicked);
        if (spawnButton  != null) spawnButton.onClick.AddListener(OnSpawnClicked);
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
    private void OnBrowseClicked()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel(
            "Select Audio File", "", "wav,mp3,ogg");
        if (!string.IsNullOrEmpty(path) && pathInputField != null)
        {
            pathInputField.text = path;
            SetStatus($"Đã chọn: {System.IO.Path.GetFileName(path)}");
        }
#else
        SetStatus("Browse chỉ hoạt động trong Editor. Nhập path thủ công cho build.");
#endif
    }

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
        var speaker = audioManager?.SpawnSpeaker();
        RefreshDropdown();
        if (speaker != null)
        {
            // Fix #2: apply _globalState lên speaker mới — tránh speaker mới bị "reset về default"
            // khi user đã kéo slider ở mode "All Sources" trước đó.
            ApplyGlobalStateToSpeaker(speaker);

            var p = speaker.transform.position;
            SetStatus($"✓ Spawned {speaker.SourceID} tại ({p.x:F1}, {p.y:F1}, {p.z:F1}) — nhìn phía trước.");
        }
    }

    /// <summary>
    /// Apply _globalState (giá trị slider hiện tại khi "All Sources") vào một speaker cụ thể.
    /// Chỉ apply per-object params (EQ, Compressor). Reverb/Master là global → không apply.
    /// </summary>
    private void ApplyGlobalStateToSpeaker(SpatialSoundObject s)
    {
        if (s == null) return;
        s.SetEQLowPass(_globalState.lowPass);
        s.SetEQHighPass(_globalState.highPass);
        s.SetEQMidGain(_globalState.midEQ);
        s.SetCompThreshold(_globalState.compThreshold);
        s.SetCompMakeupGain(_globalState.compMakeupGain);
    }

    private void OnDropdownChanged(int index)
    {
        if (sourceDropdown == null || index < 0) return;
        selectedSourceID = index == 0 ? null : sourceDropdown.options[index].text;

        var src = GetSelectedSource();
        if (src != null)
        {
            // Load per-object values vào sliders (chỉ per-object params)
            SetSliderNoNotify(sliderLowPass,          src.EQLowPassCutoff);
            SetSliderNoNotify(sliderHighPass,         src.EQHighPassCutoff);
            SetSliderNoNotify(sliderMidEQ,            src.EQMidGain);
            SetSliderNoNotify(sliderCompThreshold,    src.CompThreshold);
            SetSliderNoNotify(sliderCompMakeupGain,   src.CompMakeupGain);

            SetLabel(labelLowPass,        FormatHz(src.EQLowPassCutoff));
            SetLabel(labelHighPass,       FormatHz(src.EQHighPassCutoff));
            SetLabel(labelMidEQ,          FormatDB(src.EQMidGain));
            SetLabel(labelCompThreshold,  FormatDB(src.CompThreshold));
            SetLabel(labelCompMakeupGain, FormatDB(src.CompMakeupGain));

            // Reverb Wet/Decay, Master Volume → global, không đổi khi switch speaker
            SetStatus($"Selected: {selectedSourceID} — per-object EQ/Comp riêng, Reverb/Master chung");
        }
        else
        {
            // "All Sources" — restore _globalState cho per-object sliders
            SetSliderNoNotify(sliderLowPass,        _globalState.lowPass);
            SetSliderNoNotify(sliderHighPass,       _globalState.highPass);
            SetSliderNoNotify(sliderMidEQ,          _globalState.midEQ);
            SetSliderNoNotify(sliderCompThreshold,  _globalState.compThreshold);
            SetSliderNoNotify(sliderCompMakeupGain, _globalState.compMakeupGain);

            SetLabel(labelLowPass,        FormatHz(_globalState.lowPass));
            SetLabel(labelHighPass,       FormatHz(_globalState.highPass));
            SetLabel(labelMidEQ,          FormatDB(_globalState.midEQ));
            SetLabel(labelCompThreshold,  FormatDB(_globalState.compThreshold));
            SetLabel(labelCompMakeupGain, FormatDB(_globalState.compMakeupGain));

            SetStatus("All Sources — EQ/Comp áp tất cả speakers, Reverb/Master chung");
        }
    }

    /// <summary>Lấy SpatialSoundObject đang chọn trong dropdown.</summary>
    private SpatialSoundObject GetSelectedSource()
    {
        if (string.IsNullOrEmpty(selectedSourceID) || audioManager == null) return null;
        return audioManager.Get(selectedSourceID);
    }

    /// <summary>Set slider value mà không trigger OnValueChanged callback.</summary>
    private void SetSliderNoNotify(Slider s, float v)
    {
        if (s == null) return;
        s.SetValueWithoutNotify(v);
    }

    // ── Slider callbacks ──────────────────────────────────────────────────
    // Per-object (LP, HP, MidEQ, Comp): chọn speaker → riêng speaker đó;
    //   "All Sources" → áp tất cả + lưu _globalState.
    // Global (Reverb, Master): luôn áp lên AudioMixer qua processor.

    public void OnLowPassChanged(float v)
    {
        if (string.IsNullOrEmpty(selectedSourceID)) _globalState.lowPass = v;
        PerObject(s => s.SetEQLowPass(v));
        SetLabel(labelLowPass, FormatHz(v));
    }

    public void OnHighPassChanged(float v)
    {
        if (string.IsNullOrEmpty(selectedSourceID)) _globalState.highPass = v;
        PerObject(s => s.SetEQHighPass(v));
        SetLabel(labelHighPass, FormatHz(v));
    }

    public void OnMidEQChanged(float v)
    {
        if (string.IsNullOrEmpty(selectedSourceID)) _globalState.midEQ = v;
        PerObject(s => s.SetEQMidGain(v));
        SetLabel(labelMidEQ, FormatDB(v));
    }

    public void OnReverbWetChanged(float v)
    {
        _globalState.reverbWet = v;
        processor.SetReverbWet(v);
        SetLabel(labelReverbWet, FormatPercent(v));
    }

    public void OnReverbDecayChanged(float v)
    {
        _globalState.reverbDecay = v;
        processor.SetReverbDecay(v);
        SetLabel(labelReverbDecay, FormatSeconds(v));
    }

    public void OnCompThresholdChanged(float v)
    {
        if (string.IsNullOrEmpty(selectedSourceID)) _globalState.compThreshold = v;
        PerObject(s => s.SetCompThreshold(v));
        SetLabel(labelCompThreshold, FormatDB(v));
    }

    public void OnCompMakeupGainChanged(float v)
    {
        if (string.IsNullOrEmpty(selectedSourceID)) _globalState.compMakeupGain = v;
        PerObject(s => s.SetCompMakeupGain(v));
        SetLabel(labelCompMakeupGain, FormatDB(v));
    }

    public void OnMasterVolumeChanged(float v)
    {
        _globalState.masterVolume = v;
        processor.SetMasterVolume(v);
        SetLabel(labelMasterVolume, FormatPercent(v));
    }

    /// <summary>Áp dụng cho speaker đang chọn, hoặc tất cả nếu "All Sources".</summary>
    private void PerObject(System.Action<SpatialSoundObject> action)
    {
        var src = GetSelectedSource();
        if (src != null)
        {
            action(src);
        }
        else if (audioManager != null)
        {
            foreach (var id in audioManager.GetAllIDs())
            {
                var s = audioManager.Get(id);
                if (s != null) action(s);
            }
        }
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
        // Fix #2: speaker spawn từ bất kỳ đâu (kể cả ngoài button) đều nhận _globalState
        audioManager.OnSourceSpawned += OnExternalSpawn;
        audioManager.OnSourceRemoved += _ => RefreshDropdown();
    }

    private void UnhookAudioManager()
    {
        if (audioManager == null) return;
        audioManager.OnSourceSpawned -= OnExternalSpawn;
        audioManager.OnSourceRemoved -= _ => RefreshDropdown();
    }

    private void OnExternalSpawn(SpatialSoundObject speaker)
    {
        RefreshDropdown();
        // Chỉ apply nếu chưa có speaker cụ thể nào đang được chọn (đang ở "All Sources")
        if (string.IsNullOrEmpty(selectedSourceID))
            ApplyGlobalStateToSpeaker(speaker);
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
        // Fix #1: labels đọc từ _globalState, không phụ thuộc vào AudioProcessorController
        SetLabel(labelLowPass,        FormatHz(_globalState.lowPass));
        SetLabel(labelHighPass,       FormatHz(_globalState.highPass));
        SetLabel(labelMidEQ,          FormatDB(_globalState.midEQ));
        SetLabel(labelReverbWet,      FormatPercent(_globalState.reverbWet));
        SetLabel(labelReverbDecay,    FormatSeconds(_globalState.reverbDecay));
        SetLabel(labelCompThreshold,  FormatDB(_globalState.compThreshold));
        SetLabel(labelCompMakeupGain, FormatDB(_globalState.compMakeupGain));
        SetLabel(labelMasterVolume,   FormatPercent(_globalState.masterVolume));
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