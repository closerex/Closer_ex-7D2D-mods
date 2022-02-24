using UnityEngine;
using System.Collections;

public class RFX4_DeactivateByTime : MonoBehaviour {

    public GameObject DeactivatedGameObject;
    public float DeactivateTime = 3;

    private bool isActiveState;
    private float currentTime;
	
	void OnEnable ()
	{
        currentTime = 0;
        isActiveState = true;
	    //DeactivatedGameObject.SetActive(true);
    }

    private void Update()
    {
        currentTime += Time.deltaTime;
        if (isActiveState && currentTime >= DeactivateTime)
        {
            isActiveState = false;
            DeactivatedGameObject.SetActive(false);
           
        }

    }
}
