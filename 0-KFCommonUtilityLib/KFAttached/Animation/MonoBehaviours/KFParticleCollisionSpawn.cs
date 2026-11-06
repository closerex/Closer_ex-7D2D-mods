using System;
using System.Collections.Generic;
using UnityEngine;

namespace KFCommonUtilityLib
{
    /// <summary>
    /// Collision-event receiver subset for explicitly configured world-space effects.
    /// Uses Unity collision intersections; does not emulate KF2 collision detection.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class KFParticleCollisionSpawn : MonoBehaviour
    {
        [Serializable]
        public sealed class Receiver
        {
            public ParticleSystem particles;
            [Min(1)] public int count = 1;
            public KFParticleUniformRangeBirthSize birthSize; // Null preserves the original batch Emit path.
            public KFParticleCylinderBirth birthCylinder;
        }

        public Receiver[] receivers = Array.Empty<Receiver>();
        private readonly List<ParticleCollisionEvent> collisionEvents = new(4);
        private readonly List<KFParticleCylinderBirth.Prepared> prepared = new(4);
        private ParticleSystem source;
        private bool warnedConfiguration;

        public int CollisionEventCount { get; private set; }
        public int RequestedParticleCount { get; private set; }
        public Vector3 LastIntersection { get; private set; }

        private void Awake()
        {
            source = GetComponent<ParticleSystem>();
        }

        private void OnEnable()
        {
            warnedConfiguration = false;
        }

        private void OnParticleCollision(GameObject other)
        {
            // Collision messages can reach disabled behaviours. Existing tails remain alive.
            if (!isActiveAndEnabled || receivers == null || receivers.Length == 0) return;
            if (!source) source = GetComponent<ParticleSystem>();
            if (!ValidReceivers())
            {
                if (!warnedConfiguration)
                {
                    Debug.LogWarning("KFParticleCollisionSpawn requires distinct world-space receivers and positive counts.", this);
                    warnedConfiguration = true;
                }
                return;
            }

            int count = source.GetCollisionEvents(other, collisionEvents);
            // Validate every hit against the same matrix/config snapshot before any
            // receiver emits or advances either independent random stream.
            for (int i = 0; i < count; i++)
            {
                var check = new ParticleSystem.EmitParams { position = collisionEvents[i].intersection };
                for (int j = 0; j < receivers.Length; j++)
                    if (receivers[j].birthCylinder && receivers[j].particles.gameObject.activeInHierarchy &&
                        !KFParticleCylinderBirth.CanEmit(check, prepared[j])) return;
            }
            CollisionEventCount += count;
            for (int i = 0; i < count; i++)
            {
                LastIntersection = collisionEvents[i].intersection;
                var emit = new ParticleSystem.EmitParams
                {
                    position = LastIntersection,
                    applyShapeToPosition = true
                };
                for (int j = 0; j < receivers.Length; j++)
                {
                    var receiver = receivers[j];
                    if (!receiver.particles.gameObject.activeInHierarchy) continue;
                    // Explicit adapters only; legacy shape/velocity/lifetime/HDR stay intact.
                    if (receiver.birthCylinder)
                        receiver.birthCylinder.EmitPrepared(emit, receiver.count, receiver.birthSize, prepared[j]);
                    else if (receiver.birthSize)
                    {
                        if (!receiver.birthSize.TryEmit(emit, receiver.count)) continue;
                    }
                    else receiver.particles.Emit(emit, receiver.count);
                    RequestedParticleCount += receiver.count;
                }
            }
        }

        private bool ValidReceivers()
        {
            prepared.Clear();
            for (int i = 0; i < receivers.Length; i++)
            {
                var entry = receivers[i];
                if (entry == null || !entry.particles || entry.particles == source || entry.count <= 0 ||
                    entry.particles.main.simulationSpace != ParticleSystemSimulationSpace.World) return false;
                if (entry.birthSize && !entry.birthSize.IsValidFor(entry.particles)) return false;
                KFParticleCylinderBirth.Prepared snapshot = default(KFParticleCylinderBirth.Prepared);
                if (entry.birthCylinder && !entry.birthCylinder.TryPrepare(entry.particles, out snapshot)) return false;
                prepared.Add(snapshot);
                for (int j = 0; j < i; j++)
                    if (receivers[j].particles == entry.particles) return false;
            }
            return true;
        }
    }
}