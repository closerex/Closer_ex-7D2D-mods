using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

class InitialCollisionHandler : TrackedBehaviourBase
{
    Collider collider;
    protected override void Awake()
    {
        collider = GetComponent<Collider>();
        if (!collider)
            return;
        else
        {
            collider.isTrigger = true;
            gameObject.layer = 14;
        }

        if (isServer)
            DoCollision();
        syncOnInit = true;
        base.Awake();
    }

    public void DoCollision()
    {
        Collider[] others = null;
        if (collider is SphereCollider sphereCollider)
            others = Physics.OverlapSphere(transform.TransformPoint(sphereCollider.center), sphereCollider.radius);
        else if (collider is BoxCollider boxCollider)
            others = Physics.OverlapBox(transform.TransformPoint(boxCollider.center), boxCollider.size * 0.5f, transform.rotation);
        else if (collider is CapsuleCollider capsuleCollider)
        {
            float x = capsuleCollider.center.x, y = capsuleCollider.center.y, z = capsuleCollider.center.z;
            float x1, x2, y1, y2, z1, z2;
            float halfHeight = capsuleCollider.height * 0.5f;
            switch (capsuleCollider.direction)
            {
                case 0:
                    x1 = x - halfHeight;
                    x2 = x + halfHeight;
                    y1 = y2 = y;
                    z1 = z2 = y;
                    break;
                case 1:
                    x1 = x2 = x;
                    y1 = y - halfHeight;
                    y2 = y + halfHeight;
                    z1 = z2 = z;
                    break;
                case 2:
                    x1 = x2 = x;
                    y1 = y2 = y;
                    z1 = z - halfHeight;
                    z2 = z + halfHeight;
                    break;
                default:
                    x1 = x2 = x;
                    y1 = y2 = y;
                    z1 = z2 = z;
                    break;
            }
            others = Physics.OverlapCapsule(transform.TransformPoint(new Vector3(x1, y1, z1)), transform.TransformPoint(new Vector3(x2, y2, z2)), capsuleCollider.radius);
        }
        if (others != null && others.Length > 0)
            foreach (Collider other in others)
            {
                if (Physics.ComputePenetration(collider, transform.position, transform.rotation, other, other.transform.position, other.transform.rotation, out Vector3 dir, out float distance)) ;
                transform.position += dir * distance;
            }
        
    }

    protected override void OnExplosionInitServer(PooledBinaryWriter _bw)
    {
        StreamUtils.Write(_bw, transform.position + Origin.position);
    }

    protected override void OnExplosionInitClient(PooledBinaryReader _br)
    {
        transform.position = StreamUtils.ReadVector3(_br) - Origin.position;
    }
}

