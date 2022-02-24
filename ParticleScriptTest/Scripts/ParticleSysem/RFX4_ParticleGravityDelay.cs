using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RFX4_ParticleGravityDelay : MonoBehaviour
{
   
    public AnimationCurve GravityByTime = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float TimeMultiplier = 3;
    [Space]
    public float GravityMultiplierMin = 1;
    public float GravityMultiplierMax = 1;


    private ParticleSystem.MainModule main;

    private float startTime = 0;
    private float startMinGrav;
    private float startMaxGrav;

	// Use this for initialization
	void Awake ()
	{
	    main = GetComponent<ParticleSystem>().main;
	    startMinGrav = main.gravityModifier.constantMin;
	    startMaxGrav = main.gravityModifier.constantMax;
	}

    void OnEnable()
    {
        startTime = Time.time;

        var grav = main.gravityModifier;
        grav.constantMin = startMinGrav;
        grav.constantMax = startMaxGrav;
        main.gravityModifier = grav;
    }
	
	// Update is called once per frame
	void Update ()
	{
	    var timeDelta = Time.time - startTime;

        if (timeDelta < TimeMultiplier)
	    {
	        var gravModifier = main.gravityModifier;
	        var currentGravity = GravityByTime.Evaluate(timeDelta / TimeMultiplier);
	        gravModifier.constantMin = currentGravity * GravityMultiplierMin;
	        gravModifier.constantMax = currentGravity * GravityMultiplierMax;
            main.gravityModifier = gravModifier;
	    }
	}
}