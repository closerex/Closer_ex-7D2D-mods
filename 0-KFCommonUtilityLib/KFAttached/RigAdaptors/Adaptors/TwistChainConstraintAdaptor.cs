using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class TwistChainConstraintAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private string m_Root;
    [SerializeField]
    private string m_Tip;
    [SerializeField]
    private Transform m_RootTarget;
    [SerializeField]
    private Transform m_TipTarget;
    [SerializeField]
    private AnimationCurve m_Curve;

    public override void FindRigTargets()
    {
        var constraint = GetComponent<TwistChainConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.root = targetRoot.FindInAllChildren(m_Root);
        constraint.data.tip = targetRoot.FindInAllChildren(m_Tip);
        constraint.data.rootTarget = m_RootTarget;
        constraint.data.tipTarget = m_TipTarget;
        constraint.data.curve = m_Curve;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<TwistChainConstraint>();
        weight = constraint.weight;
        m_Root = constraint.data.root?.name;
        m_Tip = constraint.data.tip?.name;
        m_RootTarget = constraint.data.rootTarget;
        m_TipTarget = constraint.data.tipTarget;
        m_Curve = constraint.data.curve;
    }
}
