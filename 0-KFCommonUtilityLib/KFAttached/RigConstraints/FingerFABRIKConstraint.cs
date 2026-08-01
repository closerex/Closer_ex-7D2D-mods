using System;

namespace UnityEngine.Animations.Rigging
{
    [Serializable]
    public struct FingerFABRIKData : IAnimationJobData, IFingerFABRIKData
    {
        public enum Axis
        {
            X, Y, Z,
            NegX, NegY, NegZ
        }
        
        [Header("Finger nodes")]
        [SyncSceneToStream, SerializeField] private Transform _root;
        [SyncSceneToStream, SerializeField] private Transform _node1;
        [SyncSceneToStream, SerializeField] private Transform _node2;
        [SyncSceneToStream, SerializeField] private Transform _tip;
        [Header("Target")]
        [SyncSceneToStream, SerializeField] private Transform _target;
        [SyncSceneToStream, SerializeField] private Transform _hint;
        [Header("FABRIK")]
        [SyncSceneToStream, SerializeField] private Vector2 _rotationRange1;
        [SyncSceneToStream, SerializeField] private Vector2 _rotationRange2;
        [SyncSceneToStream, SerializeField] private Vector2 _rotationRange3;
        [NotKeyable, SerializeField] private Axis _forwardAxis;
        [NotKeyable, SerializeField] private Axis _rightAxis;
        [NotKeyable, SerializeField] private Axis _upAxis;
        [SyncSceneToStream, SerializeField] private float _hintMinAngle;
        [SyncSceneToStream, SerializeField] private float _hintMaxAngle;
        [NotKeyable, SerializeField] private int _iterations;
        [SyncSceneToStream, SerializeField] private float _tolerance;

        public bool IsValid()
        {
            if (_root == null || _node1 == null || _node2 == null || _tip == null || _target == null)
            {
                return false;
            }
            return true;
        }

        public void SetDefaultValues()
        {
            _root = null;
            _node1 = null;
            _node2 = null;
            _tip = null;
            _target = null;
            _hint = null;
            
            _rotationRange1 = new Vector2(-60, 90);
            _rotationRange2 = new Vector2(-5, 90);
            _rotationRange3 = new Vector2(-15, 90);

            _forwardAxis = Axis.NegX;
            _rightAxis = Axis.Z;
            _upAxis = Axis.Y;
            
            _hintMinAngle = -5;
            _hintMaxAngle = 5;
            
            _iterations = 10;
            _tolerance = 1e-6f;
        }

        public Transform root
        {
            get => _root;
            set => _root = value;
        }

        public Transform node1
        {
            get => _node1;
            set => _node1 = value;
        }

        public Transform node2
        {
            get => _node2;
            set => _node2 = value;
        }

        public Transform tip
        {
            get => _tip;
            set => _tip = value;
        }

        public Axis forwardAxis
        {
            get => _forwardAxis;
            set => _forwardAxis = value;
        }

        public Axis rightAxis
        {
            get => _rightAxis;
            set => _rightAxis = value;
        }

        public Axis upAxis
        {
            get => _upAxis;
            set => _upAxis = value;
        }

        public Vector2 rotationRange1
        {
            get => _rotationRange1;
            set => _rotationRange1 = value;
        }

        public Vector2 rotationRange2
        {
            get => _rotationRange2;
            set => _rotationRange2 = value;
        }

        public Vector2 rotationRange3
        {
            get => _rotationRange3;
            set => _rotationRange3 = value;
        }

        public Transform target
        {
            get => _target;
            set => _target = value;
        }

        public Transform hint
        {
            get => _hint;
            set => _hint = value;
        }

        public float hintMinAngle
        {
            get => _hintMinAngle;
            set => _hintMinAngle = value;
        }

        public float hintMaxAngle
        {
            get => _hintMaxAngle;
            set => _hintMaxAngle = value;
        }

        public int iterations
        {
            get => _iterations;
            set => _iterations = value;
        }

        public float tolerance
        {
            get => _tolerance;
            set => _tolerance = value;
        }

        public string rotationRange1Name => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(_rotationRange1));

        public string rotationRange2Name => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(_rotationRange2));

        public string rotationRange3Name => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(_rotationRange3));

        public string hintMinAngleName => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(_hintMinAngle));

        public string hintMaxAngleName => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(_hintMaxAngle));

        public string toleranceName => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(_tolerance));
    }
    
    public class FingerFABRIKConstraint : RigConstraint<FingerFABRIKJob, FingerFABRIKData, FingerFABRIKBinder<FingerFABRIKData>>
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            Vector2 range = m_Data.rotationRange1;
            range.x = Mathf.Clamp(range.x, -90, 90);
            range.y = Mathf.Clamp(range.y, -90, 90);
            m_Data.rotationRange1 = range;
            range = m_Data.rotationRange2;
            range.x = Mathf.Clamp(range.x, -90, 90);
            range.y = Mathf.Clamp(range.y, -90, 90);
            m_Data.rotationRange2 = range;
            range = m_Data.rotationRange3;
            range.x = Mathf.Clamp(range.x, -90, 90);
            range.y = Mathf.Clamp(range.y, -90, 90);
            m_Data.rotationRange3 = range;
            
            m_Data.hintMaxAngle = Mathf.Clamp(m_Data.hintMaxAngle, 0, 180);
            m_Data.hintMinAngle = Mathf.Clamp(m_Data.hintMinAngle, -180, 0);
            
            m_Data.iterations = Mathf.Clamp(m_Data.iterations, 2, 32);
            m_Data.tolerance = Mathf.Clamp(m_Data.tolerance, 0f, 0.01f);
        }
    }
}