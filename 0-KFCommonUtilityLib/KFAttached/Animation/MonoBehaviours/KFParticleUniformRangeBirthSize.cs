using System;
using UnityEngine;

namespace KFCommonUtilityLib
{
    /// <summary>
    /// Opt-in birth-size adapter using the explicitly RNE UniformRange sampler.
    /// Its independent seed is not the original KF2 global random schedule.
    /// ParticleSystem Play/Stop/Clear do not reset this sequence; OnEnable and
    /// ResetSequence do. Disabling this component never clears existing particles.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class KFParticleUniformRangeBirthSize : MonoBehaviour
    {
        public Vector3 firstStart = Vector3.one, firstEnd = Vector3.one;
        public Vector3 secondStart = Vector3.one, secondEnd = Vector3.one;
        public float unitScale = .01f;
        public uint initialSeed = 1u;

        private ParticleSystem particles;
        private uint state;
        public uint CurrentSeed { get { return state; } }
        public long RequestedParticleCount { get; private set; }

        private void OnEnable() { ResetSequence(initialSeed); }

        public void ResetSequence(uint seed)
        {
            state = seed;
            RequestedParticleCount = 0;
        }

        private ParticleSystem Target()
        {
            if (!particles) particles = GetComponent<ParticleSystem>();
            return particles;
        }

        // Configuration validation is independent of activeInHierarchy: an inactive
        // receiver is skipped by the bridge without blocking other valid receivers.
        public bool IsValidFor(ParticleSystem target)
        {
            if (!enabled || !target || Target() != target || !Finite(unitScale) || unitScale <= 0) return false;
            var main = target.main;
            return main.simulationSpace == ParticleSystemSimulationSpace.World && main.startSize3D &&
                   SafePair(firstStart, firstEnd) && SafePair(secondStart, secondEnd);
        }

        public bool TryEmit(ParticleSystem.EmitParams properties, int count)
        {
            var target = Target();
            if (count <= 0 || !gameObject.activeInHierarchy || !IsValidFor(target)) return false;
            for (int i = 0; i < count; i++)
            {
                properties.startSize3D = SamplePreparedSize();
                target.Emit(properties, 1);
                RequestedParticleCount++;
            }
            return true;
        }

        // Called only after whole-group validation; this does not submit an emission.
        internal Vector3 SamplePreparedSize()
        {
            Vector3 value = KFUniformRangeSampler.SampleNearestEven(ref state, firstStart, firstEnd, secondStart, secondEnd);
            return new Vector3(value.x * unitScale, value.y * unitScale, value.z * unitScale);
        }
        internal void RecordSubmittedParticle() { RequestedParticleCount++; }

        private bool SafePair(Vector3 start, Vector3 end)
        {
            return SafeAxis(start.x, end.x) && SafeAxis(start.y, end.y) && SafeAxis(start.z, end.z);
        }

        private bool SafeAxis(float start, float end)
        {
            float delta = end - start;
            if (!Finite(start) || !Finite(end) || !Finite(delta)) return false;
            // Conservative bound for every rounded interpolation, then its unit
            // conversion. Reject before advancing any seed or emitting any particle.
            double bound = Math.Abs((double)start) + Math.Abs((double)delta);
            if (bound > float.MaxValue) return false;
            return (double)(float)bound * unitScale <= float.MaxValue;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
