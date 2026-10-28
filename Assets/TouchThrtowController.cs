using UnityEngine;

public class TouchThrowController : MonoBehaviour
{
    [Header("Throw Settings")]
    public Rigidbody projectile;
    public float throwPower = 0.02f;
    public float upwardArcMultiplier = 0.5f;

    private Vector2 startTouchPos;
    private Vector2 endTouchPos;

    void Update()
    {
        // Detects both mouse clicks in the Editor and screen touches on Android
        if (Input.GetMouseButtonDown(0))
        {
            startTouchPos = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            endTouchPos = Input.mousePosition;
            ExecuteThrow();
        }
    }

    void ExecuteThrow()
    {
        if (projectile == null) return;

        Vector2 swipeVector = endTouchPos - startTouchPos;

        // Maps the 2D screen swipe to a 3D isometric world force
        Vector3 throwForce = new Vector3(
            swipeVector.x * throwPower, 
            swipeVector.magnitude * throwPower * upwardArcMultiplier, 
            swipeVector.y * throwPower
        );

        projectile.isKinematic = false;
        projectile.AddForce(throwForce, ForceMode.Impulse);
        
        projectile = null; // Prevents throwing the same object twice
    }
}