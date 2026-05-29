using UnityEngine;

/// <summary>
/// Xử lý occlusion âm thanh: khi có vật cản giữa source và listener,
/// tự động hạ cutoff frequency của LowPassFilter.
///
/// Inspector setup:
///   - Đặt lên cùng GameObject với AudioSource của SoundObject
///   - Nếu listener = null, tự tìm AudioListener trong scene
///   - LowPassFilter phải có sẵn trên GameObject
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter))]
public class OcclusionProcessor : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Thường là Main Camera. Để null để tự tìm AudioListener.")]
    public Transform     listener;
    public LayerMask     occlusionLayers = ~0;
    [Range(1, 5)] public int rayCount   = 3;   // nhiều ray → mượt hơn, tốn hơn

    [Header("Effect")]
    [Range(0f, 1f)]       public float occlusionStrength = 0.8f;
    [Range(200f, 22000f)] public float openCutoff        = 22000f;
    [Range(200f, 22000f)] public float blockedCutoff     = 800f;
    [Range(1f, 20f)]      public float smoothSpeed       = 5f;

    private AudioLowPassFilter lpFilter;
    private float currentOcclusion;

    private void Awake()
    {
        lpFilter = GetComponent<AudioLowPassFilter>();

        // Auto-find listener nếu chưa gán
        if (!listener)
        {
            var al = FindObjectOfType<AudioListener>();
            if (al) listener = al.transform;
        }
    }

    private void Update()
    {
        float target = listener ? ComputeOcclusion() : 0f;
        currentOcclusion = Mathf.Lerp(currentOcclusion, target, Time.deltaTime * smoothSpeed);

        lpFilter.cutoffFrequency = Mathf.Lerp(openCutoff, blockedCutoff, currentOcclusion);
    }

    private float ComputeOcclusion()
    {
        Vector3 dir  = listener.position - transform.position;
        float   dist = dir.magnitude;
        int     hits = 0;

        for (int i = 0; i < rayCount; i++)
        {
            Vector3 jitter = dir + Random.insideUnitSphere * 0.25f;
            if (Physics.Raycast(transform.position, jitter.normalized, dist, occlusionLayers))
                hits++;
        }

        return (float)hits / rayCount * occlusionStrength;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!listener) return;
        Gizmos.color = Color.Lerp(Color.green, Color.red, currentOcclusion);
        Gizmos.DrawLine(transform.position, listener.position);
    }
#endif
}
