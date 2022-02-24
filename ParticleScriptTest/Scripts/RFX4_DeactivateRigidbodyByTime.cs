using UnityEngine;
using System.Collections;

public class RFX4_DeactivateRigidbodyByTime : MonoBehaviour {

    public float TimeDelayToDeactivate = 6;

    void OnEnable()
    {
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.detectCollisions = true;
        Invoke("Deactivate", TimeDelayToDeactivate);
    }

    void Deactivate()
    {
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.detectCollisions = false;
	}
}
