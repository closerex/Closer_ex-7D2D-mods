using UnityEngine;

public class RFX4_PhysXSetImpulse : MonoBehaviour
{

    public float Force = 1;
    public ForceMode ForceMode = ForceMode.Force;

    private Rigidbody rig;
    private Transform t;
    // Use this for initialization
    void Start()
    {
        rig = GetComponent<Rigidbody>();
        t = transform;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (rig != null) rig.AddForce(t.forward * Force, ForceMode);
    }

    void OnDisable()
    {
        if (rig != null)
            rig.velocity = Vector3.zero;
    }
}
