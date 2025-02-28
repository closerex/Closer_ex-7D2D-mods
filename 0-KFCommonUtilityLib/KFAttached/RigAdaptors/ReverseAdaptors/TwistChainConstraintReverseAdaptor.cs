using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class TwistChainConstraintReverseAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private Transform m_Root;
    [SerializeField]
    private Transform m_Tip;
    [SerializeField]
    private string m_RootTarget;
    [SerializeField]
    private string m_TipTarget;
    [SerializeField]
    private AnimationCurve m_Curve;

    public override void FindRigTargets()
    {
        var constraint = GetComponent<TwistChainConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.root = m_Root;
        constraint.data.tip = m_Tip;
        constraint.data.rootTarget = targetRoot.FindInAllChildren(m_RootTarget);
        constraint.data.tipTarget = targetRoot.FindInAllChildren(m_TipTarget);
        constraint.data.curve = m_Curve;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<TwistChainConstraint>();
        weight = constraint.weight;
        m_Root = constraint.data.root;
        m_Tip = constraint.data.tip;
        m_RootTarget = constraint.data.rootTarget?.name;
        m_TipTarget = constraint.data.tipTarget?.name;
        m_Curve = constraint.data.curve;
    }
}
