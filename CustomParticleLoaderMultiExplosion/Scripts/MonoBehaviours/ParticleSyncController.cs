using UnityEngine;

public class ParticleSyncController : TrackedBehaviourBase
{
    private ParticleSystem ps;
    private int seed;
    private bool loop;
    private float elapsedTime = 0f;
    private GameRandom rnd;

    protected override void Awake()
    {
        syncOnInit = true;
        syncOnConnect = CustomParticleEffectLoader.LastInitializedComponent.SyncOnConnect;
        base.Awake();
        seed = Random.Range(0, int.MaxValue);
        ps = GetComponent<ParticleSystem>();
        loop = ps.main.loop;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.useAutoRandomSeed = false;
        ps.randomSeed = (uint)seed;
        if (isServer)
        {
            rnd = GameRandomManager.Instance.CreateGameRandom(seed);
            ps.Simulate(0, true, true, true);
            ps.Play();
        }
    }

    void FixedUpdate()
    {
        elapsedTime += Time.fixedDeltaTime;
        if (!loop)
            return;

        if(elapsedTime >= ps.main.duration)
        {
            ps.Stop();
            seed = rnd.RandomInt;
            ps.randomSeed = (uint)seed;
            ps.Simulate(0, false, true, true);
            ps.Play();
        }
    }

    protected override void OnExplosionInitServer(PooledBinaryWriter _bw)
    {
        _bw.Write(seed);
        //Log.Out("Seed: " + seed);
    }

    protected override void OnExplosionInitClient(PooledBinaryReader _br)
    {
        seed = _br.ReadInt32();
        //Log.Out("Seed: " + seed);
        ps.randomSeed = (uint)seed;
        rnd = GameRandomManager.Instance.CreateGameRandom(seed);
        ps.Simulate(0, true, true, true);
        ps.Play();
    }

    protected override void OnClientConnected(PooledBinaryWriter _bw)
    {
        _bw.Write(seed);
        _bw.Write(elapsedTime);
    }

    protected override void OnConnectedToServer(PooledBinaryReader _br)
    {
        seed = _br.ReadInt32();
        elapsedTime = _br.ReadSingle();
        rnd = GameRandomManager.Instance.CreateGameRandom(seed);
        ps.randomSeed = (uint)seed;
        ps.Simulate(elapsedTime, true, true, true);
        ps.Play();
    }
    /*
    public bool isSubemitter = false;
    private float startDelay;
    private float duration;
    private ParticleSystem.MinMaxCurve rateOverTime;
    private ParticleSystem.MinMaxCurve rateOverDistance;
    private ParticleSystem.Burst[] bursts = null;
    private ParticleSystem.Particle[] particles = null;
    private SubParticleJob job;
    private float elapsedTime = 0;
    private float deltaOverTime = 0;
    private float deltaOverDistance = 0;
    private Vector3 lastPosition;
    private bool loop;

    protected override void Awake()
    {
        key = PlatformIndependentHash.StringToInt32(transform.name);
        base.Awake();
        ps = transform.GetComponent<ParticleSystem>();
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
        {
            enabled = false;
            return;
        }
        ParticleSystem.MainModule main = ps.main;
        particles = new ParticleSystem.Particle[ps.main.maxParticles];
        loop = main.loop;
        duration = main.duration;
        startDelay = main.startDelay.constant;
        rateOverTime = emission.rateOverTime;
        rateOverDistance = emission.rateOverDistance;
        int burstCount = emission.burstCount;
        if (burstCount > 0)
        {
            bursts = new ParticleSystem.Burst[burstCount];
            emission.GetBursts(bursts);
            for (int i = 0; i < burstCount; ++i)
            {
                ParticleSystem.Burst burst = bursts[i];
                //burst cycle won't repeat percisely with interval longer than 0.021s
                if (burst.repeatInterval < 0.021f)
                    burst.repeatInterval = 0.021f;
            }
        }
        lastPosition = transform.position;
    }

    public void setSubemitter()
    {
        var emission = ps.emission;
        if(enabled)
        {
            isSubemitter = true;
            emission.enabled = true;
            job = new SubParticleJob(explId, (int)key);
        }else
        {
            emission.SetBursts(new ParticleSystem.Burst[] { });
            emission.rateOverDistance = 0;
            emission.rateOverTime = 0;
        }
    }

    void FixedUpdate()
    {
        if (isSubemitter)
            return;


        if(elapsedTime > startDelay)
        {
            float startTime = elapsedTime - startDelay;
            float normalizedTime = startTime / duration;
            float curDeltaOverTime = deltaOverTime + rateOverTime.Evaluate(normalizedTime);
            float curDeltaOverDistance = deltaOverDistance + rateOverDistance.Evaluate(normalizedTime) * (transform.position - lastPosition).magnitude;
            int emitCount = Mathf.FloorToInt(curDeltaOverTime) - Mathf.FloorToInt(deltaOverTime) + Mathf.FloorToInt(curDeltaOverDistance) - Mathf.FloorToInt(deltaOverDistance);

            if (bursts != null)
            {
                foreach (ParticleSystem.Burst burst in bursts)
                {
                    for (int i = 0; i < burst.cycleCount; ++i)
                    {
                        float burstTime = burst.time + i * burst.repeatInterval;
                        if (startTime < burstTime)
                            break;
                        if (startTime >= burstTime && startTime - burstTime < Time.fixedDeltaTime)
                        {
                            if (Random.Range(0, 1) < burst.probability)
                                emitCount += Mathf.FloorToInt(burst.count.Evaluate(normalizedTime));
                            break;
                        }
                    }
                }
            }

            if (emitCount > 0)
                EmitServer(emitCount);

            deltaOverTime = curDeltaOverTime;
            deltaOverDistance = curDeltaOverDistance;
        }

        lastPosition = transform.position;
        elapsedTime += Time.fixedDeltaTime;

        if (loop && elapsedTime > duration + startDelay)
        {
            elapsedTime = 0;
            deltaOverTime = 0;
            deltaOverDistance = 0;
        }
    }

    void OnParticleUpdateJobScheduled()
    {
        if (enabled && isSubemitter && SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
            job.Schedule(ps);
    }

    void EmitServer(int count)
    {
        ps.Emit(count);
        if (SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
        {
            int aliveCount = ps.GetParticles(particles);
            List<ExplosionParticleSyncParams> list_params = new List<ExplosionParticleSyncParams>();
            for (int i = 0; i < aliveCount; ++i)
            {
                ParticleSystem.Particle particle = particles[i];
                if (particle.startLifetime > particle.remainingLifetime)
                    continue;
                list_params.Add(new ExplosionParticleSyncParams(particle));
            }
            if (list_params.Count > 0)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageExplosionParticleSyncParams>().Setup(explId, (int)key, list_params));
        }
    }

    public void EmitClient(List<ExplosionParticleSyncParams> list_params)
    {
        ParticleSystem.EmitParams emitParam = new ParticleSystem.EmitParams();
        foreach (ExplosionParticleSyncParams param in list_params)
        {
            Log.Out("emit client!");
            param.set(emitParam);
            ps.Emit(emitParam, 1);
        }
    }

    private struct SubParticleJob : IJobParticleSystem
    {
        private uint explId;
        private int name;

        public SubParticleJob(uint id, int name)
        {
            explId = id;
            this.name = name;
        }

        void IJobParticleSystem.Execute(ParticleSystemJobData particles)
        {
            List<ExplosionParticleSyncParams> list_params = new List<ExplosionParticleSyncParams>();
            for (int i = 0; i < particles.count; ++i)
            {
                Log.Out(particles.aliveTimePercent[i].ToString());
                if (particles.aliveTimePercent[i] > Time.fixedDeltaTime * particles.inverseStartLifetimes[i] * 100)
                    continue;

                list_params.Add(new ExplosionParticleSyncParams(particles, i));
            }
            if (list_params.Count > 0)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageExplosionParticleSyncParams>().Setup(explId, name, list_params));
        }
    }

    public struct ExplosionParticleSyncParams
    {
        public ExplosionParticleSyncParams(ParticleSystem.Particle particle)
        {
            this.position = particle.position;
            this.rotation3D = particle.rotation3D;
            this.startSize3D = particle.startSize3D;
            this.velocity = particle.velocity;
            this.startLifetime = particle.startLifetime;
        }

        public ExplosionParticleSyncParams(ParticleSystemJobData data, int index)
        {
            this.position = data.positions[index];
            this.rotation3D = data.rotations[index];
            this.startSize3D = data.sizes[index];
            this.velocity = data.velocities[index];
            this.startLifetime = 1 / data.inverseStartLifetimes[index];
        }

        public void write(BinaryWriter _bw)
        {
            StreamUtilsCompressed.Write(_bw, position);
            StreamUtilsCompressed.Write(_bw, rotation3D);
            StreamUtilsCompressed.Write(_bw, startSize3D);
            StreamUtilsCompressed.Write(_bw, velocity);
            _bw.Write(startLifetime);
        }

        public void set(ParticleSystem.EmitParams param)
        {
            param.position = position;
            param.rotation3D = rotation3D;
            param.startSize3D = startSize3D;
            param.velocity = velocity;
            param.startLifetime = startLifetime;
        }

        public static ExplosionParticleSyncParams Create(BinaryReader _br)
        {
            ExplosionParticleSyncParams param = new ExplosionParticleSyncParams();
            param.position = StreamUtilsCompressed.ReadHalfVector3(_br);
            param.rotation3D = StreamUtilsCompressed.ReadHalfVector3(_br);
            param.startSize3D = StreamUtilsCompressed.ReadHalfVector3(_br);
            param.velocity = StreamUtilsCompressed.ReadHalfVector3(_br);
            param.startLifetime = _br.ReadSingle();
            return param;
        }

        private Vector3 position;
        private Vector3 rotation3D;
        private Vector3 startSize3D;
        private Vector3 velocity;
        private float startLifetime;
    }
    */
}
