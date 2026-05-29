using UnityEngine;

/// <summary>
/// Hiển thị dạng sóng âm (waveform) dưới dạng 1024 khối 3D.
/// Màu thay đổi theo amplitude: xanh lá (yên tĩnh) → vàng → đỏ (to).
///
/// Inspector:
///   - Kéo Cube prefab vào the_pfCube
///   - Điều chỉnh heightScale để thay đổi chiều cao
///   - Object này tự đặt vị trí Y=100 lúc Start
/// </summary>
public class Waveform : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject the_pfCube;

    [Header("Scale")]
    public float heightScale = 200f;

    [Header("Color")]
    public Color quietColor  = new Color(0.4f, 1f, 0.4f);   // xanh lá
    public Color loudColor   = new Color(1f, 0.3f, 0.1f);    // đỏ cam

    // ── Private ────────────────────────────────────────────────────────────
    private Transform[]           cubeTransforms;
    private MaterialPropertyBlock propBlock;
    private Renderer[]            cubeRenderers;
    private const int             COUNT = 1024;

    private void Start()
    {
        cubeTransforms = new Transform[COUNT];
        cubeRenderers  = new Renderer[COUNT];
        propBlock      = new MaterialPropertyBlock();

        float xStart = -(COUNT * 0.5f);

        for (int i = 0; i < COUNT; i++)
        {
            var go = Instantiate(the_pfCube, transform);
            go.name = "wf_" + i;
            go.transform.localPosition = new Vector3(xStart + i, 0f, 0f);

            cubeTransforms[i] = go.transform;
            cubeRenderers[i]  = go.GetComponent<Renderer>();
        }

        transform.position = new Vector3(transform.position.x, 100f, transform.position.z);
    }

    private void Update()
    {
        float[] wf = ChunityAudioInput.the_waveform;

        for (int i = 0; i < COUNT; i++)
        {
            float sample = wf[i];
            float h      = Mathf.Abs(sample) * heightScale;

            var pos = cubeTransforms[i].localPosition;
            cubeTransforms[i].localPosition = new Vector3(pos.x, h * 0.5f, pos.z);
            cubeTransforms[i].localScale    = new Vector3(
                cubeTransforms[i].localScale.x, Mathf.Max(h, 0.01f),
                cubeTransforms[i].localScale.z
            );

            // Màu theo amplitude
            float t = Mathf.Clamp01(Mathf.Abs(sample) * 4f);
            cubeRenderers[i].GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", Color.Lerp(quietColor, loudColor, t));
            cubeRenderers[i].SetPropertyBlock(propBlock);
        }
    }
}
