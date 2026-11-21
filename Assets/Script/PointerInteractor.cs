using UnityEngine;
 
public class PointerInteractor : MonoBehaviour
{
    public Camera cam;
    public LayerMask clickableMask;
    public float maxDistance = 5f;
 
    Clickable3D current;
 
    void Update()
    {
        if (Time.timeScale == 0f) return;
 
        bool blocked = Util.PointerOverUI() ||
                       (AssemblyManager.I != null && AssemblyManager.I.IsHolding);
 
        Clickable3D hit = null;
        if (!blocked)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit h;
            if (Physics.Raycast(ray, out h, maxDistance, clickableMask,
                                QueryTriggerInteraction.Collide))
                hit = h.collider.GetComponentInParent<Clickable3D>();
        }
 
        if (hit != current)
        {
            if (current != null) current.SetHover(false);
            current = hit;
            if (current != null) current.SetHover(true);
        }
 
        if (current != null && Input.GetMouseButtonDown(0)) current.Press();
    }
}
