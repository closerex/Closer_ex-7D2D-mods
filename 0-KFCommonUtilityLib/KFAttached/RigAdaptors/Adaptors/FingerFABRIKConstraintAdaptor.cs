using UnityEngine;
using UnityEngine.Animations.Rigging;

public class FingerFABRIKConstraintAdaptor : RigAdaptorAbs
{
    [Header("Finger nodes")]
    [SerializeField] private string _root;
    [SerializeField] private string _node1;
    [SerializeField] private string _node2;
    [SerializeField] private string _tip;
    [Header("Target")]
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _hint;
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
        constrain.data.root = targetRoot.FindInAllChildren(_root);
        constrain.data.node1 = targetRoot.FindInAllChildren(_node1);
        constrain.data.node2 = targetRoot.FindInAllChildren(_node2);
        constrain.data.tip = targetRoot.FindInAllChildren(_tip);
        constrain.data.target = ReplaceWeaponTarget(ref _target);
        constrain.data.hint = ReplaceWeaponTarget(ref _hint);
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
        _root = constrain.data.root.name;
        _node1 = constrain.data.node1.name;
        _node2 = constrain.data.node2.name;
        _tip = constrain.data.tip.name;
        _target = constrain.data.target;
        _hint = constrain.data.hint;
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