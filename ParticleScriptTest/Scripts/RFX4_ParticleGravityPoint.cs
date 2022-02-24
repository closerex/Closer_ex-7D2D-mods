using UnityEngine;

[ExecuteInEditMode]
public class RFX4_ParticleGravityPoint : MonoBehaviour
{
    public Transform target;
    public float Force = 1;
    public float StopDistance = -1;

    ParticleSystem ps;
    ParticleSystem.Particle[] particles;

    ParticleSystem.MainModule mainModule;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        mainModule = ps.main;
    }

    void LateUpdate()
    {
        var maxParticles = mainModule.maxParticles;

        if (particles == null || particles.Length < maxParticles)
        {
            particles = new ParticleSystem.Particle[maxParticles];
        }

        int particleCount = ps.GetParticles(particles);
        
        float forceDeltaTime = Time.deltaTime * Force;
       
        var targetTransformedPosition = Vector3.zero;

        if(mainModule.simulationSpace == ParticleSystemSimulationSpace.Local)
            targetTransformedPosition = transform.InverseTransformPoint(target.position);
        if(mainModule.simulationSpace == ParticleSystemSimulationSpace.World)
            targetTransformedPosition = target.position;
       
        for (int i = 0; i < particleCount; i++)
        {
            var directionToTarget = Vector3.Normalize(targetTransformedPosition - particles[i].position);
            var seekForce = directionToTarget * forceDeltaTime;
            if (StopDistance > 0 && (particles[i].position - target.position).magnitude < StopDistance) {
                particles[i].velocity = Vector3.zero;
            }
            else particles[i].velocity += seekForce;
        }

        ps.SetParticles(particles, particleCount);
    }
}
