using UnityEngine;
using System.Collections;

public class RFX4_CopyPosition : MonoBehaviour {

    public Transform CopiedTransform;

    private Transform t;
	// Use this for initialization
	void Start () {
        t = transform;
	}
	
	// Update is called once per frame
	void Update () {
        t.position = CopiedTransform.position;
	}
}
