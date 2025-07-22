using UnityEngine;
using static CameraAnimationEvents;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#else
#endif

public class AnimatorCameraAnimationState : StateMachineBehaviour
#if UNITY_EDITOR
    , ISerializationCallbackReceiver
#endif
{
    [Header("Animation Input")]
    [SerializeField]
    private AnimationClip clip;
    [Header("Animation Data (read from input clip)")]
    [SerializeField]
    private string clipName;
    [SerializeField]
    private AnimationCurve[] positionCurves;
    [SerializeField]
    private AnimationCurve[] rotationCurves;
    [SerializeField]
    private CameraAnimationEvents.CurveType rotationCurveType;
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

    private CameraAnimationEvents.CameraCurveData curvePositionData, curveRotationData;

#if UNITY_EDITOR
    public void OnBeforeSerialize()
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
                var clip = state.motion as AnimationClip;
                if (clip != null)
                {
                    stateDuration = clip.length;
                    loop = clip.isLooping;
                }
            }
        }

        if (clip != null)
        {
            clipName = clip.name;
            positionCurves = new AnimationCurve[3];
            rotationCurves = new AnimationCurve[4];
            rotationCurveType = CameraAnimationEvents.CurveType.EularAngleRaw;

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                string propertyName = binding.propertyName;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (propertyName.StartsWith("m_LocalRotation", System.StringComparison.OrdinalIgnoreCase))
                {
                    rotationCurveType = CameraAnimationEvents.CurveType.Quaternion;
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
                    rotationCurveType = CameraAnimationEvents.CurveType.EularAngleBaked;
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
                    rotationCurveType = CameraAnimationEvents.CurveType.EularAngleRaw;
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

            if (rotationCurveType != CameraAnimationEvents.CurveType.Quaternion)
            {
                rotationCurves = new AnimationCurve[]
                {
                    rotationCurves[0],
                    rotationCurves[1],
                    rotationCurves[2]
                };
            }
            clip = null;
        }
    }

    public void OnAfterDeserialize()
    {
        
    }
#endif

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var cameraEvents = animator.GetComponent<CameraAnimationEvents>();
        if (cameraEvents)
        {
            cameraEvents.Interrupt();
            float baseSpeed = speed * speedMultiplier;
            if (positionCurves != null && positionCurves.Length == 3)
            {
                curvePositionData = new CameraCurveData(positionCurves, stateDuration, blendInTime, blendOutTime, baseSpeed, weight, CurveType.Position, relative, loop, speedParamHash);
                cameraEvents.Play(curvePositionData);
            }
            if (rotationCurves != null && (((rotationCurveType == CameraAnimationEvents.CurveType.EularAngleRaw || rotationCurveType == CameraAnimationEvents.CurveType.EularAngleBaked) && rotationCurves.Length == 3) || (rotationCurveType == CameraAnimationEvents.CurveType.Quaternion && rotationCurves.Length == 4)))
            {
                curveRotationData = new CameraCurveData(rotationCurves, stateDuration, blendInTime, blendOutTime, baseSpeed, weight, rotationCurveType, relative, loop, speedParamHash);
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
