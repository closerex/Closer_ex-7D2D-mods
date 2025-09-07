#if NotEditor
using UniLinq;
#else
using System.Linq;
#endif
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using KFCommonUtilityLib;

[AddComponentMenu("KFAttachments/RigAdaptors/Rig Targets")]
public class RigTargets : AnimationTargetsAbs
{
    [Header("Fpv Fields")]
    [SerializeField]
    public Transform itemFpv;
    [SerializeField]
    public Rig rig;
    [SerializeField]
    public Transform attachmentReference;

    private RigLayer rigLayerFpv;

    private Animator itemAnimator;

    public override Transform ItemFpv { get => itemFpv; protected set => itemFpv = value; }
    public override Transform AttachmentRef { get => attachmentReference; protected set => attachmentReference = value; }
    protected override Animator ItemAnimatorFpv => itemAnimator;

    public override bool UseGraph => IsFpv ? false : ItemTpv;

    public override Transform PlayerOriginTransform { get; protected set; }

    public override bool IsRiggedWeapon => true;

    protected override void Awake()
    {
        base.Awake();
        if (!itemFpv)
            return;
        itemAnimator = itemFpv.GetComponentInChildren<Animator>(true);
        PlayerOriginTransform = itemAnimator.transform;
#if NotEditor
        itemAnimator.writeDefaultValuesOnDisable = true;
#endif
#if NotEditor
        rig.gameObject.name += $"_UID_{TypeBasedUID<AnimationTargetsAbs>.UID}";
        AnimationRiggingManager.AddRigExcludeName(rig.gameObject.name);

        itemFpv.gameObject.SetActive(false);
#endif
    }

    protected override void Init()
    {
        base.Init();
        if (!itemFpv)
        {
            return;
        }

        if (IsFpv)
        {
            if (ItemAnimatorFpv.TryGetComponent<AnimationDelayRender>(out var delayRenderer))
            {
                Destroy(delayRenderer);
            }
            itemFpv.SetParent(PlayerAnimatorTrans.parent, false);
            itemFpv.SetAsFirstSibling();
            itemFpv.position = Vector3.zero;
            itemFpv.localPosition = Vector3.zero;
            itemFpv.localRotation = Quaternion.identity;
            var rc = rig.GetComponent<RigConverter>();
            rc.targetRoot = PlayerAnimatorTrans;
            rc.Rebind();
        }
        else
        {
            itemFpv.SetParent(PlayerAnimatorTrans.parent);
            itemFpv.position = Vector3.zero;
            itemFpv.localPosition = Vector3.zero;
            itemFpv.localRotation = Quaternion.identity;
        }
        //Log.Out($"set parent to {PlayerAnimatorTrans.parent.parent.name}/{PlayerAnimatorTrans.parent.name}\n{StackTraceUtility.ExtractStackTrace()}");

//#if NotEditor
//        Utils.SetLayerRecursively(itemFpv.gameObject, 10, Utils.ExcludeLayerZoom);
//        Utils.SetLayerRecursively(gameObject, 24, Utils.ExcludeLayerZoom);
//#endif
        //LogInfo(itemFpv.localPosition.ToString() + " / " + itemFpv.localEulerAngles.ToString());
    }

    protected override bool SetupFpv()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        rig.transform.SetParent(PlayerAnimatorTrans, false);
        rig.transform.position = Vector3.zero;
        rig.transform.localPosition = Vector3.zero;
        rig.transform.localRotation = Quaternion.identity;

        var rigBuilder = PlayerAnimatorTrans.AddMissingComponent<RigBuilder>();
        rigBuilder.layers.Insert(0, rigLayerFpv = new RigLayer(rig, true));
#if NotEditor
        foreach (var layer in rigBuilder.layers)
        {
            if (layer.name == SDCSUtils.IKRIG)
            {
                layer.active = false;
            }
        }
#endif
        BuildRig(rigBuilder.GetComponent<Animator>(), rigBuilder);
        sw.Stop();
        string info = $"setup animation rig took {sw.ElapsedMilliseconds} ms";
        //info += $"\n{StackTraceUtility.ExtractStackTrace()}";
        Log.Out(info);
        return true;
    }

    protected override void RemoveFpv()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        var rigBuilder = PlayerAnimatorTrans.AddMissingComponent<RigBuilder>();
        int removed = rigBuilder.layers.RemoveAll(r => r.name == rigLayerFpv.name);
//#if NotEditor
//        Log.Out($"Removed {removed} layers, remaining:\n{string.Join("\n", rigBuilder.layers.Select(layer => layer.name))}");
//#endif
        rig.transform.SetParent(transform, false);
        rig.gameObject.SetActive(false);
        rigLayerFpv = null;
#if NotEditor
        foreach (var layer in rigBuilder.layers)
        {
            if (layer.name == SDCSUtils.IKRIG)
            {
                layer.active = true;
            }
        }
#endif
        BuildRig(rigBuilder.GetComponent<Animator>(), rigBuilder);
        sw.Stop();
        string info = $"destroy animation rig took {sw.ElapsedMilliseconds} ms";
        //info += $"\n{StackTraceUtility.ExtractStackTrace()}";
        Log.Out(info);
    }

    public override void DestroyFpv()
    {
#if NotEditor
        if (rig)
        {
            AnimationRiggingManager.RemoveRigExcludeName(rig.gameObject.name);
        }
#endif
        base.DestroyFpv();
        if (rig)
        {
            rig.transform.parent = null;
            GameObject.Destroy(rig.gameObject);
        }
        rig = null;
    }

    public override void SetEnabled(bool enabled)
    {
        //var t = new StackTrace();

        //LogInfo($"set enabled {isFPV} stack trace:\n{t.ToString()}");
        if (itemFpv)
        {
            itemFpv.localPosition = enabled ? Vector3.zero : new Vector3(0, -100, 0);
        }
        base.SetEnabled(enabled);
#if NotEditor
        if (enabled && IsFpv && ItemAnimatorFpv && !ItemAnimatorFpv.TryGetComponent<AnimationDelayRender>(out var delayRenderer))
        {
            delayRenderer = ItemAnimatorFpv.gameObject.AddComponent<AnimationDelayRender>();
        }
#endif
    }

#if NotEditor
    public override void UpdatePlayerAvatar(AvatarController avatarController, bool rigWeaponChanged)
    {
        base.UpdatePlayerAvatar(avatarController, rigWeaponChanged);
    }
#endif
}
