using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserBeamOnTarget : MonoBehaviour
{
    [SerializeField]
    private LineRenderer line;
    [SerializeField]
    private Transform shootPoint;
    [SerializeField]
    private float maxDistance;
    [SerializeField]
    private List<Collider> ignored;

    private void Update()
    {
        var capsule = shootPoint.GetComponentInChildren<CapsuleCollider>();
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hitInfo, capsule ? capsule.transform.localScale.y * 2 : maxDistance, -538750997) && !ignored.Contains(hitInfo.collider))
            line.SetPosition(1, new Vector3(0, 0, hitInfo.distance));
        else if (capsule != null)
            line.SetPosition(1, new Vector3(0, 0, capsule.transform.localScale.y * 2));
        else
            line.SetPosition(1, new Vector3(0, 0, maxDistance));
    }
}
