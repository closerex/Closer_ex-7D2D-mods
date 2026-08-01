using Unity.Collections;

namespace UnityEngine.Animations.Rigging
{
    public struct FingerFABRIKJob : IWeightedAnimationJob
    {
        public ReadOnlyTransformHandle source;
        public ReadOnlyTransformHandle hint;
        public NativeArray<ReadWriteTransformHandle> bones;
        public NativeArray<float> lengths;
        public int iterations;
        public Vector3 upAxis;
        public Vector3 rightAxis;
        public Vector3 forwardAxis;
#if !NotEditor
        public NativeArray<Vector2Property> rotationRanges;
        public FloatProperty hintMinAngle;
        public FloatProperty hintMaxAngle;
        public FloatProperty tolerance;
#else
        public NativeArray<Vector2> rotationRanges;
        public float hintMinAngle;
        public float hintMaxAngle;
        public float tolerance;
#endif
        
        public NativeArray<Vector3> positionsCache, positionsFinal;
        public NativeArray<float> anglesCache, anglesFinal;
        public float totalLength;
        private bool wasOptimal;

        public FloatProperty jobWeight { get; set; }
        
        public void ProcessAnimation(AnimationStream stream)
        {
            AlignRootToHint(stream);
            AlignRootToTargetPlane(stream);
            SolveFABRIKWithPlane(stream);
            ApplyRotations(stream);
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        void AlignRootToHint(AnimationStream stream)
        {
            if (!hint.IsValid(stream)) return;

            var root = bones[0];
            root.GetGlobalTR(stream, out var rootPos, out var rootRot);
            var hintPos = hint.GetPosition(stream);

            Vector3 forward = rootRot * forwardAxis;
            Vector3 up = rootRot * upAxis;
            Vector3 toHint = (hintPos - rootPos).normalized;

            Vector3 projectedUp = AnimationRuntimeUtils.ProjectOnPlane(up, forward);
            Vector3 projectedHint = AnimationRuntimeUtils.ProjectOnPlane(toHint, forward);

            float angle = Vector3.SignedAngle(projectedUp, projectedHint, forward);
#if !NotEditor
            angle = Mathf.Clamp(angle, hintMinAngle.Get(stream), hintMaxAngle.Get(stream));
#else
            angle = Mathf.Clamp(angle, hintMinAngle, hintMaxAngle);
#endif
            root.SetRotation(stream, Quaternion.AngleAxis(angle, forward) * rootRot);
        }
        
        void AlignRootToTargetPlane(AnimationStream stream)
        {
            var root = bones[0];
            root.GetGlobalTR(stream, out var rootPos, out var rootRot);
            var targetPos = source.GetPosition(stream);

            Vector3 forward = rootRot * forwardAxis;
            Vector3 up = rootRot * upAxis;
            Vector3 toTarget = (targetPos - rootPos).normalized;

            Vector3 projectedForward = AnimationRuntimeUtils.ProjectOnPlane(forward, up);
            Vector3 projectedTarget = AnimationRuntimeUtils.ProjectOnPlane(toTarget, up);

            float angle = Vector3.SignedAngle(projectedForward, projectedTarget, up);

            root.SetRotation(stream, Quaternion.AngleAxis(angle, up) * rootRot);
        }

        void SolveFABRIKWithPlane(AnimationStream stream)
        {
            int count = bones.Length;
            var root = bones[0];
            root.GetGlobalTR(stream, out var rootPos, out var rootRot);
            Vector3 planeNormal = rootRot * rightAxis;
            Vector3 rootForward = rootRot * forwardAxis;
            var targetPos = source.GetPosition(stream);
            float maxDistance = Vector3.Distance(targetPos, rootPos);
            int realIterations = maxDistance >= totalLength ? 1 : iterations;

            positionsCache[0] = rootPos;
            for (int i = 1; i < count; i++)
            {
                if (wasOptimal)
                {
                    positionsCache[i] = positionsCache[i - 1] + Quaternion.AngleAxis(anglesFinal[i - 1], planeNormal) * rootForward * lengths[i - 1];
                }
                else
                {
                    positionsCache[i] = AnimationRuntimeUtils.ProjectOnPlane(bones[i].GetPosition(stream) - rootPos, planeNormal) + rootPos;
                }
            }

            if (wasOptimal)
            {
                float distance = (positionsCache[count - 1] - targetPos).sqrMagnitude;
#if !NotEditor
                if (distance < tolerance.Get(stream))
#else
                if (distance < tolerance)
#endif
                {
                    NativeArray<Vector3>.Copy(positionsCache, positionsFinal, bones.Length);
                    return;
                }
            }
            
            float minDistance = float.MaxValue;
            for (int iter = 0; iter < realIterations; iter++)
            {
                // backward
                positionsCache[count - 1] = targetPos;

                float parentAngle;
                for (int i = count - 2; i >= 0; i--)
                {
                    Vector3 dir = positionsCache[i + 1] - positionsCache[i];

                    Vector3 parentDir = i == 0 ? rootForward : positionsCache[i] - positionsCache[i - 1];
                    float curAngle = Vector3.SignedAngle(rootForward, dir, planeNormal);
                    parentAngle = Vector3.SignedAngle(rootForward, parentDir, planeNormal);
#if !NotEditor
                    Vector2 rotationRange = rotationRanges[i].Get(stream);
#else
                    Vector2 rotationRange = rotationRanges[i];
#endif
                    float diffAngle = Mathf.Clamp(curAngle - parentAngle, rotationRange.x, rotationRange.y);
                    float realAngle = parentAngle + diffAngle + 180;
                    dir = Quaternion.AngleAxis(realAngle, planeNormal) * rootForward;
                    positionsCache[i] = positionsCache[i + 1] + dir * lengths[i];
                }

                // forward
                positionsCache[0] = rootPos;
                parentAngle = 0;
                for (int i = 1; i < count; i++)
                {
                    Vector3 dir = (positionsCache[i] - positionsCache[i - 1]).normalized;
#if !NotEditor
                    Vector2 rotationRange = rotationRanges[i - 1].Get(stream);
#else
                    Vector2 rotationRange = rotationRanges[i - 1];
#endif
                    float diffAngle = Mathf.Clamp(Mathf.DeltaAngle(parentAngle, Vector3.SignedAngle(rootForward, dir, planeNormal)), rotationRange.x, rotationRange.y);
                    float realAngle = parentAngle + diffAngle;
                    dir = Quaternion.AngleAxis(realAngle, planeNormal) * rootForward;
                    positionsCache[i] = positionsCache[i - 1] + dir * lengths[i - 1];
                    anglesCache[i - 1] = parentAngle = realAngle;
                }

                float distance = (positionsCache[count - 1] - targetPos).sqrMagnitude;

                if (minDistance > distance)
                {
                    minDistance = distance;
                    NativeArray<Vector3>.Copy(positionsCache, positionsFinal, bones.Length);
                    NativeArray<float>.Copy(anglesCache, anglesFinal, bones.Length - 1);
                }
                
#if !NotEditor
                if (distance < tolerance.Get(stream))
#else
                if (distance < tolerance)
#endif
                {
                    wasOptimal = true;
                    break;
                }
                
                wasOptimal = false;
            }
        }

        void ApplyRotations(AnimationStream stream)
        {
            for (int i = 0; i < bones.Length - 1; i++)
            {
                var bone = bones[i];
                var boneRot = bone.GetRotation(stream);

                Vector3 currentForward = boneRot * forwardAxis;
                Vector3 desiredDir = (positionsFinal[i + 1] - positionsFinal[i]).normalized;

                Vector3 right = boneRot * rightAxis;

                float angle = Vector3.SignedAngle(currentForward, desiredDir, right);

                bone.SetRotation(stream, Quaternion.AngleAxis(angle, right) * boneRot);
            }

            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].SetPosition(stream, positionsFinal[i]);
            }
        }
    }

    public interface IFingerFABRIKData
    {
        Transform root { get; set; }
        Transform node1 { get; set; }
        Transform node2 { get; set; }
        Transform tip { get; set; }
        
        FingerFABRIKData.Axis forwardAxis { get; set; }
        FingerFABRIKData.Axis rightAxis { get; set; }
        FingerFABRIKData.Axis upAxis { get; set; }
        
        Vector2 rotationRange1 { get; set; }
        Vector2 rotationRange2 { get; set; }
        Vector2 rotationRange3 { get; set; }

        Transform target { get; set; }
        Transform hint { get; set; }
        
        float hintMinAngle { get; set; }
        float hintMaxAngle { get; set; }
        
        int iterations { get; set; }
        float tolerance { get; set; }
        
        string rotationRange1Name { get; }
        string rotationRange2Name { get; }
        string rotationRange3Name { get; }
        
        string hintMinAngleName { get; }
        string hintMaxAngleName { get; }
        
        string toleranceName { get; }
    }

    public class FingerFABRIKBinder<T> : AnimationJobBinder<FingerFABRIKJob, T>
        where T : struct, IAnimationJobData, IFingerFABRIKData
    {
        public override FingerFABRIKJob Create(Animator animator, ref T data, Component component)
        {
            var job = new FingerFABRIKJob();

            job.source = ReadOnlyTransformHandle.Bind(animator, data.target);
            if (data.hint != null)
            {
                job.hint = ReadOnlyTransformHandle.Bind(animator, data.hint);
            }
            job.bones = new NativeArray<ReadWriteTransformHandle>(new[]
            {
                ReadWriteTransformHandle.Bind(animator, data.root),
                ReadWriteTransformHandle.Bind(animator, data.node1),
                ReadWriteTransformHandle.Bind(animator, data.node2),
                ReadWriteTransformHandle.Bind(animator, data.tip)
            }, Allocator.Persistent);
#if !NotEditor
            job.rotationRanges = new NativeArray<Vector2Property>(new[]
            {
                Vector2Property.Bind(animator, component, data.rotationRange1Name),
                Vector2Property.Bind(animator, component, data.rotationRange2Name),
                Vector2Property.Bind(animator, component, data.rotationRange3Name)
            }, Allocator.Persistent);
            job.hintMaxAngle = FloatProperty.Bind(animator, component, data.hintMaxAngleName);
            job.hintMinAngle = FloatProperty.Bind(animator, component, data.hintMinAngleName);
            job.tolerance = FloatProperty.Bind(animator, component, data.toleranceName);
#else
            job.rotationRanges = new NativeArray<Vector2>(new[]
            {
                data.rotationRange1,
                data.rotationRange2,
                data.rotationRange3
            }, Allocator.Persistent);
            job.hintMaxAngle = data.hintMaxAngle;
            job.hintMinAngle = data.hintMinAngle;
            job.tolerance = data.tolerance;
#endif
            job.lengths = new NativeArray<float>(new[]
            {
                Vector3.Distance(data.root.position, data.node1.position),
                Vector3.Distance(data.node1.position, data.node2.position),
                Vector3.Distance(data.node2.position, data.tip.position),
            }, Allocator.Persistent);
            job.totalLength = job.lengths[0] + job.lengths[1] +  job.lengths[2];
            

            job.iterations = data.iterations;
            job.upAxis = GetAxis(data.upAxis);
            job.rightAxis = GetAxis(data.rightAxis);
            job.forwardAxis = GetAxis(data.forwardAxis);
            
            job.positionsCache = new NativeArray<Vector3>(4, Allocator.Persistent);
            job.positionsFinal = new NativeArray<Vector3>(4, Allocator.Persistent);
            job.anglesCache = new NativeArray<float>(3, Allocator.Persistent);
            job.anglesFinal = new NativeArray<float>(3, Allocator.Persistent);

            return job;
        }

        public override void Destroy(FingerFABRIKJob job)
        {
            job.bones.Dispose();
            job.rotationRanges.Dispose();
            job.lengths.Dispose();
            job.positionsCache.Dispose();
            job.positionsFinal.Dispose();
            job.anglesCache.Dispose();
            job.anglesFinal.Dispose();
        }
        
        Vector3 GetAxis(FingerFABRIKData.Axis axis)
        {
            switch (axis)
            {
                case FingerFABRIKData.Axis.X: return Vector3.right;
                case FingerFABRIKData.Axis.Y: return Vector3.up;
                case FingerFABRIKData.Axis.Z: return Vector3.forward;
                case FingerFABRIKData.Axis.NegX: return -Vector3.right;
                case FingerFABRIKData.Axis.NegY: return -Vector3.up;
                case FingerFABRIKData.Axis.NegZ: return -Vector3.forward;
            }

            return default;
        }
    }
}