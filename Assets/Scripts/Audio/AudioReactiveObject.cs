using UnityEngine;

/// <summary>
/// Beat detection: Energy Window algorithm.
/// So sánh energy sub-bass (40-100Hz) của frame hiện tại
/// với trung bình của ~1 giây lịch sử. Hoạt động tốt với EDM.
/// </summary>
[DisallowMultipleComponent]
public class AudioReactiveObject : MonoBehaviour
{
    [Header("Floating")]
    [SerializeField] private bool  floatEnabled   = true;
    [SerializeField] private float floatHeight    = 1.2f;
    [SerializeField] private float floatAmplitude = 0.15f;
    [SerializeField] private float floatSpeed     = 1.5f;

    [Header("Beat Pulse")]
    [SerializeField] private bool  beatEnabled    = true;
    [SerializeField] private float baseScale      = 0.6f;
    [SerializeField] private float beatPunchScale = 0.4f;
    [SerializeField] private float beatAttack     = 50f;
    [SerializeField] private float beatDecay      = 7f;

    [Header("Beat Detection — Energy Window")]
    [Tooltip("Sub-bass thấp nhất (Hz). EDM kick ~40-60Hz")]
    [SerializeField] private float subBassMin      = 40f;
    [Tooltip("Sub-bass cao nhất (Hz). EDM kick ~80-100Hz")]
    [SerializeField] private float subBassMax      = 100f;
    [Tooltip("Energy hiện tại phải lớn hơn trung bình × hệ số này mới tính là beat.\n"
           + "EDM: thử 1.3–1.6. Tăng nếu đập nhiều quá, giảm nếu miss beat.")]
    [SerializeField] private float energyMultiplier = 1.35f;
    [Tooltip("Số lượng energy sample lưu lịch sử. 43 ≈ 1 giây ở 43fps")]
    [SerializeField] private int   historySize      = 43;
    [Tooltip("Thời gian tối thiểu giữa 2 beat (giây)")]
    [SerializeField] private float beatCooldown     = 0.15f;

    [Header("Emission")]
    [SerializeField] private bool  emissionEnabled    = true;
    [SerializeField] private Color baseEmission       = Color.black;
    [SerializeField] private Color peakEmission       = new Color(0.4f, 0.8f, 1f);
    [SerializeField] private float emissionSmooth     = 6f;

    [Header("Highlight")]
    [SerializeField] private Color highlightEmission   = new Color(1f, 0.8f, 0.1f);
    [SerializeField] private float highlightPulseSpeed = 3f;

    [Header("Rotation")]
    [SerializeField] private bool  rotateEnabled = false;
    [SerializeField] private float rotateSpeed   = 30f;

    // ── Private ────────────────────────────────────────────────────────────
    private Renderer              objectRenderer;
    private MaterialPropertyBlock propBlock;
    private AudioSource           linkedSource;

    // Scale
    private float currentScale;
    private float beatPunchAmount;

    // Emission
    private float currentEmission;

    // Float
    private float   floatPhase;
    private Vector3 basePosition;

    // Highlight
    private bool  isHighlighted;
    private float highlightPhase;

    // Beat detection — Energy Window
    private const int FFT_SIZE  = 1024;
    private float[]   fftBuffer = new float[FFT_SIZE];
    private float[]   energyHistory;       // circular buffer, lưu energy từng frame
    private int       historyIndex;
    private float     historySum;          // running sum để tính average O(1)
    private float     cooldownTimer;
    private int       binMin, binMax;      // cache bin index

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        objectRenderer = GetComponentInChildren<Renderer>();
        propBlock      = new MaterialPropertyBlock();
        currentScale   = baseScale;
        floatPhase     = Random.Range(0f, Mathf.PI * 2f);

        if (objectRenderer != null && emissionEnabled)
        {
            objectRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(EmissionColorID, baseEmission);
            objectRenderer.SetPropertyBlock(propBlock);
        }
    }

    private void Start()
    {
        linkedSource = GetComponent<AudioSource>()
                       ?? GetComponentInChildren<AudioSource>();

        basePosition = transform.position;
        if (floatEnabled)
        {
            basePosition.y     = floatHeight;
            transform.position = basePosition;
        }

        // Cache bin range: bin i = tần số i × (sampleRate / FFT_SIZE)
        float sampleRate = AudioSettings.outputSampleRate;
        float hzPerBin   = sampleRate / FFT_SIZE;
        binMin = Mathf.Max(0,            Mathf.RoundToInt(subBassMin / hzPerBin));
        binMax = Mathf.Min(FFT_SIZE / 2, Mathf.RoundToInt(subBassMax / hzPerBin));

        // Khởi tạo history buffer — fill bằng giá trị nhỏ để tránh false beat lúc đầu
        energyHistory = new float[historySize];
        for (int i = 0; i < historySize; i++) energyHistory[i] = 0.0001f;
        historySum    = 0.0001f * historySize;
        historyIndex  = 0;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // ── Lấy audio data ────────────────────────────────────────────────
        float rms = 0f;

        if (linkedSource != null && linkedSource.isPlaying && linkedSource.clip != null)
        {
            // RMS cho emission
            float[] wfBuf = new float[256];
            linkedSource.GetOutputData(wfBuf, 0);
            float sum = 0f;
            foreach (float s in wfBuf) sum += s * s;
            rms = Mathf.Sqrt(sum / wfBuf.Length);

            // FFT cho beat detection
            linkedSource.GetSpectrumData(fftBuffer, 0, FFTWindow.BlackmanHarris);
        }

        // ── Energy Window beat detection ──────────────────────────────────
        cooldownTimer -= dt;
        if (beatEnabled) DetectBeat();

        // ── Visual ────────────────────────────────────────────────────────
        UpdateScale(dt);
        UpdateFloat(dt);
        UpdateEmission(rms, dt);

        if (rotateEnabled)
            transform.Rotate(Vector3.up, rotateSpeed * dt, Space.World);
    }

    // ── Beat detection ─────────────────────────────────────────────────────

    private void DetectBeat()
    {
        // 1. Tính energy hiện tại của dải sub-bass
        //    Dùng energy (bình phương biên độ) thay vì biên độ — nhạy hơn với spike
        float energy = 0f;
        for (int i = binMin; i <= binMax; i++)
            energy += fftBuffer[i] * fftBuffer[i];

        // 2. Cập nhật circular buffer
        historySum -= energyHistory[historyIndex];
        energyHistory[historyIndex] = energy;
        historySum += energy;
        historyIndex = (historyIndex + 1) % historySize;

        float avgEnergy = historySum / historySize;

        // 3. Beat nếu energy hiện tại vượt ngưỡng × trung bình lịch sử
        //    và cooldown xong
        if (energy > energyMultiplier * avgEnergy && cooldownTimer <= 0f)
        {
            // Cường độ punch tỉ lệ với mức vượt ngưỡng
            float strength  = Mathf.Clamp01((energy - avgEnergy) / (avgEnergy + 0.0001f));
            beatPunchAmount = beatPunchScale * Mathf.Lerp(0.5f, 1f, strength);
            cooldownTimer   = beatCooldown;
        }
    }

    // ── Scale ──────────────────────────────────────────────────────────────

    private void UpdateScale(float dt)
    {
        if (!beatEnabled)
        {
            transform.localScale = Vector3.one * baseScale;
            return;
        }

        // beatPunchAmount decay
        beatPunchAmount = Mathf.Lerp(beatPunchAmount, 0f, dt * beatDecay);

        // currentScale chase target
        float targetScale = baseScale + beatPunchAmount;
        float speed       = targetScale > currentScale ? beatAttack : beatDecay;
        currentScale      = Mathf.Lerp(currentScale, targetScale, dt * speed);

        transform.localScale = Vector3.one * currentScale;
    }

    // ── Float ──────────────────────────────────────────────────────────────

    private void UpdateFloat(float dt)
    {
        if (!floatEnabled) return;
        floatPhase += dt * floatSpeed;
        transform.position = basePosition + Vector3.up * (Mathf.Sin(floatPhase) * floatAmplitude);
    }

    // ── Emission ───────────────────────────────────────────────────────────

    private void UpdateEmission(float rms, float dt)
    {
        if (objectRenderer == null) return;

        Color targetEmit;
        if (isHighlighted)
        {
            highlightPhase += dt * highlightPulseSpeed;
            float t = (Mathf.Sin(highlightPhase) + 1f) * 0.5f;
            targetEmit = Color.Lerp(highlightEmission * 0.3f, highlightEmission, t);
        }
        else
        {
            currentEmission = Mathf.Lerp(currentEmission, rms, dt * emissionSmooth);
            targetEmit      = Color.Lerp(baseEmission, peakEmission, currentEmission * 3f);
        }

        objectRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(EmissionColorID, targetEmit);
        objectRenderer.SetPropertyBlock(propBlock);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void SetHighlighted(bool highlighted)
    {
        isHighlighted  = highlighted;
        highlightPhase = 0f;
        if (!highlighted) currentEmission = 0f;
    }

    public void UpdateBasePosition()
    {
        basePosition   = transform.position;
        basePosition.y = floatHeight;
    }
}