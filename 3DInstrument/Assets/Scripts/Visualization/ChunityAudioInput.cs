using System;
using UnityEngine;

/// <summary>
/// Bắt audio từ Chunity/Unity, tính waveform và spectrum FFT.
///
/// Truy cập data:
///   ChunityAudioInput.the_waveform  — 1024 samples thời gian thực
///   ChunityAudioInput.the_spectrum  — 512 bins tần số (magnitude)
///
/// Đặt component này lên AudioManager hoặc bất kỳ GameObject
/// nằm trong audio chain (cùng path với ChucK output).
/// </summary>
public class ChunityAudioInput : MonoBehaviour
{
    public static int waveformSize = 1024;
    public static int spectrumSize = 512;

    public static float[]   the_waveform = new float[1024];
    public static float[]   the_spectrum = new float[512];

    private static readonly int   WAVEFORM_MAX = 1024;
    private static readonly int   WINDOW_SIZE  = 1024;

    private static float[]   waveformWindowed = new float[1024];
    private static Complex[] spectrumComplex  = new Complex[1024];
    private float[]          window;

    private void Awake()
    {
        window = Windowing.Hanning(WINDOW_SIZE);
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        int frames = data.Length / channels;
        waveformSize = Math.Min(WAVEFORM_MAX, frames);

        // Zero-pad nếu cần
        for (int i = waveformSize; i < WAVEFORM_MAX; i++)
            the_waveform[i] = 0f;

        // Copy channel 0
        for (int i = 0; i < waveformSize; i++)
        {
            the_waveform[i]    = data[i * channels];
            waveformWindowed[i] = the_waveform[i];
        }

        // Hanning window
        Windowing.Apply(waveformWindowed, window);

        // FFT
        Complex.Float2Complex(waveformWindowed, spectrumComplex);
        ChunityFFT.ComputeFFT(spectrumComplex, false, the_spectrum);
    }
}
