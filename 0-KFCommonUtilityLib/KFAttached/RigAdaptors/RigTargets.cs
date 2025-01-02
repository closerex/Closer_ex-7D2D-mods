#if NotEditor
using KFCommonUtilityLib.Scripts.StaticManagers;
#endif
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("KFAttachments/RigAdaptors/Rig Targets")]
public class RigTargets : MonoBehaviour
{
    [SerializeField]
    public Transform itemFpv;
    [SerializeField]
    public Rig rig;
    [SerializeField]
    public Transform attachmentReference;

    private Transform fpsArms;
    private RigLayer rigLayer;
    private RigBuilder weaponRB;

    private Animator itemAnimator;
    private RuntimeAnimatorController itemAnimatorController;

    //private float weight;

#if NotEditor
    private static int UniqueRigID = 0;
    public bool Destroyed { get; private set; } = false;
#endif
    //private PlayableGraph m_ControllerGraph;
    //private AnimatorControllerPlayable m_ControllerPlayable;
#if !NotEditor
    [SerializeField]
    private bool manualUpdate;
#else
    private bool manualUpdate = true;
#endif
    private void Awake()
    {
        itemAnimator = itemFpv.GetComponentInChildren<Animator>(true);
#if NotEditor
        itemAnimator.writeDefaultValuesOnDisable = true;
#endif
        foreach (var bindings in GetComponentsInChildren<TransformActivationBinding>(true))
        {
            bindings.animator = itemAnimator;
        }
        itemAnimatorController = itemAnimator.runtimeAnimatorController;
        //if (manualUpdate)
        //{
        //    //itemAnimator.runtimeAnimatorController = null;
        //    RebuildPlayableGraph();
        //}
#if NotEditor
        rig.gameObject.name += $"_UID_{UniqueRigID++}";
        AnimationRiggingManager.AddRigExcludeName(rig.gameObject.name);

        itemFpv.gameObject.SetActive(false);
        rig.gameObject.SetActive(false);
        gameObject.GetOrAddComponent<AttachmentReference>().attachmentReference = attachmentReference;
#endif
    }

#if NotEditor
#endif
    private void RebuildPlayableGraph()
    {
        //if (m_ControllerGraph.IsValid())
        //{
        //    m_ControllerGraph.Destroy();
        //}
        //m_ControllerGraph = PlayableGraph.Create();
        //m_ControllerGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

        //m_ControllerPlayable = AnimatorControllerPlayable.Create(m_ControllerGraph, itemAnimatorController);
        //var output = AnimationPlayableOutput.Create(m_ControllerGraph, "output", itemAnimator);
        //output.SetSourcePlayable(m_ControllerPlayable);

        //if (itemAnimator.TryGetComponent<RigBuilder>(out weaponRB))
        //{
        //    weaponRB.enabled = false;
        //    weaponRB.Build(m_ControllerGraph);

        //}

        //itemAnimator.transform.AddMissingComponent<ItemAnimatorUpdate>().graph = m_ControllerGraph;
        //m_ControllerPlayable.Play();

        //if (itemAnimator.TryGetComponent<RigBuilder>(out weaponRB))
        //{
        //    weaponRB.enabled = false;
        //    weaponRB.Build();
        //}
        //itemAnimator.enabled = false;
        itemAnimator.speed = 0;
        itemAnimator.transform.AddMissingComponent<ItemAnimatorUpdate>();

    }

    public void Init(Transform fpsArms)
    {
        if (fpsArms == null)
        {
            Destroy();
            return;
        }

        if (itemAnimator.TryGetComponent<AnimationDelayRender>(out var delayRenderer))
        {
            Destroy(delayRenderer);
        }
        var animator = fpsArms.GetComponentInChildren<Animator>();
        fpsArms = animator.transform;
        //fpsArms.AddMissingComponent<PlayerRigLateUpdate>();
        this.fpsArms = fpsArms;
        itemFpv.SetParent(fpsArms.parent, false);
        itemFpv.SetAsFirstSibling();
        itemFpv.position = Vector3.zero;
        itemFpv.localPosition = Vector3.zero;
        itemFpv.localRotation = Quaternion.identity;
#if NotEditor
        Utils.SetLayerRecursively(itemFpv.gameObject, 10, Utils.ExcludeLayerZoom);
        Utils.SetLayerRecursively(gameObject, 24, Utils.ExcludeLayerZoom);
#endif
        //LogInfo(itemFpv.localPosition.ToString() + " / " + itemFpv.localEulerAngles.ToString());
        var rc = rig.GetComponent<RigConverter>();
        rc.targetRoot = fpsArms;
        rc.Rebind();

        //((AnimationPlayableOutput)animator.playableGraph.GetOutputByType<AnimationPlayableOutput>(0)).SetSortingOrder(0);

        //animator.Update(0);
        //itemAnimator.Update(0);

        SetEnabled(false, true);
    }

    public void BuildRig()
    {
        if (!fpsArms)
        {
            Destroy();
            return;
        }
        if (rigLayer != null)
        {
            return;
        }
        Stopwatch sw = new Stopwatch();
        sw.Start();
        rig.transform.SetParent(fpsArms, false);
        rig.transform.SetAsFirstSibling();
        rig.transform.position = Vector3.zero;
        rig.transform.localPosition = Vector3.zero;
        rig.transform.localRotation = Quaternion.identity;
        var animator = fpsArms.GetComponent<Animator>();
        animator.UnbindAllStreamHandles();
        animator.UnbindAllSceneHandles();

        var rigBuilder = fpsArms.AddMissingComponent<RigBuilder>();
        rigBuilder.layers.RemoveAll(r => r.rig == rig);
        rigLayer = new RigLayer(rig, true);
        rigBuilder.layers.Add(rigLayer);
        animator.Rebind();
        rigBuilder.Build();
        sw.Stop();
        string info = $"setup animation rig took {sw.ElapsedMilliseconds} ms";
        Log.Out(info);
    }

    public void DestroyRig()
    {
        if (!fpsArms)
        {
            Destroy();
            return;
        }
        if (rigLayer == null)
        {
            return;
        }
        Stopwatch sw = new Stopwatch();
        sw.Start();
        var animator = fpsArms.GetComponent<Animator>();

        var rigBuilder = fpsArms.AddMissingComponent<RigBuilder>();
        rigBuilder.layers.Remove(rigLayer);
        animator.UnbindAllStreamHandles();
        animator.UnbindAllSceneHandles();

        rig.transform.SetParent(transform, false);
        animator.Rebind();
        rigBuilder.Build();
        rig.gameObject.SetActive(false);
        rigLayer = null;
        sw.Stop();
        string info = $"destroy animation rig took {sw.ElapsedMilliseconds} ms";
        Log.Out(info);
    }

    public void Destroy()
    {
#if NotEditor
        Destroyed = true;
        AnimationRiggingManager.RemoveRigExcludeName(rig.gameObject.name);
#endif
        //if (m_ControllerGraph.IsValid())
        //{
        //    m_ControllerGraph.Destroy();
        //}
        if (fpsArms)
        {
            DestroyRig();
        }
        fpsArms = null;

        attachmentReference?.SetParent(transform);
        //detach item fpv and rig so that transform.find does not search for them?
        itemFpv.parent = null;
        GameObject.Destroy(itemFpv.gameObject);
        rig.transform.parent = null;
        GameObject.Destroy(rig.gameObject);
        Component.Destroy(this);
        Log.Out("destroying rig no fpsarm"); ;
        return;
    }

    public void SetEnabled(bool enabled, bool forceDisableRoot = false)
    {
        //if (rigLayer == null)
        //    return;
        //var t = new StackTrace();

        //LogInfo($"set enabled {isFPV} stack trace:\n{t.ToString()}");

        attachmentReference?.SetParent(enabled ? itemFpv : transform, false);
        var rigBuilder = fpsArms.AddMissingComponent<RigBuilder>();

        itemFpv.gameObject.SetActive(enabled);
        itemFpv.localPosition = enabled ? Vector3.zero : new Vector3(0, -100, 0);
        //rigLayer.active = enabled;

        if (enabled)
        {
            BuildRig();
#if NotEditor
            if (!itemAnimator.TryGetComponent<AnimationDelayRender>(out var delayRenderer))
            {
                delayRenderer = itemAnimator.gameObject.AddComponent<AnimationDelayRender>();
                //delayRenderer.InitializeTarget(itemAnimator.transform);
            }
#endif
        }
        else
        {
            DestroyRig();
        }
            //if (enabled && manualUpdate)
            //{
            //    //so it seems there's no direct way to reset this animator playable controller
            //    //I have no choice but rebuild the whole graph again and pass the animator param bindings to the animator again
            //    //luckily this does not introduce much overhead
            //    //RebuildPlayableGraph();
            //    //foreach (var binding in attachmentReference.GetComponentsInChildren<TransformActivationBinding>(true))
            //    //{
            //    //    binding.UpdateBool(binding.gameObject.activeSelf);
            //    //}
            //    itemAnimator.Update(Time.deltaTime);
            //    //m_ControllerGraph.Evaluate(Time.deltaTime);
            //    //weaponRB?.Evaluate(Time.deltaTime);
            //}
            //else
            //{

            //}
            gameObject.SetActive(forceDisableRoot ? false : !enabled);
    }

#if NotEditor
    //VRoid switch view workaround
    public void OnEnable()
    {
        var player = GetComponentInParent<EntityPlayerLocal>();
        if (player != null && player.bFirstPersonView)
        {
            gameObject.SetActive(false);
        }
    }
#endif
}
