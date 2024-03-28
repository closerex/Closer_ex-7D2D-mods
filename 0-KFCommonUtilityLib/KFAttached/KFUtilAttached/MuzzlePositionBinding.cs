using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[AddComponentMenu("Muzzle Position Binding")]
public class MuzzlePositionBinding : MonoBehaviour
{
    [SerializeField]
    private Transform muzzleTrans;
    [SerializeField]
    private Vector3 newMuzzlePosition;
    private Vector3 initialPosition;
    private void Awake()
    {
        if (muzzleTrans)
            initialPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        if (muzzleTrans)
            muzzleTrans.localPosition = newMuzzlePosition;
    }

    private void OnDisable()
    {
        if (muzzleTrans)
            muzzleTrans.localPosition = initialPosition;
    }
}
