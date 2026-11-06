using UnityEngine;

namespace KFCommonUtilityLib
{
    /// <summary>
    /// Opt-in for a single time-zero World burst with constant random local velocity.
    /// A fixed nonnegative main.startDelay requires allowConstantStartDelay; burst.time stays zero.
    /// Captures Unity's sampled animated velocity after its first observed simulation,
    /// not a proven KF2 birth timestamp or random sequence. World force stays separate.
    /// Before replaying an active instance, Stop/Clear then call PrepareForPlay before Play.
    /// Disabling this component never restores a module underneath living baked particles.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ParticleSystem)), DefaultExecutionOrder(10000)]
    public sealed class KFParticleWorldBurstVelocity : MonoBehaviour
    {
        private ParticleSystem target;
        private ParticleSystem.Particle[] buffer;
        private bool armed, ownsDisabledModule, observedPlaying;
        private int expectedCount, minimumCount;
        private ParticleSystemCurveMode preparedCountMode;
        [Tooltip("Allow a fixed nonnegative system start delay; the single burst must still be at time zero.")]
        public bool allowConstantStartDelay;
        [Tooltip("Allow a single integer TwoConstants burst count, including a legitimate zero result.")]
        public bool allowRandomBurstCount;
        [Tooltip("Allow finite positive TwoConstants start lifetimes; preserves each particle's sampled lifetime.")]
        public bool allowRandomStartLifetime;
        public int CapturedParticleCount { get; private set; }
        public float CaptureTime { get; private set; }
        public string LastFailure { get; private set; }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!target) target = GetComponent<ParticleSystem>();
            // A component-only disable/enable must leave an existing captured tail alone.
            if (ownsDisabledModule && target.particleCount > 0) return;
            if (target.isStopped && target.particleCount == 0) { PrepareForPlay(); return; }
            // playOnAwake may start the native system before this OnEnable, but no
            // simulation has happened yet. This branch never restores a baked module.
            if (!ownsDisabledModule && target.particleCount == 0 && target.time == 0)
            {
                int count, minimum;
                string error = Validate(out count, out minimum);
                if (error == null) { Arm(count, minimum); return; }
                Fail(error);
            }
            else Fail("Enable requires an empty initial system or an existing captured tail.");
            armed = false;
        }

        // Intentionally no OnDisable restore: that would double living particles' velocity.
        public bool PrepareForPlay()
        {
            if (!target) target = GetComponent<ParticleSystem>();
            if (!target || !target.isStopped || target.particleCount != 0)
                return Fail("PrepareForPlay requires a stopped, empty system.");
            int count, minimum;
            string error = Validate(out count, out minimum);
            if (error != null) return Fail(error);
            // Only restore a switch this instance actually owns, after all checks pass.
            if (ownsDisabledModule)
            {
                var velocity = target.velocityOverLifetime;
                velocity.enabled = true;
            }
            ownsDisabledModule = false;
            Arm(count, minimum);
            return true;
        }

        private void Arm(int count, int minimum)
        {
            expectedCount = count;
            minimumCount = minimum;
            preparedCountMode = target.emission.GetBurst(0).count.mode;
            observedPlaying = false;
            if (buffer == null || buffer.Length != count) buffer = new ParticleSystem.Particle[count];
            CapturedParticleCount = 0;
            CaptureTime = 0;
            LastFailure = null;
            armed = true;
        }

        private void LateUpdate()
        {
            if (!armed || !target) return;
            observedPlaying |= target.isPlaying || target.time > 0;
            if (target.particleCount == 0)
            {
                // A zero draw is valid, but must not leak permission into an active replay.
                // Do not consume an unplayed preparation or a paused/delayed system.
                if (observedPlaying && target.isStopped) armed = false;
                return;
            }
            if (!target.isPlaying || target.isPaused) return;
            int currentCount, currentMinimum;
            string error = Validate(out currentCount, out currentMinimum);
            if (error != null || currentCount != expectedCount || currentMinimum != minimumCount || target.emission.GetBurst(0).count.mode != preparedCountMode) { Fail(error ?? "Burst changed after preparation."); return; }
            int actualCount = target.particleCount;
            if (actualCount < minimumCount || actualCount > expectedCount || target.GetParticles(buffer) != actualCount)
            {
                armed = false;
                Fail("Only the complete, single authored burst can be captured.");
                return;
            }
            // Validate the complete write set first. Do not partially change particles.
            for (int i = 0; i < actualCount; i++)
            {
                Vector3 velocity = buffer[i].velocity + buffer[i].animatedVelocity;
                if (!Finite(velocity)) { armed = false; Fail("Non-finite captured velocity."); return; }
                buffer[i].velocity = velocity;
            }
            var module = target.velocityOverLifetime;
            module.enabled = false;
            ownsDisabledModule = true;
            target.SetParticles(buffer, actualCount);
            CaptureTime = target.time;
            CapturedParticleCount = actualCount;
            armed = false;
        }

        private string Validate(out int count, out int minimum)
        {
            count = minimum = 0;
            var main = target.main;
            var emission = target.emission;
            var velocity = target.velocityOverLifetime;
            if (main.simulationSpace != ParticleSystemSimulationSpace.World || main.loop || main.prewarm ||
                !(Constant(main.startDelay, 0) || (allowConstantStartDelay && FiniteConstant(main.startDelay) && main.startDelay.constant >= 0)) || !Constant(main.startSpeed, 0) ||
                !ValidStartLifetime(main.startLifetime) ||
                !Finite(main.simulationSpeed) || main.simulationSpeed <= 0 || target.shape.enabled)
                return "Requires a finite World one-shot with no shape/start speed; fixed start delay requires explicit opt-in.";
            if (!emission.enabled || !Constant(emission.rateOverTime, 0) || !Constant(emission.rateOverDistance, 0) || emission.burstCount != 1)
                return "Requires one time-zero burst and no continuous emission.";
            var burst = emission.GetBurst(0);
            bool randomCount = allowRandomBurstCount && burst.count.mode == ParticleSystemCurveMode.TwoConstants;
            float low = randomCount ? burst.count.constantMin : burst.count.constant;
            float high = randomCount ? burst.count.constantMax : burst.count.constant;
            if (burst.time != 0 || burst.cycleCount != 1 || burst.probability != 1 ||
                (!randomCount && burst.count.mode != ParticleSystemCurveMode.Constant) ||
                !Finite(low) || !Finite(high) || low < 0 || high <= 0 || low > high ||
                low != Mathf.Floor(low) || high != Mathf.Floor(high) || high > main.maxParticles)
                return "Requires a positive fixed burst or an explicitly enabled integer count range, with probability one.";
            count = (int)high;
            minimum = (int)low;
            if ((!velocity.enabled && !ownsDisabledModule) || velocity.space != ParticleSystemSimulationSpace.Local ||
                !Range(velocity.x) || !Range(velocity.y) || !Range(velocity.z) || !Constant(velocity.speedModifier, 1) ||
                !Constant(velocity.orbitalX, 0) || !Constant(velocity.orbitalY, 0) || !Constant(velocity.orbitalZ, 0) ||
                !Constant(velocity.orbitalOffsetX, 0) || !Constant(velocity.orbitalOffsetY, 0) || !Constant(velocity.orbitalOffsetZ, 0) || !Constant(velocity.radial, 0))
                return "Only constant random XYZ local linear velocity is supported.";
            if (target.inheritVelocity.enabled || target.limitVelocityOverLifetime.enabled || target.noise.enabled ||
                target.externalForces.enabled || target.collision.enabled || target.subEmitters.enabled)
                return "Additional velocity, collision or sub-emitter modules are unsupported.";
            var force = target.forceOverLifetime;
            if (force.enabled && (force.space != ParticleSystemSimulationSpace.World || force.randomized ||
                !FiniteConstant(force.x) || !FiniteConstant(force.y) || !FiniteConstant(force.z)))
                return "Only constant World force is supported.";
            return null;
        }

        private bool ValidStartLifetime(ParticleSystem.MinMaxCurve curve)
            => (FiniteConstant(curve) && curve.constant > 0) ||
               (allowRandomStartLifetime && Range(curve) && curve.constantMin > 0 && curve.constantMax > 0);

        private bool Fail(string message) { armed = false; LastFailure = message; return false; }
        private static bool Constant(ParticleSystem.MinMaxCurve curve, float value)
            => curve.mode == ParticleSystemCurveMode.Constant && curve.constant == value;
        private static bool FiniteConstant(ParticleSystem.MinMaxCurve curve)
            => curve.mode == ParticleSystemCurveMode.Constant && Finite(curve.constant);
        private static bool Range(ParticleSystem.MinMaxCurve curve)
            => curve.mode == ParticleSystemCurveMode.TwoConstants && Finite(curve.constantMin) && Finite(curve.constantMax);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
