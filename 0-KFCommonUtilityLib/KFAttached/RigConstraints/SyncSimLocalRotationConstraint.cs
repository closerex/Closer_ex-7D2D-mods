using System;

namespace UnityEngine.Animations.Rigging
{
    [Serializable]
    public struct SyncSimLocalRotationData : IAnimationJobData, ISyncSimLocalRotationData
    {
        [SyncSceneToStream, SerializeField] private Transform _sourceParent;
        [SyncSceneToStream, SerializeField] private Transform _sourceTransform;
        [SyncSceneToStream, SerializeField] private Transform _targetParent;
        [SyncSceneToStream, SerializeField] private Transform _targetTransform;
        [NotKeyable, SerializeField] private FingerFABRIKData.Axis _sourceForwardAxis;
        [NotKeyable, SerializeField] private FingerFABRIKData.Axis _sourceUpAxis;
        [NotKeyable, SerializeField] private FingerFABRIKData.Axis _targetForwardAxis;
        [NotKeyable, SerializeField] private FingerFABRIKData.Axis _targetUpAxis;

        public Transform sourceParent
        {
            get => _sourceParent;
            set => _sourceParent = value;
        }

        public Transform sourceTransform
        {
            get => _sourceTransform;
            set => _sourceTransform = value;
        }

        public Transform targetParent
        {
            get => _targetParent;
            set => _targetParent = value;
        }

        public Transform targetTransform
        {
            get => _targetTransform;
            set => _targetTransform = value;
        }

        public FingerFABRIKData.Axis sourceForwardAxis
        {
            get => _sourceForwardAxis;
            set => _sourceForwardAxis = value;
        }

        public FingerFABRIKData.Axis sourceUpAxis
        {
            get => _sourceUpAxis;
            set => _sourceUpAxis = value;
        }

        public FingerFABRIKData.Axis targetForwardAxis
        {
            get => _targetForwardAxis;
            set => _targetForwardAxis = value;
        }

        public FingerFABRIKData.Axis targetUpAxis
        {
            get => _targetUpAxis;
            set => _targetUpAxis = value;
        }

        public bool IsValid()
        {
            return sourceParent && sourceTransform && targetParent && targetTransform;
        }

        public void SetDefaultValues()
        {
            sourceParent = null;
            sourceTransform = null;
            targetParent = null;
            targetTransform = null;

            _sourceForwardAxis = FingerFABRIKData.Axis.Z;
            _sourceUpAxis = FingerFABRIKData.Axis.Y;
            _targetForwardAxis = FingerFABRIKData.Axis.NegX;
            _targetUpAxis = FingerFABRIKData.Axis.Y;
        }
    }
    
    public class SyncSimLocalRotationConstraint : 
        RigConstraint<SyncSimLocalRotationJob, SyncSimLocalRotationData, SyncSimLocalRotationBinder<SyncSimLocalRotationData>>
    {
    }
}