using UnityEngine;

namespace KFCommonUtilityLib
{
    /// <summary>Ion FPV: the Super_Attack end-trail cue (EndKFAnimTrail 14) stops the flame layers of the
    /// one-shots this weapon's AnimationParticleSpawner created. Event driven only; it reads the spawner's
    /// own instance list instead of scanning the scene, and never polls the Animator.</summary>
    [DisallowMultipleComponent]
    public sealed class KFIonTransientFlameShutdown : MonoBehaviour
    {
        [SerializeField] private AnimationParticleSpawner spawner;

        public void EndKFAnimTrail(AnimationEvent notification)
        {
            if (notification.intParameter != 14 || !spawner) return;
            foreach (var owner in spawner.OneShots)
            {
                if (!owner) continue;
                bool ignite = owner.name.StartsWith("ignite(Clone)", System.StringComparison.Ordinal);
                bool slash = owner.name.StartsWith("ignite_slash_oneshot(Clone)", System.StringComparison.Ordinal);
                if (!ignite && !slash) continue;
                foreach (var ps in owner.GetComponentsInChildren<ParticleSystem>(true))
                {
                    bool fire = ignite ? (ps.name == "flame_top" || ps.name == "Native_Ignite_14_Flame" || ps.name == "flame_base_1" || ps.name == "flame_base_2")
                        : (ps.name == "flame_top_1" || ps.name == "flame_top_2");
                    if (fire) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }
}
