using System;
using UnityEngine;

namespace KFCommonUtilityLib
{
    /// <summary>Opt-in finite World-space Cylinder birth adapter. Uses an independent
    /// seed and a scoped UE(y,z,x) basis. Normalization is mathematical, not native
    /// RSQRTSS bit equivalence. Play/Stop do not reset; OnEnable/ResetSequence do.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ParticleSystem))]
    public sealed class KFParticleCylinderBirth : MonoBehaviour
    {
        public float radius = 25, height = 5, velocityScale = 20, unitScale = .01f;
        public Vector3 startLocation;
        public bool addInitialVelocity;
        public Vector3 initialVelocityMin, initialVelocityMax;
        // Single-impact receiver only: sample the source emitter-time acceleration
        // once, not once per normalized particle age or over an existing tail.
        public bool captureEmitterAcceleration;
        public AnimationCurve accelerationZ = AnimationCurve.Linear(0,0,1,100);
        public int heightAxis = 2;
        public int directionFlags = 0x2f;
        public bool surfaceOnly = true, radialVelocity = true, adjustForWorldSpace = true, velocityEnabled = true;
        public uint initialSeed = 17;
        private ParticleSystem particles;
        private uint state;
        public uint CurrentSeed { get { return state; } }
        public long RequestedParticleCount { get; private set; }
        public long DirectionAttempts { get; private set; }

        // Config and matrix are captured once for the whole collision callback.
        internal struct Prepared
        {
            internal ParticleSystem target;
            internal Matrix4x4 matrix;
            internal Vector3 start, offsetBound, speedBound, initialMin, initialMax;
            internal float radius, halfHeight, scale, units, acceleration;
            internal bool setAcceleration;
            internal int axis, flags;
            internal bool surface, radial, adjust, velocity, initialVelocity;
        }
        private void OnEnable() { ResetSequence(initialSeed); }
        public void ResetSequence(uint seed) { state = seed; RequestedParticleCount = 0; DirectionAttempts = 0; }
        private ParticleSystem Target() { if (!particles) particles = GetComponent<ParticleSystem>(); return particles; }
        public bool IsValidFor(ParticleSystem target) { Prepared p; return TryPrepare(target, out p); }

        internal bool TryPrepare(ParticleSystem target, out Prepared p)
        {
            p = default(Prepared);
            if (!enabled || !target || Target() != target || heightAxis < 0 || heightAxis > 2 || (directionFlags & ~63) != 0 ||
                !Finite(radius) || radius < 0 || !Finite(height) || height < 0 || !Finite(velocityScale) ||
                !Finite(unitScale) || unitScale <= 0 || !Finite(startLocation)) return false;
            var main = target.main;
            if (main.simulationSpace != ParticleSystemSimulationSpace.World || target.shape.enabled ||
                main.startSpeed.mode != ParticleSystemCurveMode.Constant || main.startSpeed.constant != 0) return false;
            // Conservative absolute-sum bounds guard intermediate float operations,
            // not merely a double result after cancellation. Margin covers rounding.
            const double limit = (double)float.MaxValue / 16;
            if (2 * (double)radius * radius > limit) return false;
            Vector3 initialBound=Vector3.zero;
            if (addInitialVelocity) for (int i=0;i<3;i++)
            {
                double b=Math.Abs((double)initialVelocityMin[i])+Math.Abs((double)initialVelocityMax[i]-initialVelocityMin[i]);
                if(!Finite(initialVelocityMin[i])||!Finite(initialVelocityMax[i])||b>limit)return false;
                initialBound[i]=(float)b;
            }
            initialBound=Basis(initialBound);
            float acceleration=0;
            if(captureEmitterAcceleration)
            {
                if(target.particleCount!=0||accelerationZ==null)return false;
                acceleration=accelerationZ.Evaluate(target.time)*unitScale;
                if(!Finite(acceleration))return false;
            }
            Matrix4x4 matrix = target.transform.localToWorldMatrix;
            Vector3 localBound = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                double b = Math.Abs((double)startLocation[i]) + (i == heightAxis ? (double)height * .5 : radius) * 1.001;
                if (b > limit) return false;
                localBound[i] = (float)b;
                for (int j = 0; j < 3; j++) if (!Finite(matrix[i,j])) return false;
            }
            Vector3 input = Basis(localBound), center = Basis(startLocation), outputBound = Vector3.zero, speedBound = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                double transformed = 0, transformedCenter = 0;
                for (int j = 0; j < 3; j++)
                {
                    transformed += Math.Abs((double)matrix[i,j]) * input[j];
                    transformedCenter += Math.Abs((double)matrix[i,j]) * Math.Abs((double)center[j]);
                }
                double centerBound = adjustForWorldSpace ? transformedCenter : Math.Abs((double)center[i]);
                double difference = transformed + centerBound;
                double extra=0;for(int j=0;j<3;j++)extra+=Math.Abs((double)matrix[i,j])*initialBound[j];
                double speed = (velocityEnabled?difference * Math.Abs((double)velocityScale):0)+extra;
                if (transformed > limit || transformedCenter > limit || difference > limit ||
                    transformed * unitScale > limit || speed > limit || speed * unitScale > limit) return false;
                outputBound[i] = (float)(transformed * unitScale * 1.001);
                speedBound[i] = (float)(speed * unitScale * 1.001);
            }
            p = new Prepared { target=target, matrix=matrix, start=startLocation, offsetBound=outputBound, speedBound=speedBound,
                radius=radius, halfHeight=height*.5f, scale=velocityScale, units=unitScale, axis=heightAxis, flags=directionFlags,
                surface=surfaceOnly, radial=radialVelocity, adjust=adjustForWorldSpace, velocity=velocityEnabled,
                initialVelocity=addInitialVelocity, initialMin=initialVelocityMin, initialMax=initialVelocityMax,
                setAcceleration=captureEmitterAcceleration, acceleration=acceleration };
            return true;
        }

        internal static bool CanEmit(ParticleSystem.EmitParams properties, Prepared p)
        {
            if (!Finite(properties.position) || !Finite(properties.velocity)) return false;
            for (int i = 0; i < 3; i++)
                if (Math.Abs((double)properties.position[i]) + p.offsetBound[i] > (double)float.MaxValue / 4 ||
                    ((p.velocity || p.initialVelocity) && Math.Abs((double)properties.velocity[i]) + p.speedBound[i] > (double)float.MaxValue / 4)) return false;
            return true;
        }
        public bool TryEmit(ParticleSystem.EmitParams properties, int count, KFParticleUniformRangeBirthSize birthSize = null)
        {
            Prepared p;
            if (count <= 0 || !gameObject.activeInHierarchy || !TryPrepare(Target(), out p) || !CanEmit(properties,p) ||
                (birthSize && !birthSize.IsValidFor(p.target))) return false;
            EmitPrepared(properties,count,birthSize,p);
            return true;
        }
        internal void EmitPrepared(ParticleSystem.EmitParams properties, int count, KFParticleUniformRangeBirthSize birthSize, Prepared p)
        {
            if(p.setAcceleration)
            {
                var force=p.target.forceOverLifetime;force.enabled=true;force.space=ParticleSystemSimulationSpace.World;
                force.x=0;force.y=p.acceleration;force.z=0;
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 offset, delta; Sample(p, out offset, out delta);
                var emission = properties; // Do not leak one receiver's overrides.
                emission.position = properties.position + offset;
                emission.applyShapeToPosition = false;
                if (p.velocity || p.initialVelocity) emission.velocity = properties.velocity + delta;
                if (birthSize) emission.startSize3D = birthSize.SamplePreparedSize();
                p.target.Emit(emission,1);
                RequestedParticleCount++;
                if (birthSize) birthSize.RecordSubmittedParticle();
            }
        }
        private void Sample(Prepared p, out Vector3 offset, out Vector3 velocity)
        {
            int h=p.axis, a=h==0?1:0, b=h==2?1:2, attempts=0;
            Vector3 d;
            do
            {
                d=new Vector3(Draw(),Draw(),Draw());
                for(int i=0;i<3;i++)
                {
                    bool positive=(p.flags&(1<<i))!=0, negative=(p.flags&(8<<i))!=0;
                    d[i]=positive?(negative?d[i]*2-1:d[i]):(negative?-d[i]:0);
                }
                attempts++;
                float x=p.radius*d[a], y=p.radius*d[b];
                if(y*y+x*x<=p.radius*p.radius || attempts==51) break;
            } while(true);
            DirectionAttempts+=attempts;
            Vector3 n=d;
            float sum=d.x*d.x+d.y*d.y+d.z*d.z;
            if(sum>1e-8f)n=d*(1/Mathf.Sqrt(sum));
            float radial=d[b]*d[b]+d[a]*d[a];
            if(radial>1e-8f){float inverse=1/Mathf.Sqrt(radial);n[a]=d[a]*inverse;n[b]=d[b]*inverse;}
            else {n[a]=0;n[b]=0;}
            Vector3 point=Vector3.zero;point[h]=d[h]*p.halfHeight;
            bool wall=p.surface&&Mathf.Abs(Mathf.Abs(point[h])-p.halfHeight)>=1e-8f;
            point[a]=p.radius*(wall?n[a]:d[a]);point[b]=p.radius*(wall?n[b]:d[b]);
            Vector3 bound=Vector3.zero;bound[a]=Mathf.Abs(n[a])*p.radius;bound[b]=Mathf.Abs(n[b])*p.radius;bound[h]=p.halfHeight;
            for(int i=0;i<3;i++)point[i]=Mathf.Clamp(point[i],-bound[i],bound[i])+p.start[i];
            Vector3 transformed=p.matrix.MultiplyVector(Basis(point));
            Vector3 center=Basis(p.start);if(p.adjust)center=p.matrix.MultiplyVector(center);
            Vector3 difference=transformed-center;
            if(p.radial&&!p.adjust)difference[h==0?2:h==1?0:1]=0;
            offset=transformed*p.units;
            Vector3 initial=Vector3.zero;
            if(p.initialVelocity)
            {
                for(int i=0;i<3;i++)initial[i]=p.initialMin[i]+(p.initialMax[i]-p.initialMin[i])*Draw();
                initial=p.matrix.MultiplyVector(Basis(initial));
            }
            velocity=((p.velocity?difference*p.scale:Vector3.zero)+initial)*p.units;
        }
        private float Draw() { state=unchecked(state*0x0bb38435u+0x3619636bu);return (state&0x007fffffu)/8388608f; }
        private static Vector3 Basis(Vector3 v) { return new Vector3(v.y,v.z,v.x); }
        private static bool Finite(float v) { return !float.IsNaN(v)&&!float.IsInfinity(v); }
        private static bool Finite(Vector3 v) { return Finite(v.x)&&Finite(v.y)&&Finite(v.z); }
    }
}