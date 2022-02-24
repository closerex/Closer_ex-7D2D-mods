using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class RFX4_PhysicsMotion : MonoBehaviour
{
    public bool UseCollisionDetect = true;
    public float MaxDistnace = -1;
    public float Mass = 1;
    public float Speed = 10;
    public float RandomSpeedOffset = 0f;
    public float AirDrag = 0.1f;
    public bool UseGravity = true;
    public ForceMode ForceMode = ForceMode.Impulse;
    public Vector3 AddRealtimeForce = Vector3.zero;
    public float MinSpeed = 0;
    public float ColliderRadius = 0.05f;
    public bool FreezeRotation;

    public bool UseTargetPositionAfterCollision;
    public GameObject EffectOnCollision;
    public bool CollisionEffectInWorldSpace = true;
    public bool LookAtNormal = true;
    public float CollisionEffectDestroyAfter = 5;

    public GameObject[] DeactivateObjectsAfterCollision;

    [HideInInspector] public float HUE = -1;

    public event EventHandler<RFX4_CollisionInfo> CollisionEnter;

    Rigidbody rigid;
    SphereCollider collid;
    ContactPoint lastContactPoint;
    Collider lastCollider;
    Vector3 offsetColliderPoint;
    bool isCollided;
    GameObject targetAnchor;
    bool isInitializedForce;
    float currentSpeedOffset;
    private RFX4_EffectSettings effectSettings;

    void OnEnable ()
    {
        effectSettings = GetComponentInParent<RFX4_EffectSettings>();
        foreach (var obj in DeactivateObjectsAfterCollision)
        {
            if (obj != null)
            {
                if(obj.GetComponent<ParticleSystem>() != null) obj.SetActive(false);
                obj.SetActive(true);
            }
        }
        currentSpeedOffset = Random.Range(-RandomSpeedOffset * 10000f, RandomSpeedOffset * 10000f) / 10000f;
	    InitializeRigid();
    }

    void InitializeRigid()
    {
        if (effectSettings.UseCollisionDetection)
        {
            collid = gameObject.AddComponent<SphereCollider>();
            collid.radius = ColliderRadius;
        }

        isInitializedForce = false;
        
       
    }

    void InitializeForce()
    {
        rigid = gameObject.AddComponent<Rigidbody>();
        rigid.mass = effectSettings.Mass;
        rigid.drag = effectSettings.AirDrag;
        rigid.useGravity = effectSettings.UseGravity;
        if (FreezeRotation) rigid.constraints = RigidbodyConstraints.FreezeRotation;
        rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigid.interpolation = RigidbodyInterpolation.Interpolate;
        rigid.AddForce(transform.forward * (effectSettings.Speed + currentSpeedOffset), ForceMode);
        isInitializedForce = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isCollided && !effectSettings.UseCollisionDetection) return;
        foreach (ContactPoint contact in collision.contacts)
        {
            if (!isCollided)
            {
                isCollided = true;
                //offsetColliderPoint = contact.otherCollider.transform.position - contact.point;
                // lastCollider = contact.otherCollider;
                // lastContactPoint = contact;
                if (UseTargetPositionAfterCollision)
                {
                    if (targetAnchor != null) Destroy(targetAnchor);

                    targetAnchor = new GameObject();
                    targetAnchor.hideFlags = HideFlags.HideAndDontSave;
                    targetAnchor.transform.parent = contact.otherCollider.transform;
                    targetAnchor.transform.position = contact.point;
                    targetAnchor.transform.rotation = transform.rotation;
                    //targetAnchor.transform.LookAt(contact.normal);
                }
                
            }
            var handler = CollisionEnter;
            if (handler != null)
                handler(this, new RFX4_CollisionInfo { HitPoint = contact.point, HitCollider = contact.otherCollider, HitGameObject = contact.otherCollider.gameObject});

            if (EffectOnCollision != null)
            {
                var instance = Instantiate(EffectOnCollision, contact.point, new Quaternion()) as GameObject;

                if (HUE > -0.9f) RFX4_ColorHelper.ChangeObjectColorByHUE(instance, HUE);
                
                if (LookAtNormal) instance.transform.LookAt(contact.point + contact.normal);
                else instance.transform.rotation = transform.rotation;
                if (!CollisionEffectInWorldSpace) instance.transform.parent = contact.otherCollider.transform.parent;
                Destroy(instance, CollisionEffectDestroyAfter);
            }
        }

        foreach (var obj in DeactivateObjectsAfterCollision)
        {
            if (obj != null)
            {
                var ps = obj.GetComponent<ParticleSystem>();
                if (ps != null) ps.Stop();
                else obj.SetActive(false);
            }
        }


        if (rigid != null) Destroy(rigid);
        if (collid != null) Destroy(collid);
    }



    private void FixedUpdate()
    {
        if (!isInitializedForce) InitializeForce();
        if (rigid != null && AddRealtimeForce.magnitude > 0.001f) rigid.AddForce(AddRealtimeForce);
        if (rigid != null && MinSpeed > 0.001f) rigid.AddForce(transform.forward * MinSpeed);
        if (rigid != null && effectSettings.MaxDistnace > 0 && transform.localPosition.magnitude > effectSettings.MaxDistnace) RemoveRigidbody();
        
        if (UseTargetPositionAfterCollision && isCollided && targetAnchor != null)
        {
            transform.position = targetAnchor.transform.position;
            transform.rotation = targetAnchor.transform.rotation;
        }
    }

    public class RFX4_CollisionInfo : EventArgs
    {
        //public ContactPoint ContactPoint;
        public Vector3 HitPoint;
        public Collider HitCollider;
        public GameObject HitGameObject;
    }

    //private void Update()
    //{
    //    var kinetic = rigid.mass* Mathf.Pow(rigid.velocity.magnitude, 2) * 0.5f;
    //    Debug.Log(transform.localPosition.magnitude + "   time" + (Time.time - startTime) + "  speed" + (transform.localPosition.magnitude/ (Time.time - startTime)));
    //}

    private void OnDisable()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = new Quaternion();
        RemoveRigidbody();
    }

    void RemoveRigidbody()
    {
        isCollided = false;
        if (rigid != null) Destroy(rigid);
        if (collid != null) Destroy(collid);
    }

    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
            return;

        var t = transform;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(t.position, ColliderRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(t.position, t.position + t.forward * 100);
    }
}
