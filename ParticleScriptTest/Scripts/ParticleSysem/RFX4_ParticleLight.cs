using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
[RequireComponent(typeof(ParticleSystem))]
public class RFX4_ParticleLight : MonoBehaviour
{
    public float LightIntencityMultiplayer = 1;
    public bool UseShadows = false;
    public int LightsLimit = 10;

    ParticleSystem ps;
    ParticleSystem.Particle[] particles;
    Light[] lights;

   

    bool isLocalSpace;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        var main = ps.main;
        if (main.maxParticles > LightsLimit) main.maxParticles = LightsLimit;
        particles = new ParticleSystem.Particle[main.maxParticles];
        isLocalSpace = ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;

        lights = new Light[main.maxParticles];

        for (int i = 0; i < lights.Length; i++)
        {
            var lightGO = new GameObject("ParticleLight" + i);
            lightGO.hideFlags = HideFlags.DontSave;
            lights[i] = lightGO.AddComponent<Light>();
            lights[i].transform.parent = transform;
            lights[i].intensity = 0;
            lights[i].shadows = UseShadows ? LightShadows.Soft : LightShadows.None;
        }
    }

    void Update()
    {
        int count = ps.GetParticles(particles);
        for (int i = 0; i < count; i++)
        {
            lights[i].gameObject.SetActive(true);
            lights[i].transform.position = isLocalSpace ? ps.transform.TransformPoint(particles[i].position) : particles[i].position; ;
            lights[i].color = particles[i].GetCurrentColor(ps);
            lights[i].range = particles[i].GetCurrentSize(ps);
            lights[i].intensity = particles[i].GetCurrentColor(ps).a / 255f * LightIntencityMultiplayer;
            lights[i].shadows = UseShadows ? LightShadows.Soft : LightShadows.None;
            if (lights[i].intensity < 0.01f) lights[i].gameObject.SetActive(false);
        }
        for (int i = count; i < particles.Length; i++)
        {
            lights[i].gameObject.SetActive(false);
        }
    }
}