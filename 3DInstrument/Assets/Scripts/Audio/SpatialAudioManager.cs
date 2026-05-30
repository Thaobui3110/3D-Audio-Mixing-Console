using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Quản lý toàn bộ SpatialSoundObject trong scene.
/// Xử lý: spawn, remove, play/stop, load clip và assign.
///
/// HIERARCHY:
///   Systems → AudioManager → SpatialAudioManager (component này)
///
/// SETUP:
///   - Kéo Speaker prefab (có SpatialSoundObject) vào speakerPrefab
///   - Kéo AudioMixerGroup vào defaultMixerGroup
///   - Kéo AudioWorld transform vào audioWorldParent
/// </summary>
[DisallowMultipleComponent]
public class SpatialAudioManager : MonoBehaviour
{
    public static SpatialAudioManager Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Prefab & World")]
    [SerializeField] private GameObject    speakerPrefab;
    [SerializeField] private Transform     audioWorldParent;

    [Header("Mixer")]
    [SerializeField] private AudioMixer      masterMixer;
    [SerializeField] private AudioMixerGroup defaultMixerGroup;

    // ── State ──────────────────────────────────────────────────────────────
    private readonly Dictionary<string, SpatialSoundObject> sources = new();
    private          AudioFileLoader                         loader;
    private          string                                  pendingAssignID;
    private          int                                     spawnCounter;

    // ── Events ─────────────────────────────────────────────────────────────
    public event System.Action<SpatialSoundObject> OnSourceSpawned;
    public event System.Action<string>             OnSourceRemoved;
    public event System.Action<string>             OnClipAssigned;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        loader = GetComponent<AudioFileLoader>();
        if (loader == null)
            loader = gameObject.AddComponent<AudioFileLoader>();

        loader.OnClipLoaded += HandleClipLoaded;
        loader.OnLoadError  += msg => Debug.LogWarning($"[SpatialAudioManager] Load error: {msg}");
    }

    private void Start()
    {
        RegisterExistingSpeakers();
    }

    /// <summary>Tìm và đăng ký tất cả SpatialSoundObject đã có sẵn trong AudioWorld.</summary>
    private void RegisterExistingSpeakers()
    {
        if (audioWorldParent == null) return;

        foreach (var obj in audioWorldParent.GetComponentsInChildren<SpatialSoundObject>())
        {
            string id = obj.SourceID;
            if (string.IsNullOrEmpty(id)) continue;
            if (sources.ContainsKey(id))  continue;

            sources[id] = obj;

            // Cập nhật spawnCounter để tránh trùng ID khi spawn mới
            if (id.StartsWith("Speaker_") && int.TryParse(id.Substring(8), out int num))
                spawnCounter = Mathf.Max(spawnCounter, num);
        }

        if (sources.Count > 0)
            Debug.Log($"[SpatialAudioManager] Đã đăng ký {sources.Count} speaker có sẵn.");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (loader != null)
        {
            loader.OnClipLoaded -= HandleClipLoaded;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Spawn speaker mới tại vị trí ngẫu nhiên trong AudioWorld.</summary>
    public SpatialSoundObject SpawnSpeaker(Vector3? position = null)
    {
        if (!speakerPrefab)
        {
            Debug.LogError("[SpatialAudioManager] speakerPrefab chưa gán!", this);
            return null;
        }

        spawnCounter++;
        string id  = $"Speaker_{spawnCounter:D2}";
        Vector3 pos = position ?? RandomSpawnPosition();

        var go  = Instantiate(speakerPrefab, pos, Quaternion.identity, audioWorldParent);
        var obj = go.GetComponent<SpatialSoundObject>();
        if (obj == null) { Destroy(go); return null; }

        go.name = id;
        obj.SetSourceID(id);
        obj.SetMixerGroup(defaultMixerGroup);

        sources[id] = obj;
        OnSourceSpawned?.Invoke(obj);
        Debug.Log($"[SpatialAudioManager] Spawned {id} at {pos}");
        return obj;
    }

    /// <summary>Load file và assign cho speaker đã chỉ định.</summary>
    public void LoadAndAssign(string filePath, string speakerID)
    {
        if (!sources.ContainsKey(speakerID))
        {
            Debug.LogWarning($"[SpatialAudioManager] SpeakerID '{speakerID}' không tìm thấy.");
            return;
        }
        pendingAssignID = speakerID;
        loader.LoadFile(filePath);
    }

    /// <summary>Load file và spawn speaker mới tự động.</summary>
    public void LoadAndSpawn(string filePath)
    {
        var obj = SpawnSpeaker();
        if (obj == null) return;
        pendingAssignID = obj.SourceID;
        loader.LoadFile(filePath);
    }

    public void PlayAll()  => ForEach(s => s.Play());
    public void StopAll()  => ForEach(s => s.Stop());
    public void PauseAll() => ForEach(s => s.Pause());

    public void Play(string id)  => Get(id)?.Play();
    public void Stop(string id)  => Get(id)?.Stop();
    public void Pause(string id) => Get(id)?.Pause();

    public void RemoveSource(string id)
    {
        if (!sources.TryGetValue(id, out var obj)) return;
        sources.Remove(id);
        if (obj != null) Destroy(obj.gameObject);
        OnSourceRemoved?.Invoke(id);
    }

    public IReadOnlyDictionary<string, SpatialSoundObject> GetAll() => sources;

    public SpatialSoundObject Get(string id)
        => sources.TryGetValue(id, out var obj) ? obj : null;

    public IReadOnlyList<string> GetAllIDs()
    {
        var list = new List<string>(sources.Keys);
        list.Sort();
        return list;
    }

    // ── Internal ───────────────────────────────────────────────────────────
    private void HandleClipLoaded(AudioClip clip, string clipName)
    {
        if (string.IsNullOrEmpty(pendingAssignID)) return;

        if (sources.TryGetValue(pendingAssignID, out var obj))
        {
            obj.SetClip(clip);
            obj.Play();
            OnClipAssigned?.Invoke(pendingAssignID);
            Debug.Log($"[SpatialAudioManager] Clip '{clipName}' → {pendingAssignID}");
        }

        pendingAssignID = null;
    }

    private static Vector3 RandomSpawnPosition()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist  = Random.Range(3f, 8f);
        return new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
    }

    private void ForEach(System.Action<SpatialSoundObject> action)
    {
        foreach (var s in sources.Values)
            if (s != null) action(s);
    }
}