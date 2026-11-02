using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Receives imported AnimNotify windows; animation time schedules samples, game time ages their tails.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("KFAttachments/Animation/KF Animation Trails")]
public sealed class KFAnimTrailPlayer : MonoBehaviour
{
    public KFAnimTrailData data;
    public Animator animator;
    public Transform skeletonRoot;
    [Tooltip("Root translation above this distance starts no bridging segment (metres per frame).")]
    [Min(0.01f)] public float teleportDistance = 5;

    private sealed class Stroke : IDisposable
    {
        public int entry, layer, state, cycle, generation, nextSample;
        public float lastSampleTime = float.NegativeInfinity;
        public bool emitting = true;
        public KFAnimTrailClip definition;
        public KFAnimTrailProfile profile;
        public Transform first, control, second;
        public KFAnimTrailMesh geometry;
        public GameObject gameObject;
        public float previousClipTime;
        public double previousTime;
        public Matrix4x4 previousRoot;
        public Vector3 previousRootPosition;
        public Vector3 firstCorrection, controlCorrection, secondCorrection;

        public void Dispose()
        {
            if (geometry != null) geometry.Dispose();
            if (gameObject != null)
            {
                gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(gameObject);
                else UnityEngine.Object.DestroyImmediate(gameObject);
            }
            geometry = null; gameObject = null;
        }
    }

    private readonly List<Stroke> strokes = new(4);
    private Color? colorOverride;
    private KFAnimTrailStyle selectedStyle = KFAnimTrailStyle.Source;
    public KFAnimTrailStyle CurrentStyle { get { return selectedStyle; } }
    private int nextGeneration;
    private static readonly int UseColor = Shader.PropertyToID("_UEUseColorData");
    public int ActiveStrokeCount { get { return strokes.Count; } }
    public int EmittingStrokeCount { get { int count = 0; foreach (var s in strokes) if (s.emitting) count++; return count; } }
    public bool HasColorOverride { get { return colorOverride.HasValue; } }
    public Color CurrentColorOverride { get { return colorOverride ?? Color.white; } }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    public void BeginKFAnimTrail(AnimationEvent message)
    {
        if (!isActiveAndEnabled || message == null || !message.isFiredByAnimator || data == null || animator == null || skeletonRoot == null) return;
        int entry = message.intParameter;
        if (entry < 0 || entry >= data.trails.Length) return;
        KFAnimTrailClip definition = data.trails[entry];
        if (definition == null || !definition.IsValid() || message.animatorClipInfo.clip != definition.clip) return;
        int state = message.animatorStateInfo.fullPathHash;
        int layer = FindLayer(state);
        if (layer < 0) return;
        if (animator.IsInTransition(layer))
        {
            var incoming = animator.GetNextAnimatorStateInfo(layer);
            if (incoming.fullPathHash != state || Mathf.Abs(incoming.normalizedTime - message.animatorStateInfo.normalizedTime) > 0.001f) return;
        }
        int cycle = Mathf.FloorToInt(message.animatorStateInfo.normalizedTime);
        float clipTime = (message.animatorStateInfo.normalizedTime - cycle) * definition.clip.length;
        // Late notifications outside their own window cannot resurrect an old attack.
        if (clipTime < definition.startTime - 0.001f || clipTime > definition.endTime + 0.1f) return;
        for (int i = 0; i < strokes.Count; i++)
        {
            var old = strokes[i];
            if (!old.emitting || old.entry != entry || old.state != state || old.layer != layer || old.cycle != cycle) continue;
            // Every native Begin is a new playback generation, even a replay at exactly the same state/time.
            old.emitting = false;
        }
        Transform first = skeletonRoot.Find(definition.firstSocketPath);
        Transform control = skeletonRoot.Find(definition.controlSocketPath);
        Transform second = skeletonRoot.Find(definition.secondSocketPath);
        if (first == null || control == null || second == null) return;
        KFAnimTrailProfile source = definition.profile;
        if (selectedStyle != KFAnimTrailStyle.Source &&
            (data.styleSet == null || !data.styleSet.TryGetProfile(selectedStyle, out source))) return;
        var shader = source.material.shader;
        if (shader == null || shader.FindPropertyIndex("_UEUseColorData") < 0) return;
        double now = Time.timeAsDouble;
        float rate = Mathf.Abs(animator.speed * message.animatorStateInfo.speed * message.animatorStateInfo.speedMultiplier);
        if (rate < 0.00001f) return;
        float rootScale = Mathf.Max(Mathf.Abs(skeletonRoot.lossyScale.x), Mathf.Max(Mathf.Abs(skeletonRoot.lossyScale.y), Mathf.Abs(skeletonRoot.lossyScale.z)));
        var profile = new KFAnimTrailProfile {
            sourceSystem = source.sourceSystem, material = source.material, lifetime = source.lifetime,
            color = source.color, rgbMultiplier = source.rgbMultiplier, alpha = source.alpha, width = source.width,
            distanceTessellationStep = source.distanceTessellationStep * rootScale,
            tilingDistance = source.tilingDistance * rootScale
        };
        var stroke = new Stroke {
            entry = entry, state = state, layer = layer, cycle = cycle, generation = ++nextGeneration, definition = definition, profile = profile,
            first = first, control = control, second = second, geometry = new KFAnimTrailMesh(),
            previousClipTime = definition.startTime, previousTime = now - Math.Max(0, clipTime - definition.startTime) / rate,
            previousRoot = skeletonRoot.localToWorldMatrix, previousRootPosition = skeletonRoot.position
        };
        try
        {
            stroke.gameObject = new GameObject("KF2 Trail " + entry + " #" + stroke.generation) { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded) SceneManager.MoveGameObjectToScene(stroke.gameObject, gameObject.scene);
            stroke.gameObject.AddComponent<MeshFilter>().sharedMesh = stroke.geometry.Mesh;
            var renderer = stroke.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = source.material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock(); block.SetFloat(UseColor, 1); renderer.SetPropertyBlock(block);
            strokes.Add(stroke);
            // AnimationEvent callbacks can precede the evaluated pose. Sample only in LateUpdate,
            // otherwise a head stamped with this clip time would retain the previous pose.
        }
        catch { strokes.Remove(stroke); stroke.Dispose(); throw; }
    }

    public void EndKFAnimTrail(AnimationEvent message)
    {
        if (message == null || !message.isFiredByAnimator || data == null) return;
        int cycle = Mathf.FloorToInt(message.animatorStateInfo.normalizedTime);
        // A delayed end from the outgoing state must not stop the incoming instance.
        foreach (var stroke in strokes)
            if (stroke.emitting && stroke.entry == message.intParameter && stroke.state == message.animatorStateInfo.fullPathHash &&
                stroke.cycle == cycle && stroke.definition.clip == message.animatorClipInfo.clip &&
                stroke.previousClipTime >= stroke.definition.endTime - 0.0001f)
                stroke.emitting = false;
        // Normally LateUpdate consumes the last samples and closes the window. Never erase its live tail here.
    }

    private int FindLayer(int state)
    {
        for (int layer = 0; layer < animator.layerCount; layer++)
            if ((animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).fullPathHash == state) ||
                animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == state) return layer;
        return -1;
    }

    private bool TryClipTime(Stroke stroke, out float time)
    {
        time = 0;
        if (animator == null || !animator.isActiveAndEnabled || stroke.layer >= animator.layerCount) return false;
        AnimatorStateInfo state;
        if (animator.IsInTransition(stroke.layer))
        {
            state = animator.GetNextAnimatorStateInfo(stroke.layer);
            if (state.fullPathHash != stroke.state) return false; // interrupted outgoing attack, keep only its tail
        }
        else state = animator.GetCurrentAnimatorStateInfo(stroke.layer);
        if (state.fullPathHash != stroke.state || Mathf.FloorToInt(state.normalizedTime) != stroke.cycle) return false;
        time = (state.normalizedTime - stroke.cycle) * stroke.definition.clip.length;
        return time >= stroke.previousClipTime - 0.00001f;
    }

    private void LateUpdate()
    {
        double now = Time.timeAsDouble;
        for (int i = strokes.Count - 1; i >= 0; i--)
        {
            Stroke stroke = strokes[i];
            float clipTime;
            if (stroke.emitting && skeletonRoot != null && TryClipTime(stroke, out clipTime)) Advance(stroke, clipTime, now);
            else stroke.emitting = false;
            stroke.geometry.Build(now, stroke.profile, colorOverride);
            if (!stroke.emitting && !stroke.geometry.HasLivePoints) { stroke.Dispose(); strokes.RemoveAt(i); }
        }
    }

    private void Advance(Stroke stroke, float clipTime, double now)
    {
        Matrix4x4 currentRoot = skeletonRoot.localToWorldMatrix;
        // There is no prior evaluated root before the first sample; do not compare against the notify callback pose.
        if (stroke.nextSample > 0 && Vector3.Distance(skeletonRoot.position, stroke.previousRootPosition) > teleportDistance) { stroke.emitting = false; return; }
        KFAnimTrailSample reference = Evaluate(stroke.definition.referenceSamples, clipTime);
        // Bake supplies the curved path between frames. The live residual keeps blended/IK-adjusted frame endpoints attached.
        Vector3 firstCorrection = stroke.first.position - currentRoot.MultiplyPoint3x4(reference.first);
        Vector3 controlCorrection = stroke.control.position - currentRoot.MultiplyPoint3x4(reference.control);
        Vector3 secondCorrection = stroke.second.position - currentRoot.MultiplyPoint3x4(reference.second);
        if (stroke.nextSample == 0)
        {
            // The first evaluated pose anchors the baked startup history. No earlier live pose is known.
            // Preserve the original notification birth clock and window; do not invent pre-pose residuals.
            stroke.previousRoot = currentRoot;
            stroke.previousRootPosition = skeletonRoot.position;
            stroke.firstCorrection = firstCorrection;
            stroke.controlCorrection = controlCorrection;
            stroke.secondCorrection = secondCorrection;
        }
        var samples = stroke.definition.samples;
        float span = clipTime - stroke.previousClipTime;
        while (stroke.nextSample < samples.Length && samples[stroke.nextSample].time <= clipTime + 0.000001f)
        {
            KFAnimTrailSample sample = samples[stroke.nextSample++];
            if (sample.time <= stroke.lastSampleTime + 0.000001f) continue;
            float fraction = span > 0.000001f ? Mathf.Clamp01((sample.time - stroke.previousClipTime) / span) : 1;
            double birth = stroke.previousTime + (now - stroke.previousTime) * fraction;
            sample.first = Vector3.Lerp(stroke.previousRoot.MultiplyPoint3x4(sample.first), currentRoot.MultiplyPoint3x4(sample.first), fraction) + Vector3.Lerp(stroke.firstCorrection, firstCorrection, fraction);
            sample.control = Vector3.Lerp(stroke.previousRoot.MultiplyPoint3x4(sample.control), currentRoot.MultiplyPoint3x4(sample.control), fraction) + Vector3.Lerp(stroke.controlCorrection, controlCorrection, fraction);
            sample.second = Vector3.Lerp(stroke.previousRoot.MultiplyPoint3x4(sample.second), currentRoot.MultiplyPoint3x4(sample.second), fraction) + Vector3.Lerp(stroke.secondCorrection, secondCorrection, fraction);
            if (!stroke.geometry.Append(sample, birth)) { stroke.emitting = false; break; }
            stroke.lastSampleTime = sample.time;
        }
        // The fixed source cadence supplies the curved history; add the actual frame endpoint so the head never lags behind the blade.
        if (stroke.emitting && clipTime < stroke.definition.endTime && clipTime > stroke.lastSampleTime + 0.000001f)
        {
            var head = new KFAnimTrailSample { time = clipTime, first = stroke.first.position, control = stroke.control.position, second = stroke.second.position };
            if (!stroke.geometry.Append(head, now)) stroke.emitting = false;
            else stroke.lastSampleTime = clipTime;
        }
        stroke.previousClipTime = clipTime; stroke.previousTime = now; stroke.previousRoot = currentRoot;
        stroke.previousRootPosition = skeletonRoot.position; stroke.firstCorrection = firstCorrection;
        stroke.controlCorrection = controlCorrection; stroke.secondCorrection = secondCorrection;
        if (clipTime >= stroke.definition.endTime || stroke.nextSample >= samples.Length) stroke.emitting = false;
    }

    private static KFAnimTrailSample Evaluate(KFAnimTrailSample[] samples, float time)
    {
        if (time <= samples[0].time) return samples[0];
        int end = samples.Length - 1;
        if (time >= samples[end].time) return samples[end];
        int lo = 0, hi = end;
        while (lo + 1 < hi) { int mid = (lo + hi) / 2; if (samples[mid].time <= time) lo = mid; else hi = mid; }
        var a = samples[lo]; var b = samples[hi]; float t = (time - a.time) / (b.time - a.time);
        return new KFAnimTrailSample { time = time, first = Vector3.Lerp(a.first, b.first, t),
            control = Vector3.Lerp(a.control, b.control, t), second = Vector3.Lerp(a.second, b.second, t) };
    }

    /// <summary>Validate without mutation so root routes can preflight every player atomically.</summary>
    public bool CanSetTrailStyle(KFAnimTrailStyle style)
    {
        if (style == KFAnimTrailStyle.Source) return true;
        KFAnimTrailProfile profile;
        return data != null && data.styleSet != null && data.styleSet.TryGetProfile(style, out profile);
    }

    /// <summary>Only future Begin notifications select this style. Existing strokes keep their profile and tail lifetime.</summary>
    public bool SetTrailStyle(KFAnimTrailStyle style)
    {
        if (!CanSetTrailStyle(style)) return false;
        selectedStyle = style;
        return true;
    }

    public bool SetTrailColor(Color linearColor)
    {
        if (!KFAnimTrailMesh.Finite(linearColor)) return false;
        colorOverride = linearColor; // accepted even while no stroke exists, including when this component is disabled
        foreach (var stroke in strokes) stroke.geometry.Build(Time.timeAsDouble, stroke.profile, colorOverride);
        return true;
    }

    public void ResetTrailColor()
    {
        colorOverride = null;
        foreach (var stroke in strokes) stroke.geometry.Build(Time.timeAsDouble, stroke.profile, colorOverride);
    }
    public void StopEmitting() { foreach (var stroke in strokes) stroke.emitting = false; }
    public void ClearTrails() { foreach (var stroke in strokes) stroke.Dispose(); strokes.Clear(); }
    private void OnDisable() { ClearTrails(); }
    private void OnDestroy() { ClearTrails(); }
}
