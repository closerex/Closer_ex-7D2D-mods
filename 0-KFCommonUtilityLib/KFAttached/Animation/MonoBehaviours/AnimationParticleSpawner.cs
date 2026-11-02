using KFCommonUtilityLib;
using System.Collections.Generic;
using UnityEngine;

public class AnimationParticleSpawner : MonoBehaviour
{
    private Dictionary<string, Transform> dict_path;
    private readonly List<AnimationParticleOneShot> ownedOneShots = new();

    private void Awake()
    {
        dict_path = new Dictionary<string, Transform>();
    }

    public void SpawnParticle(AnimationEvent param)
    {
        if (!dict_path.TryGetValue(param.stringParameter, out var parent))
        {
            parent = transform.FindInAllChildren(param.stringParameter);
            if (!parent)
            {
                return;
            }
            dict_path[param.stringParameter] = parent;
        }

        if (param.objectReferenceParameter is GameObject prefab)
        {
            var particle = Instantiate(prefab, parent, false);
            if (particle)
            {
                particle.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
#if NotEditor
                particle.transform.AddMissingComponent<TemporaryMuzzleFlash>().life = param.floatParameter > 0f ? param.floatParameter : 5f;
#endif
            }
        }
    }

    // Opt-in for finite animation VFX. Existing SpawnParticle events retain their timed cleanup.
    public void SpawnParticleOneShot(AnimationEvent param)
    {
        if (!(param.objectReferenceParameter is GameObject prefab)) return;
        if (dict_path == null) dict_path = new Dictionary<string, Transform>();
        if (!dict_path.TryGetValue(param.stringParameter, out var parent) || !parent)
        {
            parent = transform.FindInAllChildren(param.stringParameter);
            if (!parent) return;
            dict_path[param.stringParameter] = parent;
        }
        ownedOneShots.RemoveAll(instance => !instance);
        var particle = Instantiate(prefab, parent, false);
        particle.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        particle.SetActive(true);
        var lifetime = particle.AddComponent<AnimationParticleOneShot>();
        if (!lifetime.Begin(transform))
        {
            Debug.LogWarning("Animation one-shot requires finite non-looping particle systems: " + prefab.name, this);
            Destroy(particle);
            return;
        }
        ownedOneShots.Add(lifetime);
    }

    // Live one-shot instances created by this spawner; consumers read this instead of scanning the scene.
    public IReadOnlyList<AnimationParticleOneShot> OneShots => ownedOneShots;

    public void CancelOneShotParticles()
    {
        foreach (var instance in ownedOneShots)
            if (instance) Destroy(instance.gameObject);
        ownedOneShots.Clear();
    }

    private void OnDisable()
    {
        CancelOneShotParticles();
    }
}

// Runtime-only helper: kept in this compile unit for the existing explicit game-project Compile entry.
namespace KFCommonUtilityLib
{
    // Only owns an instance created for one animation notification, never a weapon subtree.
    [DisallowMultipleComponent]
    public sealed class AnimationParticleOneShot : MonoBehaviour
    {
        private ParticleSystem[] systems;
        private bool initialized;

        public bool Begin(Transform simulationSpace)
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0) return false;
            foreach (var ps in systems)
            {
                var main = ps.main;
                if (main.loop || !Finite(main.startLifetime) || !Finite(main.startDelay)) return false;
                if (main.simulationSpace == ParticleSystemSimulationSpace.Custom && !simulationSpace) return false;
            }
            foreach (var ps in systems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                if (main.simulationSpace == ParticleSystemSimulationSpace.Custom)
                    main.customSimulationSpace = simulationSpace;
            }
            initialized = true;
            // Every system is finite and non-looping, so the end time is known at spawn: no per-frame IsAlive polling.
            float longest = 0f, longestSub = 0f;
            foreach (var ps in systems)
            {
                var main = ps.main;
                float end = (Max(main.startDelay) + main.duration + Max(main.startLifetime)) / Mathf.Max(main.simulationSpeed, 0.0001f);
                if (ps.subEmitters.enabled) for (int i = 0; i < ps.subEmitters.subEmittersCount; i++)
                {
                    var sub = ps.subEmitters.GetSubEmitterSystem(i);
                    if (sub) longestSub = Mathf.Max(longestSub, Max(sub.main.startLifetime) / Mathf.Max(sub.main.simulationSpeed, 0.0001f));
                }
                longest = Mathf.Max(longest, end);
            }
            foreach (var ps in systems)
                if (ps.gameObject.activeInHierarchy) ps.Play(false);
            Destroy(gameObject, longest + longestSub);
            return true;
        }

        private static float Max(ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant: return curve.constant;
                case ParticleSystemCurveMode.TwoConstants: return Mathf.Max(curve.constantMin, curve.constantMax);
                default:
                    float peak = 0f;
                    foreach (var key in curve.curveMax.keys) peak = Mathf.Max(peak, key.value);
                    if (curve.mode == ParticleSystemCurveMode.TwoCurves) foreach (var key in curve.curveMin.keys) peak = Mathf.Max(peak, key.value);
                    return peak * curve.curveMultiplier;
            }
        }

        private void OnDisable()
        {
            // A disabled/unequipped owner cancels this instance; it must not return as stale VFX.
            if (!initialized || !Application.isPlaying) return;
            initialized = false;
            Destroy(gameObject);
        }

        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static bool Finite(AnimationCurve curve)
        {
            if (curve == null) return false;
            foreach (var key in curve.keys) if (!Finite(key.value)) return false;
            return true;
        }
        private static bool Finite(ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant: return Finite(curve.constant);
                case ParticleSystemCurveMode.TwoConstants: return Finite(curve.constantMin) && Finite(curve.constantMax);
                case ParticleSystemCurveMode.Curve: return Finite(curve.curveMultiplier) && Finite(curve.curve);
                case ParticleSystemCurveMode.TwoCurves: return Finite(curve.curveMultiplier) && Finite(curve.curveMin) && Finite(curve.curveMax);
                default: return false;
            }
        }
    }
}
