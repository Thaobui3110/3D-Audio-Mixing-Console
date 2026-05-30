using UnityEngine;

/// <summary>
/// Hiển thị waveform dưới dạng 1024 khối 3D.
/// Đọc data từ SpectrumAnalyzer.Instance.WaveformData.
/// </summary>
public class Waveform : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] public GameObject the_pfCube;

    [Header("Scale")]
    [SerializeField, Range(50f, 500f)] private float heightScale = 150f;
    [SerializeField, Range(0.5f, 3f)]  private float binWidth    = 1f;

    [Header("Color")]
    [SerializeField] private Color quietColor = new Color(0.3f, 0.9f, 0.5f);
    [SerializeField] private Color loudColor  = new Color(1f, 0.3f, 0.1f);

    private Transform[]           cubeTransforms;
    private Renderer[]            cubeRenderers;
    private MaterialPropertyBlock propBlock;
    private static readonly int   BaseColorID = Shader.PropertyToID("_BaseColor");
    private const float           MinHeight   = 0.01f;
    private const int             Count       = 1024;

    private void Start()
    {
        if (the_pfCube == null) { Debug.LogError("[Waveform] the_pfCube chưa gán!", this); enabled = false; return; }

        cubeTransforms = new Transform[Count];
        cubeRenderers  = new Renderer[Count];
        propBlock      = new MaterialPropertyBlock();

        float xStart = -(Count * binWidth * 0.5f);
        for (int i = 0; i < Count; i++)
        {
            var go = Instantiate(the_pfCube, transform);
            go.name = "wf_" + i;
            go.transform.localPosition = new Vector3(xStart + i * binWidth, 0f, 0f);
            go.transform.localScale    = new Vector3(binWidth * 0.85f, MinHeight, 1f);
            cubeTransforms[i] = go.transform;
            cubeRenderers[i]  = go.GetComponent<Renderer>();
        }

        transform.position = new Vector3(transform.position.x, 100f, transform.position.z);
    }

    private void Update()
    {
        if (SpectrumAnalyzer.Instance == null) return;

        float[] wf = SpectrumAnalyzer.Instance.WaveformData;

        for (int i = 0; i < Count; i++)
        {
            float h = Mathf.Max(Mathf.Abs(wf[i]) * heightScale, MinHeight);

            var scale = cubeTransforms[i].localScale;
            cubeTransforms[i].localScale    = new Vector3(scale.x, h, scale.z);
            var pos = cubeTransforms[i].localPosition;
            cubeTransforms[i].localPosition = new Vector3(pos.x, h * 0.5f, pos.z);

            float t = Mathf.Clamp01(Mathf.Abs(wf[i]) * 4f);
            cubeRenderers[i].GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorID, Color.Lerp(quietColor, loudColor, t));
            cubeRenderers[i].SetPropertyBlock(propBlock);
        }
    }
}
