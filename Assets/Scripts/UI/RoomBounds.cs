// Assets/Scripts/Environment/RoomBounds.cs
// Gắn vào GameObject "Room" — tự tính phạm vi phòng từ các BoxCollider con.
// Các script khác (SpatialAudioManager) dùng RoomBounds.Instance để clamp vị trí.

using UnityEngine;

public class RoomBounds : MonoBehaviour
{
    // ── Singleton ──
    public static RoomBounds Instance { get; private set; }

    [Header("Room Bounds (auto-calculated or manual)")]
    [Tooltip("Nếu tick, tự tính từ collider con khi Awake. Nếu không, dùng giá trị manual.")]
    public bool autoDetect = true;

    [Header("Manual Bounds (dùng nếu autoDetect = false)")]
    public Vector3 center = Vector3.zero;
    public Vector3 size   = new Vector3(10f, 4f, 12f);

    // ── Margin — khoảng cách tối thiểu từ tường ──
    [Header("Spawn Margin")]
    [Tooltip("Khoảng cách tối thiểu từ tường khi spawn speaker")]
    public float margin = 0.8f;

    // ── Bounds tính được ──
    public Bounds RoomArea { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (autoDetect)
        {
            CalculateBoundsFromColliders();
        }
        else
        {
            RoomArea = new Bounds(center, size);
        }

        Debug.Log($"[RoomBounds] Bounds: center={RoomArea.center}, size={RoomArea.size}");
    }

    private void CalculateBoundsFromColliders()
    {
        var colliders = GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            Debug.LogWarning("[RoomBounds] Không tìm thấy collider nào trong Room. Dùng giá trị mặc định.");
            RoomArea = new Bounds(center, size);
            return;
        }

        Bounds b = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
        {
            b.Encapsulate(colliders[i].bounds);
        }

        RoomArea = b;
    }

    /// <summary>
    /// Clamp vị trí vào bên trong phòng, cách tường ít nhất [margin] mét.
    /// Y được giữ trên sàn (min Y + 0.05).
    /// </summary>
    public Vector3 ClampInside(Vector3 pos)
    {
        Vector3 min = RoomArea.min + Vector3.one * margin;
        Vector3 max = RoomArea.max - Vector3.one * margin;

        pos.x = Mathf.Clamp(pos.x, min.x, max.x);
        pos.z = Mathf.Clamp(pos.z, min.z, max.z);

        // Y: giữ trên sàn, không vượt trần
        float floorY = RoomArea.min.y + 0.05f;
        float ceilY  = RoomArea.max.y - margin;
        pos.y = Mathf.Clamp(pos.y, floorY, ceilY);

        return pos;
    }

    /// <summary>
    /// Tạo vị trí ngẫu nhiên bên trong phòng (trên sàn).
    /// </summary>
    public Vector3 RandomPositionOnFloor()
    {
        Vector3 min = RoomArea.min + Vector3.one * margin;
        Vector3 max = RoomArea.max - Vector3.one * margin;

        return new Vector3(
            Random.Range(min.x, max.x),
            RoomArea.min.y + 0.05f,
            Random.Range(min.z, max.z)
        );
    }

    /// <summary>
    /// Kiểm tra vị trí có nằm trong phòng không.
    /// </summary>
    public bool IsInside(Vector3 pos)
    {
        return RoomArea.Contains(pos);
    }

    // ── Gizmos — vẽ viền phòng trong Scene view ──
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(RoomArea.center, RoomArea.size);

            // Vùng spawn (sau margin)
            Gizmos.color = Color.yellow;
            Vector3 spawnSize = RoomArea.size - Vector3.one * margin * 2f;
            Gizmos.DrawWireCube(RoomArea.center, spawnSize);
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center, size);
        }
    }
}