// Assets/Scripts/Debug/SpatialAudioDebug.cs
// Gắn lên Main Camera — chẩn đoán lỗi đảo ngược stereo

using UnityEngine;

public class SpatialAudioDebug : MonoBehaviour
{
    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F5)) return;

        var listener = GetComponent<AudioListener>();
        Debug.Log($"=== SPATIAL AUDIO DEBUG ===");
        Debug.Log($"AudioListener on: {gameObject.name}");
        Debug.Log($"Listener world RIGHT: {transform.right}");
        Debug.Log($"Listener world FORWARD: {transform.forward}");
        Debug.Log($"Listener world pos: {transform.position}");
        Debug.Log($"Listener lossyScale: {transform.lossyScale}");

        // Kiểm tra tất cả AudioListener trong scene
        var allListeners = FindObjectsOfType<AudioListener>();
        Debug.Log($"Số AudioListener trong scene: {allListeners.Length}");
        foreach (var al in allListeners)
            Debug.Log($"  → {al.gameObject.name} (active: {al.enabled})");

        // Kiểm tra tất cả AudioSource có spatialBlend
        foreach (var src in FindObjectsOfType<AudioSource>())
        {
            if (src.spatialBlend < 0.5f) continue;
            Vector3 dir = src.transform.position - transform.position;
            float dot = Vector3.Dot(transform.right, dir.normalized);
            // dot > 0 = bên PHẢI, dot < 0 = bên TRÁI
            Debug.Log($"Speaker [{src.gameObject.name}]: " +
                      $"panStereo={src.panStereo:F2}, " +
                      $"spatialBlend={src.spatialBlend:F1}, " +
                      $"dotRight={dot:F2} ({(dot > 0 ? "RIGHT" : "LEFT")}), " +
                      $"spread={src.spread}");
        }
    }
}