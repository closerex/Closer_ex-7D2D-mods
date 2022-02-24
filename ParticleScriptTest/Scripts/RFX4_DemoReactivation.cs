using UnityEngine;
using System.Collections;

public class RFX4_DemoReactivation : MonoBehaviour
{
    public float ReactivationTime = 5;
    public GameObject Effect;
	// Use this for initialization
	void Start () {
	    InvokeRepeating("Reactivate", 0, ReactivationTime);
	}
	
	// Update is called once per frame
	void Reactivate () {
        Effect.SetActive(false);
	    Effect.SetActive(true);
	}
}
