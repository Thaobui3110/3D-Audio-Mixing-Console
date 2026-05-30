using UnityEngine;

/// <summary>
/// Hiển thị phổ tần số dưới dạng N khối 3D.
/// Đọc data từ SpectrumAnalyzer.Instance (Unity native, không cần Chunity).
///
/// HIERARCHY:
///   Systems → SpectrumVisualizer → Spectrum (component này)
///   (spawn N bin_ objects lúc runtime)
/// </summary>
public class Spectrum : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] public GameObject the_pfCube;

    [Header("Display")]
    [SerializeField, Range(32, 512)] private int binCount = 256;
    [SerializeField] private float scaleMultiplier = 300f;
    [SerializeField] private bool  logScale        = true;

    [Header("Peak Hold")]
    [SerializeField] private float peakHoldTime  = 1.0f;
    [SerializeField] private float peakFallSpeed = 60f;

    [Header("Colors")]
    [SerializeField] private Color bassColor   = new Color(1f, 0.2f, 0.1f);
    [SerializeField] private Color midColor    = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private Color trebleColor = new Color(0.5f, 0.3f, 1f);

    // ── Private ────────────────────────────────────────────────────────────
    private Transform[]           cubeTransforms;
    private Renderer[]            cubeRenderers;
    private MaterialPropertyBlock propBlock;
    private float[]               peakValues;
    private float[]               peakTimers;
    private static readonly int   BaseColorID = Shader.PropertyToID("_BaseColor");
    private const float           MinHeight   = 0.01f;
    private const float           BinWidth    = 2f;

    private void Start()
    {
        if (the_pfCube == null) { Debug.LogError("[Spectrum] the_pfCube chưa gán!", this); enabled = false; return; }

        cubeTransforms = new Transform[binCount];
        cubeRenderers  = new Renderer[binCount];
        peakValues     = new float[binCount];
        peakTimers     = new float[binCount];
        propBlock      = new MaterialPropertyBlock();

        float xStart = -(binCount * BinWidth * 0.5f);
        for (int i = 0; i < binCount; i++)
        {
            var go = Instantiate(the_pfCube, transform);
            go.name = "bin_" + i;
            go.transform.localPosition = new Vector3(xStart + i * BinWidth, 0f, 0f);
            go.transform.localScale    = new Vector3(BinWidth * 0.85f, MinHeight, 1f);
            cubeTransforms[i] = go.transform;
            cubeRenderers[i]  = go.GetComponent<Renderer>();
        }

        transform.position = new Vector3(transform.position.x, -100f, transform.position.z);
    }

    private void Update()
    {
        if (SpectrumAnalyzer.Instance == null) return;

        float[] data  = SpectrumAnalyzer.Instance.SpectrumData;
        int     count = Mathf.Min(binCount, data.Length);
        float   dt    = Time.deltaTime;

        for (int i = 0; i < count; i++)
        {
            float h = logScale
                ? scaleMultiplier * Mathf.Log10(1f + data[i] * 9f)
                : scaleMultiplier * Mathf.Sqrt(data[i]);
            h = Mathf.Max(h, MinHeight);

            // Peak hold
            if (h >= peakValues[i])  { peakValues[i] = h; peakTimers[i] = peakHoldTime; }
            else
            {
                peakTimers[i] -= dt;
                if (peakTimers[i] < 0f)
                    peakValues[i] = Mathf.Max(peakValues[i] - peakFallSpeed * dt, h);
            }

            // Transform
            var scale = cubeTransforms[i].localScale;
            cubeTransforms[i].localScale    = new Vector3(scale.x, h, scale.z);
            var pos = cubeTransforms[i].localPosition;
            cubeTransforms[i].localPosition = new Vector3(pos.x, h * 0.5f, pos.z);

            // Color gradient
            float t = (float)i / count;
            Color c = t < 0.5f
                ? Color.Lerp(bassColor, midColor,    t * 2f)
                : Color.Lerp(midColor,  trebleColor, (t - 0.5f) * 2f);

            cubeRenderers[i].GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorID, c);
            cubeRenderers[i].SetPropertyBlock(propBlock);
        }
    }
}
