using UnityEngine;
using System.Collections;

public class RFX4_CollisionPropertyDeactiavtion : MonoBehaviour
{

    public float DeactivateTimeDelay = 1;

    private float startTime;
    private WindZone windZone;
    ParticleSystem ps;
    ParticleSystem.CollisionModule collisionModule;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        collisionModule = ps.collision;
    }

    private void OnEnable()
    {
        startTime = Time.time;
        collisionModule.enabled = true;
    }

    private void Update()
    {
        var time = Time.time - startTime;
       
        if (time >= DeactivateTimeDelay)
        {
            collisionModule.enabled = false;
        }
    }
}
