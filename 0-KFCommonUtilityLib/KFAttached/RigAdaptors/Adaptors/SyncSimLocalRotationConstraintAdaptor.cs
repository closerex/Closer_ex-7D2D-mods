using UnityEngine;
using UnityEngine.Animations.Rigging;

public class SyncSimLocalRotationConstraintAdaptor : RigAdaptorAbs
{
    private Transform _sourceParent;
    private Transform _sourceTransform;
    private string _targetParent;
    private string _targetTransform;
    private FingerFABRIKData.Axis _sourceForwardAxis;
    private FingerFABRIKData.Axis _sourceUpAxis;
    private FingerFABRIKData.Axis _targetForwardAxis;
    private FingerFABRIKData.Axis _targetUpAxis;
    
    public override void ReadRigData()
    {
        var constraint = GetComponent<SyncSimLocalRotationConstraint>();
        weight = constraint.weight;
        _sourceParent = constraint.data.sourceParent;
        _sourceTransform = constraint.data.sourceTransform;
        _sourceForwardAxis = constraint.data.sourceForwardAxis;
        _sourceUpAxis = constraint.data.sourceUpAxis;
        _targetParent = constraint.data.targetParent.name;
        _targetTransform = constraint.data.targetParent.name;
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
