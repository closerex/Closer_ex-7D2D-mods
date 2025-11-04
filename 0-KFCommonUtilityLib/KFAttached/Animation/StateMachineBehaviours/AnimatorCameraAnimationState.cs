using UnityEngine;
using static CameraAnimationEvents;
using System;
using System.Collections.Generic;

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
    public AnimationClip clip;
    [SerializeField]
    public string propertyPath;
#endif
    [Header("Parameters")]
    [SerializeField]
    public float weight = 1;
    [SerializeField]
    public float delay = 0f;
    [SerializeField]
    public float blendInTime = 0.2f;
    [SerializeField]
    public float blendOutTime = 0.2f;
    [SerializeField]
    public float speedMultiplier = 1f;
    [SerializeField]
    public float targetStateFps = -1f;
    [SerializeField]
    public bool relative = true;
    [SerializeField]
    public bool normalizeLength = false;
    [SerializeField]
    internal string tagOverride;
    [SerializeField]
    private bool interruptOnExit = true;
    [Header("Animation Data (read from input clip)")]
    [SerializeField]
    private string clipName;
    [SerializeField]
    private float clipLength;
    [SerializeField]
    private string clipID;
    [SerializeField]
    private string clipPropertyRemembered;
    [SerializeField]
    private ulong clipLastModified;
    [SerializeField]
    private AnimationCurve[] positionCurves;
    [SerializeField]
    private AnimationCurve[] rotationCurves;
    [SerializeField]
    private CurveType rotationCurveType = CurveType.Quaternion;
    [Header("State Data (read from animator)")]
    [SerializeField]
    private float stateDuration;
    [SerializeField]
    private string speedParam;
    [SerializeField]
    private float initialFpsModifier = 1f;
    [SerializeField, HideInInspector]
    private float speed = 1f;
    [SerializeField, HideInInspector]
    private int speedParamHash;
    [SerializeField, HideInInspector]
    private bool loop;
    [SerializeField]
    private int tagOrNameHash = 0;

    private Queue<CameraCurveData> queue_pos_curves = new(), queue_rot_curves = new();

#if UNITY_EDITOR
    public static bool IsReloading { get; private set; }
    static AnimatorCameraAnimationState()
    {
        SubscribeEvents();
    }

    private static void SubscribeEvents()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;

        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

        IsReloading = false;
    }

    private static void OnBeforeAssemblyReload()
    {
        IsReloading = true;
    }

    private static void OnAfterAssemblyReload()
    {
        IsReloading = false;
    }
    public void OnBeforeSerialize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || IsReloading)
        {
            return;
        }
        tagOrNameHash = 0;
        try
        {
            var context = AnimatorController.FindStateMachineBehaviourContext(this);
            speedParam = null;
            speedParamHash = 0;
            speed = 1;
            initialFpsModifier = 1f;
            loop = false;
            if (context != null && context.Length > 0)
            {
                var state = context[0].animatorObject as AnimatorState;
                if (state != null)
                {
                    speed = state.speed;
                    tagOrNameHash = string.IsNullOrEmpty(tagOverride) ? (string.IsNullOrEmpty(state.tag) ? state.nameHash : Animator.StringToHash(state.tag)) : Animator.StringToHash(tagOverride);
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
                        if (targetStateFps > 0)
                        {
                            initialFpsModifier = stateClip.frameRate / targetStateFps;
                        }
                        if (clip == null)
                        {
                            if (!string.IsNullOrEmpty(propertyPath))
                            {
                                ExtractCurvesFromClip(stateClip, propertyPath);
                                clipPropertyRemembered = propertyPath;
                                clipID = null;
                            }
                            else if (!string.IsNullOrEmpty(clipPropertyRemembered) && string.IsNullOrEmpty(clipID))
                            {
                                if (CheckModified(stateClip))
                                {
                                    ExtractCurvesFromClip(stateClip, clipPropertyRemembered);
                                    Log.Out($"Loading Clip from current state - {clipPropertyRemembered}");
                                }
                            }
                        }
                    }
                }
            }

            if (clip != null)
            {
                ExtractCurvesFromClip(clip);
                clipID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip));
                clipPropertyRemembered = null;
                Log.Out($"Loading Clip from assigned clip - {clip.name}");
            }
            else if (!string.IsNullOrEmpty(clipID))
            {
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(clipID));
                if (clip != null)
                {
                    if (CheckModified(clip))
                    {
                        ExtractCurvesFromClip(clip, propertyPath);
                        Log.Out($"Loading Clip from GUID {clipID} - {clip.name}");
                    }
                }
                else
                {
                    clipID = null;
                }
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

    private bool CheckModified(AnimationClip clip)
    {
        string stateClipPath = AssetDatabase.GetAssetPath(clip);
        ulong lastModified = AssetImporter.GetAtPath(stateClipPath).assetTimeStamp;
        if (lastModified != clipLastModified)
        {
            return true;
        }
        return false;
    }

    private void ExtractCurvesFromClip(AnimationClip clip, string propertyPath = null)
    {
        clipName = clip.name;
        clipLength = clip.length;
        positionCurves = new AnimationCurve[3];
        rotationCurves = new AnimationCurve[4];
        rotationCurveType = CurveType.EularAngleRaw;
        clipLastModified = 0;

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        if (!string.IsNullOrEmpty(propertyPath))
        {
            bindings = bindings.Where(binding => binding.path == propertyPath).ToArray();
            if (bindings.Length == 0)
            {
                positionCurves = null;
                rotationCurves = null;
                Debug.LogWarning($"No curves found for property path: {propertyPath} in clip: {clip.name}");
                return;
            }
            EditorCurveBinding[] rotationRawBindings = bindings.Where(binding => binding.propertyName.StartsWith("localEulerAnglesRaw", System.StringComparison.OrdinalIgnoreCase)).ToArray();
            if (rotationRawBindings.Length > 0)
            {
                Type.GetType("UnityEditor.RotationCurveInterpolation,UnityEditor.CoreModule").GetMethod("SetInterpolation", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(null, new object[] { clip, rotationRawBindings, 0 });
                clip.EnsureQuaternionContinuity();
                bindings = AnimationUtility.GetCurveBindings(clip).Where(binding => binding.path == propertyPath).ToArray();
            }
        }
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
        clipLastModified = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)).assetTimeStamp;
    }
#endif

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var cameraEvents = animator.GetComponent<CameraAnimationEvents>();
        if (cameraEvents)
        {
            int statehash = stateInfo.fullPathHash;
            cameraEvents.Interrupt(statehash);
            float baseSpeed = normalizeLength ? speed * clipLength / stateDuration : speed * speedMultiplier * initialFpsModifier;
            if (positionCurves != null && positionCurves.Length == 3)
            {
                var curvePositionData = new CameraCurveData(tagOrNameHash, statehash, positionCurves, clipLength, delay, blendInTime, blendOutTime, baseSpeed, weight, CurveType.Position, relative, loop, speedParamHash);
                cameraEvents.Play(curvePositionData);
                queue_pos_curves.Enqueue(curvePositionData);
            }
            if (rotationCurves != null && (((rotationCurveType == CurveType.EularAngleRaw || rotationCurveType == CurveType.EularAngleBaked) && rotationCurves.Length == 3) || (rotationCurveType == CurveType.Quaternion && rotationCurves.Length == 4)))
            {
                var curveRotationData = new CameraCurveData(tagOrNameHash, statehash, rotationCurves, clipLength, delay, blendInTime, blendOutTime, baseSpeed, weight, rotationCurveType, relative, loop, speedParamHash);
                cameraEvents.Play(curveRotationData);
                queue_rot_curves.Enqueue(curveRotationData);
            }
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        bool shouldInterrupt = loop || interruptOnExit;
        if (queue_pos_curves.TryDequeue(out var curvePositionData) && shouldInterrupt)
        {
            curvePositionData?.Interrupt();
        }
        if (queue_rot_curves.TryDequeue(out var curveRotationData) && shouldInterrupt)
        {
            curveRotationData?.Interrupt();
        }
    }
}
