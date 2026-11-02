using System;
using UnityEngine;

/// <summary>Imported notify windows and three socket tracks in the bound skeleton root's local space.</summary>
[CreateAssetMenu(menuName = "KF2/Animation Trail Data")]
public sealed class KFAnimTrailData : ScriptableObject
{
    public KFAnimTrailClip[] trails = Array.Empty<KFAnimTrailClip>();
    [Tooltip("Optional explicit style alternatives; never required for Source/default notifications.")]
    public KFAnimTrailStyleSet styleSet;
}

[Serializable]
public sealed class KFAnimTrailClip
{
    public string sourceNotify;
    public AnimationClip clip;
    public float startTime;
    public float endTime;
    public KFAnimTrailProfile profile;
    public string firstSocketPath;
    public string controlSocketPath;
    public string secondSocketPath;
    public KFAnimTrailSample[] samples = Array.Empty<KFAnimTrailSample>();
    [Tooltip("Whole-clip socket reference, including poses after the notify window; used only for live pose residuals.")]
    public KFAnimTrailSample[] referenceSamples = Array.Empty<KFAnimTrailSample>();

    public bool IsValid()
    {
        if (clip == null || profile == null || !profile.IsValid() || samples == null || samples.Length < 2 ||
            !KFAnimTrailMesh.Finite(startTime) || !KFAnimTrailMesh.Finite(endTime) || endTime <= startTime)
            return false;
        for (int i = 0; i < samples.Length; i++)
            if (!samples[i].IsFinite() || (i > 0 && samples[i].time <= samples[i - 1].time)) return false;
        if (referenceSamples == null || referenceSamples.Length < 2 || referenceSamples[0].time > startTime ||
            referenceSamples[referenceSamples.Length - 1].time < clip.length - 0.0001f) return false;
        for (int i = 0; i < referenceSamples.Length; i++)
            if (!referenceSamples[i].IsFinite() || (i > 0 && referenceSamples[i].time <= referenceSamples[i - 1].time)) return false;
        return Mathf.Abs(samples[0].time - startTime) < 0.0001f &&
               Mathf.Abs(samples[samples.Length - 1].time - endTime) < 0.0001f;
    }
}

[Serializable]
public sealed class KFAnimTrailProfile
{
    public string sourceSystem;
    public Material material;
    [Min(0.001f)] public float lifetime = 0.2f;
    [ColorUsage(true, true)] public Color color = Color.white;
    [Tooltip("Optional RGB-only lifetime scale. Null/empty preserves legacy constant RGB; alpha stays independent.")]
    public AnimationCurve rgbMultiplier;
    public AnimationCurve alpha = AnimationCurve.Linear(0, 1, 1, 0);
    public AnimationCurve width = AnimationCurve.Constant(0, 1, 1);
    [Tooltip("Distances are in the same skeleton-root-local units as the samples.")]
    [Min(0.000001f)] public float distanceTessellationStep = 0.001f;
    [Min(0)] public float tilingDistance = 0.0256f;

    public bool IsValid()
    {
        return material != null && KFAnimTrailMesh.Finite(lifetime) && lifetime > 0 &&
               KFAnimTrailMesh.Finite(distanceTessellationStep) && distanceTessellationStep > 0 &&
               KFAnimTrailMesh.Finite(tilingDistance) && tilingDistance >= 0 &&
               KFAnimTrailMesh.Finite(color) && alpha != null && width != null;
    }
}

[Serializable]
public struct KFAnimTrailSample
{
    public float time;
    public Vector3 first;
    public Vector3 control;
    public Vector3 second;

    public bool IsFinite()
    {
        return KFAnimTrailMesh.Finite(time) && KFAnimTrailMesh.Finite(first) &&
               KFAnimTrailMesh.Finite(control) && KFAnimTrailMesh.Finite(second);
    }
}
