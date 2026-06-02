using UnityEngine;

/// <summary>
/// Speaker mesh phản ứng trực quan với âm thanh:
///   - Lơ lửng cố định (không bob lên xuống)
///   - Scale zoom in/out theo bass (attack nhanh, decay chậm)
///   - Xoay vô định trên 3 trục
///   - Phát sáng (Emission) theo volume
/// </summary>
[DisallowMultipleComponent]
public class AudioReactiveObject : MonoBehaviour
{
    [Header("Beat Zoom (Scale theo nhịp)")]
    [SerializeField] private bool  scaleEnabled = true;
    [SerializeField] private float baseScale    = 0.6f;
    [SerializeField] private float beatPunch    = 0.5f;
    [SerializeField] private float attackSpeed  = 25f;
    [SerializeField] private float decaySpeed   = 4f;

    [Header("Rotation (Xoay vô định)")]
    [SerializeField] private bool  rotateEnabled = true;
    [SerializeField] private float rotateBase    = 15f;   // tốc độ xoay cơ bản (deg/s)
    [SerializeField] private float rotateBeat    = 40f;   // thêm tốc độ khi beat mạnh

    [Header("Emission Reaction")]
    [SerializeField] private bool  emissionEnabled = true;
    [SerializeField] private Color baseEmission    = Color.black;
    [SerializeField] private Color peakEmission    = new Color(0.4f, 0.8f, 1f);
    [SerializeField] private float emissionSmooth  = 6f;

    // ── Private ────────────────────────────────────────────────────────────
    private Renderer              objectRenderer;
    private MaterialPropertyBlock propBlock;
    private float                 currentScale;
    private float                 currentEmission;
    private Vector3               rotateAxis;     // trục xoay ngẫu nhiên cho mỗi speaker
    private static readonly int   EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        objectRenderer = GetComponentInChildren<Renderer>();
        propBlock      = new MaterialPropertyBlock();
        currentScale   = baseScale;

        // Mỗi speaker có trục xoay ngẫu nhiên riêng → xoay vô định khác nhau
        rotateAxis = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)
        ).normalized;

        if (objectRenderer != null && emissionEnabled)
        {
            objectRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(EmissionColorID, baseEmission);
            objectRenderer.SetPropertyBlock(propBlock);
        }
    }

    private void Update()
    {
        float dt   = Time.deltaTime;
        float rms  = 0f;
        float bass = 0f;

        if (SpectrumAnalyzer.Instance != null)
        {
            rms  = SpectrumAnalyzer.Instance.RMS;
            bass = SpectrumAnalyzer.Instance.Bass;
        }

        if (scaleEnabled)    UpdateBeatScale(bass, dt);
        if (rotateEnabled)   UpdateRotation(bass, dt);
        if (emissionEnabled) UpdateEmission(rms, dt);
    }

    private void UpdateBeatScale(float bass, float dt)
    {
        float target = baseScale + bass * beatPunch;
        float speed  = target > currentScale ? attackSpeed : decaySpeed;
        currentScale = Mathf.Lerp(currentScale, target, dt * speed);
        transform.localScale = Vector3.one * currentScale;
    }

    private void UpdateRotation(float bass, float dt)
    {
        float speed = rotateBase + bass * rotateBeat;
        transform.Rotate(rotateAxis, speed * dt, Space.World);
    }

    private void UpdateEmission(float rms, float dt)
    {
        if (objectRenderer == null) return;
        currentEmission = Mathf.Lerp(currentEmission, rms, dt * emissionSmooth);
        Color emColor   = Color.Lerp(baseEmission, peakEmission, currentEmission * 3f);

        objectRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(EmissionColorID, emColor);
        objectRenderer.SetPropertyBlock(propBlock);
    }
}