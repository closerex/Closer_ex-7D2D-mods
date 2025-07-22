using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("KFAttachments/Utils/Camera Animation Events")]
[DefaultExecutionOrder(0)]
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class CameraAnimationEvents : MonoBehaviour, IPlayableGraphRelated
{
    [Serializable]
    public enum CurveType
    {
        [InspectorName(null)]
        Position,
        EularAngleRaw,
        EularAngleBaked,
        Quaternion
    }

    public class CameraCurveData
    {
        AnimationCurve[] curves;
        float[] values, initialValues;
        float clipLength, curTime, blendInTime, curBlendInTime, blendOutTime, curBlendOutTime, speed, weight;
        CurveType curveType;
        int speedParamHash;
        bool relative;
        bool loop;
        bool interrupted;

        public CameraCurveData(AnimationCurve[] curves, float clipLength, float blendInTime, float blendOutTime, float speed, float weight, CurveType curveType, bool relative, bool loop, int speedParamHash = 0)
        {
            this.curves = curves;
            this.clipLength = clipLength;
            this.blendInTime = blendInTime;
            this.blendOutTime = blendOutTime;
            this.speed = speed;
            this.speedParamHash = speedParamHash;
            this.curveType = curveType;
            this.relative = relative;
            this.loop = loop;
            this.weight = weight;
            values = new float[curves.Length];
            initialValues = new float[curves.Length];
            if (relative)
            {
                for (int i = 0; i < curves.Length; i++)
                {
                    initialValues[i] = curves[i]?.Evaluate(0) ?? 0;
                }
            }
        }

        public bool Finished => curTime >= clipLength || (interrupted && curBlendOutTime >= blendOutTime);

        public void Update(Animator animator, float dt)
        {
            float dynamicSpeed = this.speed;
            if (speedParamHash != 0)
            {
                dynamicSpeed *= animator.GetWrappedFloat(speedParamHash);
            }

            dt *= dynamicSpeed;
            curBlendInTime += dt;
            curTime += dt;
            if (interrupted)
            {
                curBlendOutTime += dt;
            }
            if (loop)
            {
                curTime %= clipLength;
            }
            for (int i = 0; i < curves.Length; i++)
            {
                if (curves[i] == null)
                {
                    continue;
                }

                values[i] = curves[i].Evaluate(curTime);
            }
        }

        public void Modify(ref Vector3 position, ref Quaternion rotation, Quaternion axisCorrection)
        {
            float dynamicWeight = weight;
            if (blendInTime > 0)
            {
                dynamicWeight = Mathf.Lerp(0, weight, curBlendInTime / blendInTime);
            }
            if (interrupted && blendOutTime > 0)
            {
                dynamicWeight = Mathf.Lerp(dynamicWeight, 0, curBlendOutTime / blendOutTime);
            }

            switch (curveType)
            {
                case CurveType.Position:
                {
                    Vector3 positionValue = new Vector3(values[0], values[1], values[2]);
                    if (relative)
                    {
                        positionValue -= new Vector3(initialValues[0], initialValues[1], initialValues[2]);
                    }
                    position += axisCorrection * Vector3.Lerp(Vector3.zero, positionValue, dynamicWeight);
                    break;
                }
                case CurveType.EularAngleRaw:
                {
                    Vector3 eularRawValue = new Vector3(values[0], values[1], values[2]);
                    if (relative)
                    {
                        eularRawValue -= new Vector3(initialValues[0], initialValues[1], initialValues[2]);
                    }
                    rotation *= Quaternion.Slerp(Quaternion.identity, axisCorrection * Quaternion.Euler(eularRawValue), dynamicWeight);
                    break;
                }
                case CurveType.EularAngleBaked:
                {
                    Quaternion eularBakedValue = axisCorrection * Quaternion.Euler(values[0], values[1], values[2]);
                    if (relative)
                    {
                        eularBakedValue = eularBakedValue * Quaternion.Inverse(axisCorrection * Quaternion.Euler(initialValues[0], initialValues[1], initialValues[2]));
                    }
                    rotation *= Quaternion.Slerp(Quaternion.identity, eularBakedValue, dynamicWeight);
                    break;
                }
                case CurveType.Quaternion:
                {
                    Quaternion rotationValue = axisCorrection * new Quaternion(values[0], values[1], values[2], values[3]);
                    if (relative)
                    {
                        rotationValue = rotationValue * Quaternion.Inverse(axisCorrection * new Quaternion(initialValues[0], initialValues[1], initialValues[2], initialValues[3]));
                    }
                    rotation *= Quaternion.Slerp(Quaternion.identity, rotationValue, dynamicWeight);
                    break;
                }
            }
        }

        public void Interrupt()
        {
            interrupted = true;
        }
    }
    [SerializeField]
    private Transform cameraOffsetTrans;
    [SerializeField]
    private float cameraAnimWeight = 1f;
    [SerializeField]
    private Quaternion axisCorrection = Quaternion.identity;
    private Animator animator;
#if NotEditor
#else
    [SerializeField]
    private Camera debugCamera;

    private Vector3 initialDebugCameraLocalPos;
    private Quaternion initialDebugCameraLocalRot;
#endif
    private List<CameraCurveData> list_curves = new List<CameraCurveData>();

    private void Awake()
    {
        animator = GetComponent<Animator>();
#if NotEditor
        if (!GetComponentInParent<EntityPlayerLocal>())
        {
            Destroy(this);
            return;
        }
#else
        if (debugCamera)
        {
            initialDebugCameraLocalPos = debugCamera.transform.localPosition;
            initialDebugCameraLocalRot = debugCamera.transform.localRotation;
        }
#endif
    }

    private void OnEnable()
    {
        list_curves.Clear();
    }

    private void OnDisable()
    {
        list_curves.Clear();
    }

    private void LateUpdate()
    {
        Vector3 localPos = cameraOffsetTrans.localPosition;
        Quaternion localRot = cameraOffsetTrans.localRotation;
        if (list_curves.Count > 0)
        {
            foreach (CameraCurveData curve in list_curves)
            {
                curve.Update(animator, Time.deltaTime);
                curve.Modify(ref localPos, ref localRot, axisCorrection);
            }

            for (int i = list_curves.Count - 1; i >= 0; i--)
            {
                if (list_curves[i].Finished)
                {
                    list_curves.RemoveAt(i);
                }
            }
        }

        Vector3 camPosOffset = Vector3.Lerp(Vector3.zero, localPos, cameraAnimWeight);
        Quaternion camRotOffset = Quaternion.Slerp(Quaternion.identity, localRot, cameraAnimWeight);
#if NotEditor
        CameraAnimationUpdater.SupplyCameraOffset(camPosOffset, camRotOffset);
#else
        if (debugCamera)
        {
            debugCamera.transform.localPosition = initialDebugCameraLocalPos + camPosOffset;
            debugCamera.transform.localRotation = camRotOffset * initialDebugCameraLocalRot;
        }
#endif
    }

    public void Interrupt()
    {
        foreach (var active in list_curves)
        {
            active.Interrupt();
        }
    }

    public void Play(CameraCurveData curveData)
    {
        list_curves.Add(curveData);
    }

    public MonoBehaviour Init(Transform playerAnimatorTrans, bool isLocalPlayer)
    {
        if (!isLocalPlayer)
        {
            return null;
        }
        var copy = playerAnimatorTrans.AddMissingComponent<CameraAnimationEvents>();
        copy.cameraAnimWeight = cameraAnimWeight;
        copy.cameraOffsetTrans = cameraOffsetTrans;
#if !NotEditor
        copy.debugCamera = debugCamera;
        copy.initialDebugCameraLocalPos = initialDebugCameraLocalPos;
        copy.initialDebugCameraLocalRot = initialDebugCameraLocalRot;
#endif
        return copy;
    }

    public void Disable(Transform playerAnimatorTrans)
    {
        enabled = false;
    }
}
