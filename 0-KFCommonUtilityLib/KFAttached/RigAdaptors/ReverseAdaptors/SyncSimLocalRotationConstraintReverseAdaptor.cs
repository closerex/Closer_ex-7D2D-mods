using UnityEngine;
using UnityEngine.Animations.Rigging;

public class SyncSimLocalRotationConstraintReverseAdaptor : RigAdaptorAbs
{
    private string _sourceParent;
    private string _sourceTransform;
    private Transform _targetParent;
    private Transform _targetTransform;
    private FingerFABRIKData.Axis _sourceForwardAxis;
    private FingerFABRIKData.Axis _sourceUpAxis;
    private FingerFABRIKData.Axis _targetForwardAxis;
    private FingerFABRIKData.Axis _targetUpAxis;
    
    public override void ReadRigData()
    {
        var constraint = GetComponent<SyncSimLocalRotationConstraint>();
        weight = constraint.weight;
        _sourceParent = constraint.data.sourceParent.name;
        _sourceTransform = constraint.data.sourceTransform.name;
        _sourceForwardAxis = constraint.data.sourceForwardAxis;
        _sourceUpAxis = constraint.data.sourceUpAxis;
        _targetParent = constraint.data.targetParent;
        _targetTransform = constraint.data.targetParent;
        _targetForwardAxis = constraint.data.targetForwardAxis;
        _targetUpAxis = constraint.data.targetUpAxis;
    }

    public override void FindRigTargets()
    {
        var constraint = GetComponent<SyncSimLocalRotationConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.sourceParent = targetRoot.FindInAllChildren(_sourceParent);
        constraint.data.sourceTransform = targetRoot.FindInAllChildren(_sourceTransform);
        constraint.data.sourceForwardAxis = _sourceForwardAxis;
        constraint.data.sourceUpAxis = _sourceUpAxis;
        constraint.data.targetParent = ReplaceWeaponTarget(ref _targetParent);
        constraint.data.targetTransform = ReplaceWeaponTarget(ref _targetTransform);
        constraint.data.targetForwardAxis = _targetForwardAxis;
        constraint.data.targetUpAxis = _targetUpAxis;
    }
}
