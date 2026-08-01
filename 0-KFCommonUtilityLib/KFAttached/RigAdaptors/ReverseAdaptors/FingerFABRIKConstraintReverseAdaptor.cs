using UnityEngine;
using UnityEngine.Animations.Rigging;

public class FingerFABRIKConstraintReverseAdaptor : RigAdaptorAbs
{
    [Header("Finger nodes")]
    [SerializeField] private Transform _root;
    [SerializeField] private Transform _node1;
    [SerializeField] private Transform _node2;
    [SerializeField] private Transform _tip;
    [Header("Target")]
    [SerializeField] private string _target;
    [SerializeField] private string _hint;
    [Header("FABRIK")]
    [SerializeField] private Vector2 _rotationRange1;
    [SerializeField] private Vector2 _rotationRange2;
    [SerializeField] private Vector2 _rotationRange3;
    [SerializeField] private FingerFABRIKData.Axis _forwardAxis;
    [SerializeField] private FingerFABRIKData.Axis _rightAxis;
    [SerializeField] private FingerFABRIKData.Axis _upAxis;
    [SerializeField] private float _hintMinAngle;
    [SerializeField] private float _hintMaxAngle;
    [SerializeField] private int _iterations;
    [SerializeField] private float _tolerance;
    
    public override void FindRigTargets()
    {
        var constrain = GetComponent<FingerFABRIKConstraint>();
        constrain.Reset();
        constrain.weight = weight;
        constrain.data.root = ReplaceWeaponTarget(ref _root);
        constrain.data.node1 = ReplaceWeaponTarget(ref _node1);
        constrain.data.node2 = ReplaceWeaponTarget(ref _node2);
        constrain.data.tip = ReplaceWeaponTarget(ref _tip);
        constrain.data.target = targetRoot.FindInAllChildren(_target);
        constrain.data.hint = string.IsNullOrEmpty(_hint) ? null : targetRoot.FindInAllChildren(_hint);
        constrain.data.rotationRange1 = _rotationRange1;
        constrain.data.rotationRange2 = _rotationRange2;
        constrain.data.rotationRange3 = _rotationRange3;
        constrain.data.forwardAxis = _forwardAxis;
        constrain.data.rightAxis = _rightAxis;
        constrain.data.upAxis = _upAxis;
        constrain.data.hintMinAngle = _hintMinAngle;
        constrain.data.hintMaxAngle = _hintMaxAngle;
        constrain.data.tolerance = _tolerance;
        constrain.data.iterations = _iterations;
    }

    public override void ReadRigData()
    {
        var constrain = GetComponent<FingerFABRIKConstraint>();
        weight = constrain.weight;
        _root = constrain.data.root;
        _node1 = constrain.data.node1;
        _node2 = constrain.data.node2;
        _tip = constrain.data.tip;
        _target = constrain.data.target.name;
        _hint = constrain.data.hint?.name;
        _rotationRange1 = constrain.data.rotationRange1;
        _rotationRange2 = constrain.data.rotationRange2;
        _rotationRange3 = constrain.data.rotationRange3;
        _forwardAxis = constrain.data.forwardAxis;
        _rightAxis = constrain.data.rightAxis;
        _upAxis = constrain.data.upAxis;
        _hintMinAngle = constrain.data.hintMinAngle;
        _hintMaxAngle = constrain.data.hintMaxAngle;
        _tolerance = constrain.data.tolerance;
        _iterations = constrain.data.iterations;
    }
}