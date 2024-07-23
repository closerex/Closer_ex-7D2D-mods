#if NotEditor
using KFCommonUtilityLib.Scripts.StaticManagers;
#endif
using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

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
#endif
    private PlayableGraph m_ControllerGraph;
    private AnimatorControllerPlayable m_ControllerPlayable;
    private void Awake()
    {
        itemAnimator = itemFpv.GetComponentInChildren<Animator>(true);
        itemAnimator.writeDefaultValuesOnDisable = true;
        foreach (var bindings in GetComponentsInChildren<TransformActivationBinding>(true))
        {
            bindings.animator = itemAnimator;
        }
        itemAnimatorController = itemAnimator.runtimeAnimatorController;
        itemAnimator.runtimeAnimatorController = null;
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
        if (m_ControllerGraph.IsValid())
        {
            m_ControllerGraph.Destroy();
        }
        m_ControllerGraph = PlayableGraph.Create();
        m_ControllerGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

        m_ControllerPlayable = AnimatorControllerPlayable.Create(m_ControllerGraph, itemAnimatorController);
        var output = AnimationPlayableOutput.Create(m_ControllerGraph, "output", itemAnimator);
        output.SetSourcePlayable(m_ControllerPlayable);

        //itemAnimator.UnbindAllStreamHandles();
        //itemAnimator.UnbindAllSceneHandles();
        if (itemAnimator.TryGetComponent<RigBuilder>(out weaponRB))
        {
            weaponRB.enabled = false;
            weaponRB.Build(m_ControllerGraph);
            //weaponRB.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            //((AnimationPlayableOutput)weaponRB.graph.GetOutputByType<AnimationPlayableOutput>(0)).SetSortingOrder(2);
            //for (int i = 1; i < weaponRB.graph.GetOutputCount(); i++)
            //{
            //    ((AnimationPlayableOutput)weaponRB.graph.GetOutputByType<AnimationPlayableOutput>(i)).SetSortingOrder(1);
            //}
        }
        //for (int i = 0; i < itemAnimator.playableGraph.GetOutputCount(); i++)
        //{
        //    ((AnimationPlayableOutput)itemAnimator.playableGraph.GetOutputByType<AnimationPlayableOutput>(i)).SetSortingOrder(3);
        //}
        //itemAnimator.Rebind();
        //itemAnimator.enabled = false;
        //itemAnimator.playableGraph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
        itemAnimator.transform.AddMissingComponent<ItemAnimatorUpdate>().graph = m_ControllerGraph;
        m_ControllerPlayable.Play();
    }

    public void Init(Transform fpsArms)
    {
        if (fpsArms == null)
        {
            Destroy();
            return;
        }
        Stopwatch sw = new Stopwatch();
        sw.Start();

        var delayRenderer = itemAnimator.GetComponent<AnimationDelayRender>();
        if (delayRenderer)
        {
            Destroy(delayRenderer);
        }

        var animator = fpsArms.GetComponentInChildren<Animator>();
        fpsArms = animator.transform;
        this.fpsArms = fpsArms;
        itemFpv.SetParent(fpsArms.parent, false);
        itemFpv.SetAsFirstSibling();
        itemFpv.position = Vector3.zero;
        itemFpv.localPosition = Vector3.zero;
        itemFpv.localRotation = Quaternion.identity;
        rig.transform.SetParent(fpsArms, false);
        rig.transform.SetAsFirstSibling();
        rig.transform.position = Vector3.zero;
        rig.transform.localPosition = Vector3.zero;
        rig.transform.localRotation = Quaternion.identity;
#if NotEditor
        Utils.SetLayerRecursively(itemFpv.gameObject, 10, Utils.ExcludeLayerZoom);
        Utils.SetLayerRecursively(gameObject, 24, Utils.ExcludeLayerZoom);
#endif
        //LogInfo(itemFpv.localPosition.ToString() + " / " + itemFpv.localEulerAngles.ToString());
        var rc = rig.GetComponent<RigConverter>();
        rc.targetRoot = fpsArms;
        rc.Rebind();
        animator.UnbindAllStreamHandles();
        animator.UnbindAllSceneHandles();

        var rigBuilder = fpsArms.AddMissingComponent<RigBuilder>();
        rigBuilder.layers.RemoveAll(r => r.rig == rig);
        rigLayer = new RigLayer(rig, false);
        rigBuilder.layers.Add(rigLayer);
        rigBuilder.Build();
        animator.Rebind();
        //((AnimationPlayableOutput)animator.playableGraph.GetOutputByType<AnimationPlayableOutput>(0)).SetSortingOrder(0);

        //animator.Update(0);
        //itemAnimator.Update(0);

        sw.Stop();
        string info = $"setup animation rig took {sw.ElapsedMilliseconds} ms";
        Log.Out(info);

        SetEnabled(false, true);
    }

    public void Destroy()
    {
#if NotEditor
        AnimationRiggingManager.RemoveRigExcludeName(rig.gameObject.name);
#endif
        if (m_ControllerGraph.IsValid())
        {
            m_ControllerGraph.Destroy();
        }
        if (fpsArms == null)
        {
            attachmentReference?.SetParent(transform);
            GameObject.Destroy(itemFpv.gameObject);
            GameObject.Destroy(rig.gameObject);
            Component.Destroy(this);
            Log.Out("destroying rig no fpsarm"); ;
            return;
        }
        Stopwatch sw = new Stopwatch();
        sw.Start();
        var animator = fpsArms.GetComponent<Animator>();
        animator.UnbindAllStreamHandles();
        animator.UnbindAllSceneHandles();

        var rigBuilder = fpsArms.AddMissingComponent<RigBuilder>();
        rigBuilder.layers.Remove(rigLayer);
        rigBuilder.Build();
        animator.Rebind();

        rig.transform.SetParent(transform, false);
        itemFpv.SetParent(transform, false);
        rig.gameObject.SetActive(false);
        itemFpv.gameObject.SetActive(false);
        rigLayer = null;
        fpsArms = null;
        sw.Stop();
        string info = $"destroy animation rig took {sw.ElapsedMilliseconds} ms";
        Log.Out(info);
    }

    public void SetEnabled(bool enabled, bool forceDisableRoot = false)
    {
        if (rigLayer == null)
            return;
        //var t = new StackTrace();

        //LogInfo($"set enabled {isFPV} stack trace:\n{t.ToString()}");

        attachmentReference?.SetParent(enabled ? itemFpv : transform, false);
        var rigBuilder = fpsArms.AddMissingComponent<RigBuilder>();

        itemFpv.gameObject.SetActive(enabled);
        rigLayer.active = enabled;

        itemFpv.localPosition = new Vector3(0, 0, enabled ? 0 : -100);
#if NotEditor
#endif
        if (enabled)
        {
            //so it seems there's no direct way to reset this animator playable controller
            //I have no choice but rebuild the whole graph again and pass the animator param bindings to the animator again
            //luckily this does not introduce much overhead
            RebuildPlayableGraph();
            foreach (var binding in attachmentReference.GetComponentsInChildren<TransformActivationBinding>(true))
            {
                binding.UpdateBool(binding.gameObject.activeSelf);
            }
            m_ControllerGraph.Evaluate(Time.deltaTime);
            weaponRB?.SyncLayers();
        }
        else
        {

        }
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
