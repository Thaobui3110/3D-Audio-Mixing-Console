using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Load AudioClip từ file trên disk lúc runtime (WAV / MP3 / OGG).
///
/// HIERARCHY:
///   Systems → AudioManager → AudioFileLoader (component này)
///
/// KHÔNG cần StandaloneFileBrowser — dùng path string trực tiếp.
/// UI sẽ gọi LoadFile(path) sau khi user cung cấp đường dẫn
/// (qua InputField hoặc NativeFilePicker / StandaloneFileBrowser nếu muốn mở dialog).
///
/// Events:
///   OnClipLoaded(AudioClip)   — clip đã load xong
///   OnLoadError(string)       — lỗi load
///   OnLoadProgress(float)     — tiến trình 0-1
/// </summary>
public class AudioFileLoader : MonoBehaviour
{
    // ── Events ─────────────────────────────────────────────────────────────
    public event Action<AudioClip, string> OnClipLoaded;
    public event Action<string>            OnLoadError;
    public event Action<float>             OnLoadProgress;

    // ── State ──────────────────────────────────────────────────────────────
    public  bool   IsLoading  { get; private set; }
    public  string LastPath   { get; private set; }

    private static readonly string[] SupportedExtensions = { ".wav", ".mp3", ".ogg" };

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Load file từ đường dẫn tuyệt đối.</summary>
    public void LoadFile(string absolutePath)
    {
        if (IsLoading)
        {
            Debug.LogWarning("[AudioFileLoader] Đang load file khác — bỏ qua yêu cầu mới.");
            return;
        }

        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            Notify($"File không tồn tại: {absolutePath}");
            return;
        }

        string ext = Path.GetExtension(absolutePath).ToLowerInvariant();
        bool   supported = Array.Exists(SupportedExtensions, e => e == ext);
        if (!supported)
        {
            Notify($"Định dạng không hỗ trợ: {ext}. Cần .wav / .mp3 / .ogg");
            return;
        }

        LastPath = absolutePath;
        StartCoroutine(LoadRoutine(absolutePath));
    }

    // ── Coroutine ──────────────────────────────────────────────────────────
    private IEnumerator LoadRoutine(string path)
    {
        IsLoading = true;
        OnLoadProgress?.Invoke(0f);

        string ext      = Path.GetExtension(path).ToLowerInvariant();
        var    audioType = ExtToAudioType(ext);
        string url       = "file://" + path.Replace("\\", "/");

        using var req = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
        var op = req.SendWebRequest();

        while (!op.isDone)
        {
            OnLoadProgress?.Invoke(op.progress);
            yield return null;
        }

        IsLoading = false;

        if (req.result != UnityWebRequest.Result.Success)
        {
            Notify($"Lỗi load: {req.error}");
            yield break;
        }

        var clip = DownloadHandlerAudioClip.GetContent(req);
        if (clip == null)
        {
            Notify("Load xong nhưng clip null — file có thể bị lỗi.");
            yield break;
        }

        clip.name = Path.GetFileNameWithoutExtension(path);
        OnLoadProgress?.Invoke(1f);
        OnClipLoaded?.Invoke(clip, clip.name);
        Debug.Log($"[AudioFileLoader] Loaded: {clip.name} ({clip.length:F1}s, {clip.channels}ch)");
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static AudioType ExtToAudioType(string ext) => ext switch
    {
        ".wav" => AudioType.WAV,
        ".mp3" => AudioType.MPEG,
        ".ogg" => AudioType.OGGVORBIS,
        _      => AudioType.UNKNOWN,
    };

    private void Notify(string msg)
    {
        Debug.LogWarning($"[AudioFileLoader] {msg}");
        OnLoadError?.Invoke(msg);
    }
}
