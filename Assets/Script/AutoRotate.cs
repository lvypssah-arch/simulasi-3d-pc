using UnityEngine;
 
public class AutoRotate : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float speed = 12f;      // derajat per detik
 
    void Update()
    {
        transform.Rotate(axis, speed * Time.unscaledDeltaTime, Space.World);
    }
}
