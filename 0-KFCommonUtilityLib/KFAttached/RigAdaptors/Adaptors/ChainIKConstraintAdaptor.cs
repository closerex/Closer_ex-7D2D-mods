using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class ChainIKConstraintAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private string m_Root;
    [SerializeField]
    private string m_Tip;
    [SerializeField]
    private Transform m_Target;
    [SerializeField]
    private float m_ChainRotationWeight;
    [SerializeField]
    private float m_TipRotationWeight;
    [SerializeField]
    private int m_MaxIterations;
    [SerializeField]
    private float m_Tolerance;
    [SerializeField]
    private bool m_MaintainTargetPositionOffset;
    [SerializeField]
    private bool m_MaintainTargetRotationOffset;

    public override void ReadRigData()
    {
        var constraint = GetComponent<ChainIKConstraint>();
        weight = constraint.weight;
        m_Root = constraint.data.root?.name;
        m_Tip = constraint.data.tip?.name;
        m_Target = constraint.data.target;
        m_ChainRotationWeight = constraint.data.chainRotationWeight;
        m_TipRotationWeight = constraint.data.tipRotationWeight;
        m_MaxIterations = constraint.data.maxIterations;
        m_Tolerance = constraint.data.tolerance;
        m_MaintainTargetPositionOffset = constraint.data.maintainTargetPositionOffset;
        m_MaintainTargetRotationOffset = constraint.data.maintainTargetRotationOffset;
    }

    public override void FindRigTargets()
    {
        var constraint = GetComponent<ChainIKConstraint>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.root = targetRoot.FindInAllChildren(m_Root);
        constraint.data.tip = targetRoot.FindInAllChildren(m_Tip);
        constraint.data.target = m_Target;
        constraint.data.chainRotationWeight = m_ChainRotationWeight;
        constraint.data.tipRotationWeight = m_TipRotationWeight;
        constraint.data.maxIterations = m_MaxIterations;
        constraint.data.tolerance = m_Tolerance;
        constraint.data.maintainTargetPositionOffset = m_MaintainTargetPositionOffset;
        constraint.data.maintainTargetRotationOffset = m_MaintainTargetRotationOffset;
    }
}
