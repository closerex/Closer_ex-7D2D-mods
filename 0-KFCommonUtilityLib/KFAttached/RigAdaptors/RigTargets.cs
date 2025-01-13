#if NotEditor
using KFCommonUtilityLib.Scripts.StaticManagers;
#endif
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("KFAttachments/RigAdaptors/Rig Targets")]
public class RigTargets : AnimationTargetsAbs
{
    [SerializeField]
    public Transform itemFpv;
    [SerializeField]
    public Rig rig;
    [SerializeField]
    public Transform attachmentReference;

    private RigLayer rigLayer;
    private RigBuilder weaponRB;

    private Animator itemAnimator;

    public override Transform ItemFpv { get => itemFpv; protected set => itemFpv = value; }
    public override Transform AttachmentRef { get => attachmentReference; protected set => attachmentReference = value; }
    protected override Animator ItemAnimatorFpv => itemAnimator;
    protected override void Awake()
    {
        base.Awake();
        if (!itemFpv)
            return;
        itemAnimator = itemFpv.GetComponentInChildren<Animator>(true);
#if NotEditor
        itemAnimator.writeDefaultValuesOnDisable = true;
#endif
#if NotEditor
        rig.gameObject.name += $"_UID_{TypeBasedUID<AnimationTargetsAbs>.UID}";
        AnimationRiggingManager.AddRigExcludeName(rig.gameObject.name);

        itemFpv.gameObject.SetActive(false);
        rig.gameObject.SetActive(false);
#endif
    }

    protected override void Init()
    {
        if (Destroyed || !itemFpv)
        {
            return;
        }

        if (ItemAnimator.TryGetComponent<AnimationDelayRender>(out var delayRenderer))
        {
            Destroy(delayRenderer);
        }
        if (IsFpv)
        {
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

    protected override void SetupFpv()
    {
        if (!PlayerAnimatorTrans || !itemFpv)
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
        rig.transform.SetParent(PlayerAnimatorTrans, false);
        rig.transform.SetAsFirstSibling();
        rig.transform.position = Vector3.zero;
        rig.transform.localPosition = Vector3.zero;
        rig.transform.localRotation = Quaternion.identity;
        var animator = PlayerAnimatorTrans.GetComponent<Animator>();
        animator.UnbindAllStreamHandles();
        animator.UnbindAllSceneHandles();

        var rigBuilder = PlayerAnimatorTrans.AddMissingComponent<RigBuilder>();
        rigBuilder.layers.RemoveAll(r => r.rig == rig);
        rigLayer = new RigLayer(rig, true);
        rigBuilder.layers.Add(rigLayer);
        animator.Rebind();
        rigBuilder.Build();
        sw.Stop();
        string info = $"setup animation rig took {sw.ElapsedMilliseconds} ms";
        Log.Out(info);
    }

    protected override void RemoveFpv()
    {
        if (!PlayerAnimatorTrans || !itemFpv)
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
        var animator = PlayerAnimatorTrans.GetComponent<Animator>();

        var rigBuilder = PlayerAnimatorTrans.AddMissingComponent<RigBuilder>();
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

    public override void DestroyFpv()
    {
        base.DestroyFpv();
        if (rig)
        {
#if NotEditor
            AnimationRiggingManager.RemoveRigExcludeName(rig.gameObject.name);
#endif
            rig.transform.parent = null;
            GameObject.Destroy(rig.gameObject);
        }
        rig = null;
    }

    public override void SetEnabled(bool enabled)
    {
        //var t = new StackTrace();

        //LogInfo($"set enabled {isFPV} stack trace:\n{t.ToString()}");
        if (IsFpv)
        {
            var rigBuilder = PlayerAnimatorTrans.AddMissingComponent<RigBuilder>();
        }
        if (itemFpv)
        {
            itemFpv.localPosition = enabled ? Vector3.zero : new Vector3(0, -100, 0);
        }
        base.SetEnabled(enabled);
#if NotEditor
        if (enabled && ItemAnimator && !ItemAnimator.TryGetComponent<AnimationDelayRender>(out var delayRenderer))
        {
            delayRenderer = ItemAnimator.gameObject.AddComponent<AnimationDelayRender>();
        }
#endif
    }

#if NotEditor
    private readonly static int[] resetHashes = new int[]
    {
            Animator.StringToHash("Reload"),
            Animator.StringToHash("WeaponFire")
    };

    public override void UpdatePlayerAvatar(AvatarController avatarController, bool rigWeaponChanged)
    {
        base.UpdatePlayerAvatar(avatarController, rigWeaponChanged);
        if (avatarController is AvatarLocalPlayerController localPlayerController && localPlayerController.isFPV && localPlayerController.FPSArms != null)
        {
            localPlayerController.FPSArms.Animator.Play("idle", 0, 0f);
            foreach (var hash in resetHashes)
            {
                AnimationRiggingPatches.VanillaResetTrigger(localPlayerController, hash, false);
            }
        }
    }
#endif
}
