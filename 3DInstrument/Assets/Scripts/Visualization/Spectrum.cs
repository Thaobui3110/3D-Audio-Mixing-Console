using UnityEngine;

/// <summary>
/// Hiển thị phổ tần số (spectrum) dưới dạng 512 khối 3D.
/// Gradient màu: đỏ (bass) → xanh lá (mid) → tím (treble).
/// Thêm peak hold: mỗi bin giữ đỉnh trong vài giây trước khi rơi.
///
/// Inspector:
///   - Kéo Cube prefab vào the_pfCube
///   - Chỉnh scaleMultiplier để thay đổi chiều cao tổng thể
///   - logScale = true: scale theo log (giống analyzer thực tế)
/// </summary>
public class Spectrum : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject the_pfCube;

    [Header("Scale")]
    public float scaleMultiplier = 600f;
    public bool  logScale        = true;

    [Header("Peak Hold")]
    public float peakHoldTime   = 1.2f;  // giây giữ đỉnh
    public float peakFallSpeed  = 80f;   // units/s rơi xuống

    [Header("Colors")]
    public Color bassColor    = new Color(1f, 0.2f, 0.1f);    // đỏ
    public Color midColor     = new Color(0.2f, 1f, 0.3f);    // xanh lá
    public Color trebleColor  = new Color(0.5f, 0.3f, 1f);    // tím

    // ── Private ────────────────────────────────────────────────────────────
    private Transform[]           cubeTransforms;
    private Renderer[]            cubeRenderers;
    private MaterialPropertyBlock propBlock;
    private float[]               peakValues;
    private float[]               peakTimers;
    private const int             COUNT = 512;

    private void Start()
    {
        cubeTransforms = new Transform[COUNT];
        cubeRenderers  = new Renderer[COUNT];
        peakValues     = new float[COUNT];
        peakTimers     = new float[COUNT];
        propBlock      = new MaterialPropertyBlock();

        float xStart = -(COUNT * 1f);  // 2 units per bin

        for (int i = 0; i < COUNT; i++)
        {
            var go = Instantiate(the_pfCube, transform);
            go.name = "bin_" + i;
            go.transform.localPosition = new Vector3(xStart + i * 2f, 0f, 0f);
            go.transform.localScale    = new Vector3(2f, 0.01f, 1f);

            cubeTransforms[i] = go.transform;
            cubeRenderers[i]  = go.GetComponent<Renderer>();
        }

        transform.position = new Vector3(transform.position.x, -100f, transform.position.z);
    }

    private void Update()
    {
        float[] spectrum = ChunityAudioInput.the_spectrum;
        float   dt       = Time.deltaTime;

        for (int i = 0; i < COUNT; i++)
        {
            float raw = spectrum[i];
            float h   = logScale
                ? scaleMultiplier * Mathf.Log10(1f + raw * 9f)   // log scale
                : scaleMultiplier * Mathf.Sqrt(raw);               // sqrt (code cũ)
            h = Mathf.Max(h, 0.01f);

            // Peak hold
            if (h >= peakValues[i])
            {
                peakValues[i] = h;
                peakTimers[i] = peakHoldTime;
            }
            else
            {
                peakTimers[i] -= dt;
                if (peakTimers[i] < 0f)
                    peakValues[i] = Mathf.Max(peakValues[i] - peakFallSpeed * dt, h);
            }

            // Transform
            var s = cubeTransforms[i].localScale;
            cubeTransforms[i].localScale    = new Vector3(s.x, h, s.z);
            var p = cubeTransforms[i].localPosition;
            cubeTransforms[i].localPosition = new Vector3(p.x, h * 0.5f, p.z);

            // Gradient màu theo bin index (bass → mid → treble)
            float t = (float)i / COUNT;
            Color c = t < 0.5f
                ? Color.Lerp(bassColor, midColor,    t * 2f)
                : Color.Lerp(midColor,  trebleColor, (t - 0.5f) * 2f);

            cubeRenderers[i].GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", c);
            cubeRenderers[i].SetPropertyBlock(propBlock);
        }
    }
}
