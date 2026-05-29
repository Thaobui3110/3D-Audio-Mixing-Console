using UnityEngine;

/// <summary>
/// Placeholder SoundObject mặc định dùng thay thế Piano.
///
/// Setup trong Unity:
///   1. Tạo GameObject (Cube, Sphere, hoặc mesh bất kỳ)
///   2. Add component SoundButton
///   3. Gán AudioClip trong Inspector hoặc để SoundObjectManager điền tự động
///   4. Đảm bảo GameObject nằm trong Layer "Instrument"
///
/// Để đổi mesh: chỉ thay MeshFilter/MeshRenderer — không cần sửa code.
/// </summary>
public class SoundButton : SoundObjectBase
{
    [Header("Button Press")]
    [SerializeField] private float pressDepth = 0.06f;   // units nhấn xuống
    [SerializeField] private float pressSpeed = 14f;

    private Vector3 restPosition;
    private bool    pressing;
    private float   pressT;

    protected override void Awake()
    {
        base.Awake();
        restPosition = transform.localPosition;
    }

    protected override void Update()
    {
        base.Update();

        if (pressing)
        {
            pressT += Time.deltaTime * pressSpeed;
            float t = Mathf.Sin(pressT * Mathf.PI);   // ping-pong 0→1→0
            transform.localPosition = Vector3.Lerp(
                restPosition,
                restPosition + Vector3.down * pressDepth,
                t
            );
            if (pressT >= 1f)
            {
                transform.localPosition = restPosition;
                pressing = false;
            }
        }
    }

    protected override void OnSoundTriggered(float velocity)
    {
        pressing = true;
        pressT   = 0f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
#endif
}
