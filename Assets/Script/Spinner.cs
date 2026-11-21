using UnityEngine;
 
public class Spinner : MonoBehaviour
{
    public Vector3 axis = Vector3.forward;
    public float degreesPerSecond = 900f;
 
    void Awake() { enabled = false; }      // baru berputar setelah PC dinyalakan
    public void Run() { enabled = true; }
 
    void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
