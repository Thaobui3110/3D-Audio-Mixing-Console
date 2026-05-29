using UnityEngine;

/// <summary>
/// Khởi chạy ChucK code khi game start.
/// Đặt lên cùng GameObject với ChuckSubInstance.
/// </summary>
public class ChunityRunCode : MonoBehaviour
{
    private ChuckSubInstance chuck;

    private void Start()
    {
        chuck = GetComponent<ChuckSubInstance>();
        RunMic();
    }

    // Mic passthrough — ChucK nhận audio input từ mic
    private void RunMic()
    {
        chuck.RunCode(@"adc => Gain g => dac; while( true ) 1::second => now;");
    }

    // Dope loop — giữ lại để test nhanh
    private void RunDope()
    {
        chuck.RunCode(@"SndBuf buffy => dac;
            ""special:dope"" => buffy.read;
            while( true ) { 0 => buffy.pos; 400::ms => now; }");
    }
}
