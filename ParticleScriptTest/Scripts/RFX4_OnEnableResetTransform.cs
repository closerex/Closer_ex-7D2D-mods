using UnityEngine;
using System.Collections;

public class RFX4_OnEnableResetTransform : MonoBehaviour {

    Transform t;
    Vector3 startPosition;
    Quaternion startRotation;
    Vector3 startScale;
    bool isInitialized;

	void OnEnable () {
	    if(!isInitialized)
        {
            isInitialized = true;
            t = transform;
            startPosition = t.position;
            startRotation = t.rotation;
            startScale = t.localScale;
        }
        else
        {
            t.position = startPosition;
            t.rotation = startRotation;
            t.localScale = startScale;
        }
	}

    void OnDisable()
    {
        if (!isInitialized)
        {
            isInitialized = true;
            t = transform;
            startPosition = t.position;
            startRotation = t.rotation;
            startScale = t.localScale;
        }
        else
        {
            t.position = startPosition;
            t.rotation = startRotation;
            t.localScale = startScale;
        }
    }
}
