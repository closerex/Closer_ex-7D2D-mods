using Unity.Mathematics;

namespace UnityEngine.Animations.Rigging
{
    public struct SyncSimLocalRotationJob : IWeightedAnimationJob
    {
        public ReadOnlyTransformHandle sourceParent;
        public ReadOnlyTransformHandle sourceTransform;
        public ReadWriteTransformHandle targetParent;
        public ReadOnlyTransformHandle targetTransform;
        public Vector3 sourceUpAxis;
        public Vector3 sourceForwardAxis;
        public Vector3 targetUpAxis;
        public Vector3 targetForwardAxis;
        
        public void ProcessAnimation(AnimationStream stream)
        {
            var w = jobWeight.Get(stream);
            if (w > 0f)
            {
                sourceParent.GetGlobalTR(stream, out Vector3 sourceParentPos, out var sourceParentRot);
                var sourceParentLocalRot = sourceParent.GetLocalRotation(stream);
                sourceParentRot = sourceParentRot * Quaternion.Inverse(sourceParentLocalRot);
                var sourcePos = sourceTransform.GetPosition(stream);
                var sourceUp = sourceParentRot * sourceUpAxis;
                var sourceForward = sourceParentRot * sourceForwardAxis;
                var sourceRight = Vector3.Cross(sourceUp,  sourceForward);
                var sourceAimWorld = (sourcePos - sourceParentPos).normalized;
                var sourceAimLocal = new Vector3(Vector3.Dot(sourceAimWorld, sourceRight), Vector3.Dot(sourceAimWorld, sourceUp), Vector3.Dot(sourceAimWorld, sourceForward));
                var sourceLocalRot = Quaternion.LookRotation(sourceAimLocal);
                
                targetParent.GetGlobalTR(stream, out Vector3 targetParentPos, out var targetParentRot);
                var targetPos = targetTransform.GetPosition(stream);
                var targetUp = targetParentRot * targetUpAxis;
                var targetForward = targetParentRot * targetForwardAxis;
                var targetParentWorldRot = Quaternion.LookRotation(targetForward, targetUp);
                var sourceAimWorldSim = targetParentWorldRot * sourceLocalRot * Vector3.forward;
                var targetAimWorld = (targetPos - targetParentPos).normalized;
                var diff = Quaternion.FromToRotation(targetAimWorld, sourceAimWorldSim);
                targetParent.SetRotation(stream, Quaternion.Lerp(targetParentRot, diff * targetParentRot, w));
            }
            else
            {
                AnimationRuntimeUtils.PassThrough(stream, targetParent);
            }
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
            
        }

        public FloatProperty jobWeight
        {
            get;
            set;
        }
    }

    public interface ISyncSimLocalRotationData
    {
        Transform sourceParent { get; set; }
        Transform sourceTransform { get; set; }
        Transform targetParent { get; set; }
        Transform targetTransform { get; set; }
        FingerFABRIKData.Axis sourceForwardAxis { get; set; }
        FingerFABRIKData.Axis sourceUpAxis { get; set; }
        FingerFABRIKData.Axis targetForwardAxis { get; set; }
        FingerFABRIKData.Axis targetUpAxis { get; set; }
    }

    public class SyncSimLocalRotationBinder<T> : AnimationJobBinder<SyncSimLocalRotationJob, T>
        where T : struct, IAnimationJobData, ISyncSimLocalRotationData
    {
        public override SyncSimLocalRotationJob Create(Animator animator, ref T data, Component component)
        {
            var job = new SyncSimLocalRotationJob();
            job.sourceParent = ReadOnlyTransformHandle.Bind(animator, data.sourceParent);
            job.sourceTransform = ReadOnlyTransformHandle.Bind(animator, data.sourceTransform);
            job.targetParent = ReadWriteTransformHandle.Bind(animator, data.targetParent);
            job.targetTransform = ReadOnlyTransformHandle.Bind(animator, data.targetTransform);
            job.sourceForwardAxis = GetAxis(data.sourceForwardAxis);
            job.sourceUpAxis = GetAxis(data.sourceUpAxis);
            job.targetForwardAxis = GetAxis(data.targetForwardAxis);
            job.targetUpAxis = GetAxis(data.targetUpAxis);
            return job;
        }

        public override void Destroy(SyncSimLocalRotationJob job)
        {
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