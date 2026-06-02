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
        DarkenFloor();
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

    /// <summary>Tự động tối hóa Floor nếu tìm thấy trong scene.</summary>
    private void DarkenFloor()
    {
        var floor = GameObject.Find("Floor");
        if (floor == null) return;
        var renderer = floor.GetComponent<Renderer>();
        if (renderer == null) return;

        // Tạo material mới để không ảnh hưởng shared material
        var mat = new Material(renderer.sharedMaterial);
        mat.color = new Color(0.04f, 0.04f, 0.06f);  // gần đen, hơi xanh tối
        renderer.material = mat;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Spawn speaker mới. Nếu chưa gán prefab → tự tạo Cube runtime.</summary>
    public SpatialSoundObject SpawnSpeaker(Vector3? position = null)
    {
        spawnCounter++;
        string  id  = $"Speaker_{spawnCounter:D2}";
        Vector3 pos = position ?? SpawnInFrontOfPlayer();

        GameObject go;

        if (speakerPrefab != null)
        {
            go = Instantiate(speakerPrefab, pos, Quaternion.identity, audioWorldParent);
        }
        else
        {
            // Fallback: tạo Cube runtime làm speaker tạm thời
            go = CreateRuntimeSpeaker(pos);
        }

        var obj = go.GetComponent<SpatialSoundObject>();
        if (obj == null) obj = go.AddComponent<SpatialSoundObject>();

        go.name = id;
        obj.SetSourceID(id);
        obj.SetMixerGroup(defaultMixerGroup);

        // Đảm bảo có AudioReactiveObject để cube phản ứng với nhạc
        if (go.GetComponent<AudioReactiveObject>() == null)
            go.AddComponent<AudioReactiveObject>();

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

    /// <summary>Vị trí trước mặt player, cách 4-6m, lệch ngang nhẹ để không chồng.</summary>
    private Vector3 SpawnInFrontOfPlayer()
    {
        var cam = Camera.main;
        if (cam == null)
            return FallbackSpawnPosition();
 
        // ── Tính vị trí trước mặt camera ──
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
 
        Vector3 right = cam.transform.right;
        right.y = 0f;
        right.Normalize();
 
        float dist       = Random.Range(3f, 5f);
        float sideOffset = Random.Range(-1.2f, 1.2f);
 
        Vector3 pos = cam.transform.position + fwd * dist + right * sideOffset;
        pos.y = 0f;
 
        // ── Clamp vào bên trong phòng ──
        if (RoomBounds.Instance != null)
        {
            pos = RoomBounds.Instance.ClampInside(pos);
 
            // Nếu sau khi clamp, vị trí quá gần player (< 1.5m)
            // → player đang nhìn ra tường → spawn random trong phòng
            float distToPlayer = Vector3.Distance(
                new Vector3(cam.transform.position.x, 0, cam.transform.position.z),
                new Vector3(pos.x, 0, pos.z)
            );
 
            if (distToPlayer < 1.5f)
            {
                pos = RoomBounds.Instance.RandomPositionOnFloor();
                Debug.Log("[SpatialAudioManager] Player nhìn ra tường → spawn random trong phòng");
            }
        }
 
        return pos;
    }
 
    /// <summary>Fallback khi không có camera — spawn trong phòng nếu có RoomBounds.</summary>
    private Vector3 FallbackSpawnPosition()
    {
        if (RoomBounds.Instance != null)
            return RoomBounds.Instance.RandomPositionOnFloor();
 
        // Không có RoomBounds → spawn quanh origin
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float r     = Random.Range(3f, 6f);
        return new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
    }

    // ── Màu sắc cho Cube runtime — mỗi speaker một màu khác nhau ────────
    private static readonly Color[] SpeakerColors =
    {
        new Color(0.2f, 0.6f, 1.0f),   // xanh dương
        new Color(1.0f, 0.4f, 0.2f),   // cam đỏ
        new Color(0.3f, 1.0f, 0.4f),   // xanh lá
        new Color(1.0f, 0.85f, 0.1f),  // vàng
        new Color(0.8f, 0.3f, 1.0f),   // tím
        new Color(0.1f, 0.9f, 0.9f),   // cyan
    };

    /// <summary>
    /// Tạo Cube runtime làm speaker khi chưa gán prefab.
    /// Cube 0.6m, nổi 0.5m trên sàn, material URP/Lit sáng + Emission.
    /// </summary>
    private GameObject CreateRuntimeSpeaker(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(audioWorldParent, false);
        go.transform.position   = pos + Vector3.up * 1.2f;  // lơ lửng — AudioReactiveObject sẽ bob
        go.transform.localScale = Vector3.one * 0.6f;

        // Material — tạo mới, màu sáng + emission
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard"));
            Color col = SpeakerColors[spawnCounter % SpeakerColors.Length];
            mat.color = col;

            // Bật emission để cube phát sáng — dễ nhìn kể cả trong bóng tối
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", col * 0.5f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            renderer.material = mat;
        }

        // Collider mặc định từ CreatePrimitive là BoxCollider — đổi sang SphereCollider cho SpatialSoundObject
        var box = go.GetComponent<BoxCollider>();
        if (box != null) Object.Destroy(box);

        Debug.Log($"[SpatialAudioManager] Tạo Cube speaker runtime tại {pos}");
        return go;
    }

    private void ForEach(System.Action<SpatialSoundObject> action)
    {
        foreach (var s in sources.Values)
            if (s != null) action(s);
    }
}