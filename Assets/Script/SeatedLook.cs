using System.Collections;
using UnityEngine;
 
public class SeatedLook : MonoBehaviour
{
    public static SeatedLook I;
 
    public Transform cam;                       // Main Camera (anak Player)
    public float sensitivity = 2f;
    public float yawLimit = 55f;                // batas toleh kiri/kanan (derajat)
    public Vector2 pitchLimit = new Vector2(-5f, 55f);   // plus = menunduk
    public float startPitch = 22f;
    public float startYaw = 0f;
    float yaw, pitch;
    Vector3 basePos, shakeOffset;
    public bool useTransformAsStart = false;   // centang jika ingin memakai rotasi kamera dari editor

void Awake()
{
    I = this;
    if (useTransformAsStart)
    {
        Vector3 e = cam.localEulerAngles;
        startPitch = Mathf.DeltaAngle(0f, e.x);
        startYaw = Mathf.DeltaAngle(0f, e.y);
    }
    yaw = startYaw;
    pitch = startPitch;
    basePos = cam.localPosition;
}
  
 
    void Update()
    {
        if (Time.timeScale == 0f) return;
 
        if (Input.touchCount == 2)               // HP: geser dua jari
        {
            Vector2 d = (Input.GetTouch(0).deltaPosition + Input.GetTouch(1).deltaPosition) * 0.5f;
            yaw += d.x * 0.08f;
            pitch -= d.y * 0.08f;
        }
        else if (Input.GetMouseButton(1))        // laptop: tahan klik kanan
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        }
        yaw += Input.GetAxisRaw("Horizontal") * 50f * Time.deltaTime;   // A D / panah
        pitch -= Input.GetAxisRaw("Vertical") * 35f * Time.deltaTime;   // W S / panah
 
        yaw = Mathf.Clamp(yaw, startYaw - yawLimit, startYaw + yawLimit);
        pitch = Mathf.Clamp(pitch, pitchLimit.x, pitchLimit.y);
        cam.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        cam.localPosition = basePos + shakeOffset;
    }
 
    public void Shake(float duration = 0.25f, float magnitude = 0.012f)
    {
        StopAllCoroutines();
        StartCoroutine(ShakeRoutine(duration, magnitude));
    }
 
    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            shakeOffset = Random.insideUnitSphere * magnitude * (1f - t / duration);
            yield return null;
        }
        shakeOffset = Vector3.zero;
    }
}
