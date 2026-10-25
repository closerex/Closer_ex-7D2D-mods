using UnityEngine;
using UnityEngine.Animations.Rigging;

public class SyncSimLocalRotationConstraintAdaptor : RigAdaptorAbs
{
    [SerializeField] private Transform _sourceParent;
    [SerializeField] private Transform _sourceTransform;
    [SerializeField] private string _targetParent;
    [SerializeField] private string _targetTransform;
    [SerializeField] private FingerFABRIKData.Axis _sourceForwardAxis;
    [SerializeField] private FingerFABRIKData.Axis _sourceUpAxis;
    [SerializeField] private FingerFABRIKData.Axis _targetForwardAxis;
    [SerializeField] private FingerFABRIKData.Axis _targetUpAxis;
    
    public override void ReadRigData()
    {
        var constraint = GetComponent<SyncSimLocalRotationConstraint>();
        weight = constraint.weight;
        _sourceParent = constraint.data.sourceParent;
        _sourceTransform = constraint.data.sourceTransform;
        _sourceForwardAxis = constraint.data.sourceForwardAxis;
        _sourceUpAxis = constraint.data.sourceUpAxis;
        _targetParent = constraint.data.targetParent?.name;
        _targetTransform = constraint.data.targetTransform?.name;
        _targetForwardAxis = constraint.data.targetForwardAxis;
        _targetUpAxis = constraint.data.targetUpAxis;
    }

    public override void FindRigTargets()
    {
        var constraint = GetComponent<SyncSimLocalRotationConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.sourceParent = ReplaceWeaponTarget(ref _sourceParent);
        constraint.data.sourceTransform = ReplaceWeaponTarget(ref _sourceTransform);
        constraint.data.sourceForwardAxis = _sourceForwardAxis;
        constraint.data.sourceUpAxis = _sourceUpAxis;
        constraint.data.targetParent = targetRoot.FindInAllChildren(_targetParent);
        constraint.data.targetTransform = targetRoot.FindInAllChildren(_targetTransform);
        constraint.data.targetForwardAxis = _targetForwardAxis;
        constraint.data.targetUpAxis = _targetUpAxis;
    }
}
