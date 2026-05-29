using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Contract cho bất kỳ object 3D nào phát âm thanh.
/// Implement interface này thay vì kế thừa trực tiếp SoundObjectBase khi cần.
/// </summary>
public interface ISoundObject : IInteractable
{
    string SoundID        { get; set; }
    string DisplayName    { get; }
    AudioClip Clip        { get; set; }
    float Volume          { get; set; }
    float Pitch           { get; set; }
    AudioMixerGroup MixerGroup { get; set; }
    bool IsPlaying        { get; }

    void TriggerSound(float velocity = 1f);
    void StopSound();
    void SetHighlight(bool active);
}
