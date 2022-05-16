using UnityEngine;

class InitialCollisionHandler : MonoBehaviour
{
    Collider collider;
    void Awake()
    {
        collider = GetComponent<Collider>();
        if (!collider)
            return;
        else
        {
            collider.isTrigger = true;
            gameObject.layer = 14;
        }

        if (!GameManager.IsDedicatedServer)
            DoCollision();
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
        {
            Vector3 final = Vector3.zero;
            foreach (Collider other in others)
            {
                if (Physics.ComputePenetration(collider, transform.position, transform.rotation, other, other.transform.position, other.transform.rotation, out Vector3 dir, out float distance))
                    final += dir * distance;
            }
            transform.position += final;
        }
    }
}

