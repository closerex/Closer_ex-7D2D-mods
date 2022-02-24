using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RFX4_RaycastCollision : MonoBehaviour
{
    public float RaycastDistance = 100;
    public GameObject[] Effects;
    public float Offset = 0;
    public float EnableTimeDelay = 0;

    public float DestroyTime = 3;
    public bool UsePivotPosition;
    public bool UseNormalRotation = true;
    public bool IsWorldSpace = true;
    public bool RealTimeUpdateRaycast;
    public bool DestroyAfterDisabling;
    [HideInInspector]
    public float HUE = -1;
    [HideInInspector]
    public List<GameObject> CollidedInstances = new List<GameObject>();

    const string particlesAdditionalName = "Distance";
    ParticleSystem[] distanceParticles;

    private bool canUpdate;

    public event EventHandler<RFX4_PhysicsMotion.RFX4_CollisionInfo> CollisionEnter;

    void Awake()
    {
        distanceParticles = transform.root.GetComponentsInChildren<ParticleSystem>();
    }

    // Use this for initialization
    void OnEnable()
    {
        CollidedInstances.Clear();
        if (EnableTimeDelay > 0.001) Invoke("UpdateRaycast", EnableTimeDelay);
        else UpdateRaycast();
    }

    void OnDisable()
    {
        if (DestroyAfterDisabling)
        {
            foreach (var instance in CollidedInstances)
            {
                Destroy(instance);
            }
        }
    }

    void Update()
    {
        if (canUpdate) {
            UpdateRaycast();
        }
    }


    private void UpdateRaycast()
    {
        RaycastHit raycastHit;
        if (Physics.Raycast(transform.position, transform.forward, out raycastHit, RaycastDistance)) {
            Vector3 position;
            if (UsePivotPosition)
                position = raycastHit.transform.position;
            else
                position = raycastHit.point + raycastHit.normal * Offset;

            var handler = CollisionEnter;
            if (handler != null)
                handler(this, new RFX4_PhysicsMotion.RFX4_CollisionInfo { HitPoint = raycastHit.point, HitCollider = raycastHit.collider, HitGameObject = raycastHit.transform.gameObject});

            if (distanceParticles !=null)
            foreach (var rayPS in distanceParticles)
            {

                    if (rayPS != null && rayPS.name.Contains(particlesAdditionalName))
                    rayPS.GetComponent<ParticleSystemRenderer>().lengthScale = (transform.position - raycastHit.point).magnitude / rayPS.main.startSize.constantMax;

            }

            if (CollidedInstances.Count==0)
                foreach (var effect in Effects) {
                    if (effect != null)
                    {
                        var instance = Instantiate(effect, position, new Quaternion()) as GameObject;
                        var effectSettings = instance.GetComponent<RFX4_EffectSettings>();
                        var effectSettingsRoot = GetComponentInParent<RFX4_EffectSettings>();
                        if (effectSettings != null && effectSettingsRoot != null)
                        {
                            //effectSettings.EffectQuality = effectSettingsRoot.EffectQuality;
                            // effectSettings.ForceInitialize();
                        }

                        CollidedInstances.Add(instance);

                        if (HUE > -0.9f) RFX4_ColorHelper.ChangeObjectColorByHUE(instance, HUE);
                    
                        if (!IsWorldSpace)
                            instance.transform.parent = transform;
                        if (UseNormalRotation)
                            instance.transform.LookAt(raycastHit.point + raycastHit.normal);
                        if (DestroyTime > 0.0001f)
                            Destroy(instance, DestroyTime);
                    }
                }
            else
                foreach (var instance in CollidedInstances) {
                    if (instance == null) continue;
                    instance.transform.position = position;
                    if (UseNormalRotation)
                        instance.transform.LookAt(raycastHit.point + raycastHit.normal);
                }
        }
        if (RealTimeUpdateRaycast)
            canUpdate = true;
    }


    void OnDrawGizmosSelected()
    {
       Gizmos.color = Color.blue;
       Gizmos.DrawLine(transform.position, transform.position + transform.forward * RaycastDistance);
    }
}
