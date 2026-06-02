using UnityEngine;

/// <summary>
/// Tắt frustum culling cho Camera.
/// Gắn lên Main Camera — model sẽ không bao giờ bị cắt khi xoay/di chuyển.
/// Performance OK cho scene nhỏ (1 phòng + vài speakers).
/// </summary>
[RequireComponent(typeof(Camera))]
public class DisableFrustumCulling : MonoBehaviour
{
    private Camera cam;

    private void Start()
    {
        cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        // Override culling matrix = frustum cực lớn
        // → Unity coi MỌI object đều nằm trong tầm nhìn → không cull
        cam.cullingMatrix = Matrix4x4.Ortho(-99999, 99999, -99999, 99999, 0.001f, 99999)
                          * cam.worldToCameraMatrix;
    }

    private void OnDisable()
    {
        // Restore culling bình thường khi tắt script
        cam.ResetCullingMatrix();
    }
}