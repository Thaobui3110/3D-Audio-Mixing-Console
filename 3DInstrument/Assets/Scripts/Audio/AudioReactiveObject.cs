using UnityEngine;

/// <summary>
/// Làm cho speaker mesh phản ứng trực quan với âm thanh:
///   - Scale theo bass
///   - Phát sáng (Emission) theo volume
///   - Xoay nhẹ theo RMS
///
/// HIERARCHY:
///   Speaker_01
///   ├── SpatialSoundObject
///   └── AudioReactiveObject   ← component này (cùng GameObject hoặc child visual)
///
/// Material phải dùng URP/Lit hoặc HDRP/Lit để hỗ trợ Emission.
/// </summary>
[DisallowMultipleComponent]
public class AudioReactiveObject : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Scale Reaction")]
    [SerializeField] private bool  scaleEnabled    = true;
    [SerializeField] private float baseScale       = 1f;
    [SerializeField] private float scaleMultiplier = 2f;
    [SerializeField] private float scaleSmooth     = 8f;

    [Header("Emission Reaction")]
    [SerializeField] private bool  emissionEnabled = true;
    [SerializeField] private Color baseEmission    = Color.black;
    [SerializeField] private Color peakEmission    = new Color(0.4f, 0.8f, 1f);
    [SerializeField] private float emissionSmooth  = 6f;

    [Header("Rotation")]
    [SerializeField] private bool  rotateEnabled   = false;
    [SerializeField] private float rotateSpeed     = 30f;

    // ── Private ────────────────────────────────────────────────────────────
    private Renderer           objectRenderer;
    private MaterialPropertyBlock propBlock;
    private float              currentScale;
    private float              currentEmission;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        objectRenderer = GetComponentInChildren<Renderer>();
        propBlock      = new MaterialPropertyBlock();
        currentScale   = baseScale;

        if (objectRenderer != null && emissionEnabled)
        {
            // Enable emission keyword trên material instance
            objectRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(EmissionColorID, baseEmission);
            objectRenderer.SetPropertyBlock(propBlock);
        }
    }

    private void Update()
    {
        if (SpectrumAnalyzer.Instance == null) return;

        float rms  = SpectrumAnalyzer.Instance.RMS;
        float bass = SpectrumAnalyzer.Instance.Bass;
        float dt   = Time.deltaTime;

        if (scaleEnabled)    UpdateScale(bass, dt);
        if (emissionEnabled) UpdateEmission(rms, dt);
        if (rotateEnabled)   transform.Rotate(0f, rotateSpeed * rms * dt, 0f);
    }

    // ── Private ────────────────────────────────────────────────────────────
    private void UpdateScale(float bass, float dt)
    {
        float target = baseScale + bass * scaleMultiplier;
        currentScale = Mathf.Lerp(currentScale, target, dt * scaleSmooth);
        transform.localScale = Vector3.one * currentScale;
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
