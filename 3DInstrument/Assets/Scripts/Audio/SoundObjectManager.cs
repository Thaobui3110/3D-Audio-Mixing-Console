using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Singleton quản lý toàn bộ ISoundObject trong scene.
/// Đặt component này lên GameObject AudioManager.
///
/// Chức năng:
///   - Auto-register mọi SoundObjectBase có trong scene khi Start
///   - TriggerByID: dùng bởi SongSequencer
///   - SpawnSoundButton: tạo object mới lúc runtime
///   - GetAllIDs: cung cấp danh sách cho UI
/// </summary>
public class SoundObjectManager : MonoBehaviour
{
    public static SoundObjectManager Instance { get; private set; }

    [Header("Mixer")]
    public AudioMixer     masterMixer;
    public AudioMixerGroup defaultMixerGroup;

    [Header("Runtime Spawn")]
    [Tooltip("Prefab chứa component SoundButton")]
    public GameObject soundButtonPrefab;
    [Tooltip("Parent Transform cho objects được spawn")]
    public Transform  spawnParent;

    private readonly Dictionary<string, ISoundObject> registry = new();

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Auto-register tất cả SoundObjectBase đang có trong scene
        foreach (var obj in FindObjectsOfType<SoundObjectBase>())
            Register(obj);

        Debug.Log($"[SoundObjectManager] Đã đăng ký {registry.Count} sound objects.");
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void Register(ISoundObject obj)
    {
        if (string.IsNullOrEmpty(obj.SoundID)) return;
        if (obj.MixerGroup == null) obj.MixerGroup = defaultMixerGroup;
        registry[obj.SoundID] = obj;
    }

    public void Unregister(ISoundObject obj)
    {
        if (!string.IsNullOrEmpty(obj.SoundID))
            registry.Remove(obj.SoundID);
    }

    /// <summary>Trigger sound theo ID — dùng bởi SongSequencer.</summary>
    public bool TriggerByID(string id, float velocity = 1f)
    {
        if (registry.TryGetValue(id, out var obj))
        {
            obj.TriggerSound(velocity);
            return true;
        }
        Debug.LogWarning($"[SoundObjectManager] SoundID '{id}' không tìm thấy.");
        return false;
    }

    /// <summary>Lấy toàn bộ ID đã đăng ký (dùng cho UI dropdown).</summary>
    public List<string> GetAllIDs() => new(registry.Keys);

    /// <summary>
    /// Spawn một SoundButton mới vào scene lúc runtime.
    /// soundID phải unique; clip có thể null (gán sau qua Inspector/SoundLibrary).
    /// </summary>
    public SoundObjectBase SpawnSoundButton(Vector3 worldPos, string soundID, AudioClip clip = null)
    {
        if (!soundButtonPrefab)
        {
            Debug.LogError("[SoundObjectManager] soundButtonPrefab chưa được gán!");
            return null;
        }

        var go  = Instantiate(soundButtonPrefab, worldPos, Quaternion.identity, spawnParent);
        var btn = go.GetComponent<SoundObjectBase>();
        if (!btn) { Destroy(go); return null; }

        btn.SoundID = soundID;
        go.name     = soundID;
        if (clip) btn.Clip = clip;

        Register(btn);
        return btn;
    }

    public void RemoveObject(string soundID)
    {
        if (registry.TryGetValue(soundID, out var obj))
        {
            Unregister(obj);
            if (obj is MonoBehaviour mb) Destroy(mb.gameObject);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Log Registry")]
    private void LogRegistry()
    {
        Debug.Log($"[SoundObjectManager] Registry ({registry.Count} objects):");
        foreach (var kv in registry) Debug.Log($"  • {kv.Key}");
    }
#endif
}
