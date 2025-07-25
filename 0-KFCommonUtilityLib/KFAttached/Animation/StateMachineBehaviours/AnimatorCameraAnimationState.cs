﻿using UnityEngine;
using static CameraAnimationEvents;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;
#else
#endif

[DisallowMultipleComponent]
public class AnimatorCameraAnimationState : StateMachineBehaviour
#if UNITY_EDITOR
    , ISerializationCallbackReceiver
#endif
{
#if UNITY_EDITOR
    [Header("Animation Input")]
    [SerializeField]
    private AnimationClip clip;
    [SerializeField]
    private string propertyPath;
#endif
    [Header("Animation Data (read from input clip)")]
    [SerializeField]
    private string clipName;
    [SerializeField]
    private float clipLength;
    [SerializeField]
    private AnimationCurve[] positionCurves;
    [SerializeField]
    private AnimationCurve[] rotationCurves;
    [SerializeField]
    private CameraAnimationEvents.CurveType rotationCurveType = CurveType.Quaternion;
    [Header("State Data (read from animator)")]
    [SerializeField]
    private float stateDuration;
    [SerializeField]
    private string speedParam;
    [SerializeField, HideInInspector]
    private float speed = 1f;
    [SerializeField, HideInInspector]
    private int speedParamHash;
    [SerializeField, HideInInspector]
    private bool loop;
    [Header("Parameters")]
    [SerializeField]
    private float weight = 1;
    [SerializeField]
    private float blendInTime = 0.2f;
    [SerializeField]
    private float blendOutTime = 0.2f;
    [SerializeField]
    private float speedMultiplier = 1f;
    [SerializeField]
    private bool relative = true;
    [SerializeField]
    private bool normalizeLength = false;

    private CameraCurveData curvePositionData, curveRotationData;

#if UNITY_EDITOR
    public void OnBeforeSerialize()
    {
        try
        {
            var context = AnimatorController.FindStateMachineBehaviourContext(this);
            speedParam = null;
            speedParamHash = 0;
            speed = 1;
            loop = false;
            if (context != null && context.Length > 0)
            {
                var state = context[0].animatorObject as AnimatorState;
                if (state != null)
                {
                    speed = state.speed;
                    if (state.speedParameterActive)
                    {
                        speedParam = state.speedParameter;
                        speedParamHash = Animator.StringToHash(speedParam);
                    }
                    var stateClip = state.motion as AnimationClip;
                    if (stateClip != null)
                    {
                        stateDuration = stateClip.length;
                        loop = stateClip.isLooping;
                        if (!string.IsNullOrEmpty(propertyPath) && clip == null)
                        {
                            ExtractCurvesFromClip(stateClip, propertyPath);
                        }
                    }
                }
            }

            if (clip != null)
            {
                ExtractCurvesFromClip(clip);
            }

            if (clipLength <= 0)
            {
                if (positionCurves != null)
                {
                    clipLength = positionCurves.FirstOrDefault(curve => curve != null)?.keys?.Last().time ?? 0;
                }
                if (clipLength <= 0 && rotationCurves != null)
                {
                    clipLength = rotationCurves.FirstOrDefault(curve => curve != null)?.keys?.Last().time ?? 0;
                }
            }
        }
        finally
        {
            propertyPath = null;
            clip = null;
            if (loop)
            {
                normalizeLength = true;
            }
        }
    }

    public void OnAfterDeserialize()
    {
        
    }

    private void ExtractCurvesFromClip(AnimationClip clip, string propertyPath = null)
    {
        clipName = clip.name;
        clipLength = clip.length;
        positionCurves = new AnimationCurve[3];
        rotationCurves = new AnimationCurve[4];
        rotationCurveType = CurveType.EularAngleRaw;

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            if (!string.IsNullOrEmpty(propertyPath) && binding.path != propertyPath)
            {
                continue;
            }
            string propertyName = binding.propertyName;
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (propertyName.StartsWith("m_LocalRotation", System.StringComparison.OrdinalIgnoreCase))
            {
                rotationCurveType = CurveType.Quaternion;
                if (propertyName.EndsWith("x", System.StringComparison.OrdinalIgnoreCase))
                {
                    rotationCurves[0] = curve;
                }
                else if (propertyName.EndsWith("y", System.StringComparison.OrdinalIgnoreCase))
                {
                    rotationCurves[1] = curve;
                }
                else if (propertyName.EndsWith("z", System.StringComparison.OrdinalIgnoreCase))
                {
                    rotationCurves[2] = curve;
                }
                else if (propertyName.EndsWith("w", System.StringComparison.OrdinalIgnoreCase))
                {
                    rotationCurves[3] = curve;
                }
            }
            else if (propertyName.StartsWith("localEulerAnglesBaked", System.StringComparison.OrdinalIgnoreCase))
            {
                rotationCurveType = CurveType.EularAngleBaked;
                if (propertyName.EndsWith("x", System.StringComparison.OrdinalIgnoreCase))
                {
                    rotationCurves[0] = curve;
                }
                else if (propertyName.EndsWith("y", System.StringComparison.OrdinalIgnoreCase))
                {
                    rotationCurves[1] = curve;
                }
                else if (propertyName.EndsWith("z", System.StringComparison.OrdinalIgnoreCase))
                {
                    rotationCurves[2] = curve;
                }
            }
            else if (propertyName.StartsWith("localEulerAnglesRaw", System.StringComparison.OrdinalIgnoreCase))
            {
                rotationCurveType = CurveType.EularAngleRaw;
                if (propertyName.EndsWith("x", System.StringComparison.OrdinalIgnoreCase))
                {
                    rotationCurves[0] = curve;
                }
                else if (propertyName.EndsWith("y", System.StringComparison.OrdinalIgnoreCase))
                {
                    rotationCurves[1] = curve;
                }
                else if (propertyName.EndsWith("z", System.StringComparison.OrdinalIgnoreCase))
                {
                    rotationCurves[2] = curve;
                }
            }
            else if (propertyName.Contains("m_LocalPosition", System.StringComparison.OrdinalIgnoreCase))
            {
                if (propertyName.EndsWith("x", System.StringComparison.OrdinalIgnoreCase))
                {
                    positionCurves[0] = curve;
                }
                else if (propertyName.EndsWith("y", System.StringComparison.OrdinalIgnoreCase))
                {
                    positionCurves[1] = curve;
                }
                else if (propertyName.EndsWith("z", System.StringComparison.OrdinalIgnoreCase))
                {
                    positionCurves[2] = curve;
                }
            }
        }

        if (rotationCurveType != CurveType.Quaternion)
        {
            rotationCurves = new AnimationCurve[]
            {
                rotationCurves[0],
                rotationCurves[1],
                rotationCurves[2]
            };
        }
    }
#endif

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var cameraEvents = animator.GetComponent<CameraAnimationEvents>();
        if (cameraEvents)
        {
            cameraEvents.Interrupt();
            float baseSpeed = normalizeLength ? speed * clipLength / stateDuration : speed * speedMultiplier;
            if (positionCurves != null && positionCurves.Length == 3)
            {
                curvePositionData = new CameraCurveData(positionCurves, clipLength, blendInTime, blendOutTime, baseSpeed, weight, CurveType.Position, relative, loop, speedParamHash);
                cameraEvents.Play(curvePositionData);
            }
            if (rotationCurves != null && (((rotationCurveType == CurveType.EularAngleRaw || rotationCurveType == CurveType.EularAngleBaked) && rotationCurves.Length == 3) || (rotationCurveType == CurveType.Quaternion && rotationCurves.Length == 4)))
            {
                curveRotationData = new CameraCurveData(rotationCurves, clipLength, blendInTime, blendOutTime, baseSpeed, weight, rotationCurveType, relative, loop, speedParamHash);
                cameraEvents.Play(curveRotationData);
            }
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (loop)
        {
            curvePositionData?.Interrupt();
            curveRotationData?.Interrupt();
        }
        curvePositionData = null;
        curveRotationData = null;
    }
}
