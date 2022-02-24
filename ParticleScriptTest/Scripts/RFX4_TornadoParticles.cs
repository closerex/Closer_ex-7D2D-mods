using UnityEngine;

public class RFX4_TornadoParticles : MonoBehaviour
{

    public Material TornadoMaterial;

    private ParticleSystem.Particle[] particleArray;
    private ParticleSystem particleSys;
    private Light myLight;

    private Vector4 _twistScale;
    private int materialID = -1;
    
	// Use this for initialization
	void Start () {
        particleSys = GetComponent<ParticleSystem>();
	    myLight = GetComponent<Light>();
#if !UNITY_5_5_OR_NEWER
        if (particleSys!=null)
            particleArray = new ParticleSystem.Particle[particleSys.maxParticles];
#else
        if (particleSys != null)
            particleArray = new ParticleSystem.Particle[particleSys.main.maxParticles];
#endif

        if (TornadoMaterial.HasProperty("_TwistScale"))
	        materialID = Shader.PropertyToID("_TwistScale");
	    else {
            Debug.Log(TornadoMaterial.name + " not have property twist");
	    }
        if (materialID != -1)
            _twistScale = TornadoMaterial.GetVector(materialID);
	}
	
	// Update is called once per frame
    private void Update()
    {
        if (materialID != -1)
            _twistScale = TornadoMaterial.GetVector(materialID);
        if (particleSys!=null) {
            var numParticlesAlive = particleSys.GetParticles(particleArray);

            for (int i = 0; i < numParticlesAlive; i++) {
                var pos = particleArray[i].position;

                var height = (pos.y) * _twistScale.y;
                pos.x = Mathf.Sin(Time.time * _twistScale.z + pos.y * _twistScale.x) * height;
                pos.z = Mathf.Sin(Time.time * _twistScale.z + pos.y * _twistScale.x + 3.1415f / 2) * height;
                particleArray[i].position = pos;

                particleSys.SetParticles(particleArray, numParticlesAlive);
            }
        }
        if (myLight!=null) {
            var pos = transform.localPosition;
            var height = pos.y * _twistScale.y;
            pos.x = Mathf.Sin(Time.time * _twistScale.z + pos.y * _twistScale.x) * height;
            pos.z = Mathf.Sin(Time.time * _twistScale.z + pos.y * _twistScale.x + 3.1415f / 2) * height;
            transform.localPosition = pos;
        }
    }

}
