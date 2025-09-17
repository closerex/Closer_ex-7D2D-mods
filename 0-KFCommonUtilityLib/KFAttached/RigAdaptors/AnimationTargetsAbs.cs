#if NotEditor
using UniLinq;
#else
using System.Linq;
#endif
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using System;
using System.Collections.Generic;
using KFCommonUtilityLib;

[AddComponentMenu("")]
public abstract class AnimationTargetsAbs : MonoBehaviour
{
    protected enum ParentName
    {
        Spine3,
        LeftHand,
        RightHand,
    }
    protected static readonly string[] ParentNames = { "Spine3", "LeftHand", "RightHand" };
    protected static string GetParentName(ParentName name) => ParentNames[(int)name];
    [Header("TPV Fields")]
    [SerializeField]
    protected Transform itemTpv;
    [SerializeField]
    protected RuntimeAnimatorController weaponRuntimeControllerTpv;
    [SerializeField]
    protected AvatarMask weaponRigMaskTpv;
    [SerializeField]
    protected ParentName parentNameTpv;

    private Rig[] rigTpv;
    private RigLayer[] rigLayerTpv;
    protected Animator itemAnimatorTpv;
    protected bool fpvSet = false;
    protected bool tpvSet = false;

    private Dictionary<string, GameObject> dict_attachments = new Dictionary<string, GameObject>();
    private List<GameObject> list_attached_attachments = new List<GameObject>();
    private List<GameObject> list_activate_attachments = new List<GameObject>();

    public abstract Transform ItemFpv { get; protected set; }
    public abstract Transform AttachmentRef { get; protected set; }
    public Transform ItemTpv { get => itemTpv; protected set => itemTpv = value; }
    public Transform ItemTpvOrSelf => itemTpv ? itemTpv : transform;
    public bool IsFpv { get; set; }
    public bool IsAnimationSet => !Destroyed && ((IsFpv && fpvSet) || (!IsFpv && tpvSet));
    public bool IsCurrent { get; internal set; }
    public bool Destroyed { get; protected set; }
    public Transform PlayerAnimatorTrans { get; private set; }
    public Animator ItemAnimator => IsFpv ? ItemAnimatorFpv : ItemAnimatorTpv;
    public Transform ItemCurrent => IsFpv ? ItemFpv : ItemTpv;
    public Transform ItemCurrentOrDefault => IsFpv ? ItemFpv : ItemTpvOrSelf;
    public AnimationGraphBuilder GraphBuilder { get; private set; }
    public abstract bool UseGraph { get; }
    public abstract Transform PlayerOriginTransform { get; protected set; }
    public abstract bool IsRiggedWeapon { get; }
#if NotEditor
    public int SlotIndex { get; private set; } = -1;
#endif

    protected abstract Animator ItemAnimatorFpv { get; }
    protected virtual Animator ItemAnimatorTpv => itemAnimatorTpv;

    private Transform spine1, spine2, spine3;

    protected virtual void Awake()
    {
        foreach (var bindings in GetComponentsInChildren<TransformActivationBinding>(true))
        {
            bindings.targets = this;
            list_activate_attachments.AddRange(bindings.AllBindings);
        }
#if NotEditor
        gameObject.GetOrAddComponent<AttachmentReference>().attachmentReference = AttachmentRef;
#endif
        if (itemTpv)
        {
            rigTpv = itemTpv.GetComponentsInChildren<Rig>(true);
#if NotEditor
            if (rigTpv.Length > 0)
            {
                int uid = TypeBasedUID<AnimationTargetsAbs>.UID;
                foreach (var rig in rigTpv)
                {
                    rig.gameObject.name += $"_UID_{uid}";
                    AnimationRiggingManager.AddRigExcludeName(rig.gameObject.name);
                }
            }
            rigLayerTpv = new RigLayer[rigTpv.Length];
#endif
            itemTpv.gameObject.SetActive(false);
        }
    }

#if NotEditor
    //attaching the same prefab multiple times is not allowed!
    public void AttachPrefab(GameObject prefab)
    {
        if (Destroyed || dict_attachments == null || !prefab.TryGetComponent<AttachmentReferenceAppended>(out var appended) || !appended)
        {
            return;
        }
        appended.Merge(this);
        dict_attachments[prefab.name] = prefab.gameObject;
        if (!list_attached_attachments.Contains(prefab))
        {
            list_attached_attachments.Add(prefab);
        }
    }

    public GameObject GetPrefab(string name)
    {
        if (Destroyed || dict_attachments == null || !dict_attachments.TryGetValue(name, out var prefab))
        {
            return null;
        }
        return prefab;
    }

    public GameObject RemovePrefab(string name)
    {
        if (Destroyed || dict_attachments == null || !dict_attachments.TryGetValue(name, out var prefab))
        {
            return null;
        }
        if (prefab.TryGetComponent<AttachmentReferenceAppended>(out var reference))
        {
            reference.Remove();
        }
        dict_attachments.Remove(name);
        list_attached_attachments.Remove(prefab);
        return prefab;
    }

    public Transform GetAttachmentPathOverride(string path, bool onlyActive)
    {
        for (int i = list_activate_attachments.Count - 1; i >= 0; i--)
        {
            if (!list_activate_attachments[i] || !list_activate_attachments[i].activeInHierarchy)
            {
                continue;
            }
            Transform child = onlyActive ? GameUtils.FindDeepChildActive(list_activate_attachments[i].transform, path) : GameUtils.FindDeepChild(list_activate_attachments[i].transform, path);
            if (child)
            {
                return child;
            }
        }
        for (int i = list_attached_attachments.Count - 1; i >= 0; i--)
        {
            if (!list_attached_attachments[i] || !list_attached_attachments[i].activeInHierarchy)
            {
                continue;
            }
            Transform child = onlyActive ? GameUtils.FindDeepChildActive(list_attached_attachments[i].transform, path) : GameUtils.FindDeepChild(list_attached_attachments[i].transform, path);
            if (child)
            {
                return child;
            }
        }
        return null;
    }
#endif

    public void Init(Transform playerAnimatorTrans, bool isFpv
#if NotEditor
        , int idx
#endif
        )
    {
        if (Destroyed || (isFpv && fpvSet) || (!isFpv && tpvSet))
        {
            return;
        }
        if (!playerAnimatorTrans)
        {
            Destroy();
            return;
        }
        var animator = playerAnimatorTrans.GetComponentInChildren<Animator>(true);
        if (!animator)
        {
            Destroy();
            return;
        }
#if NotEditor
        SlotIndex = idx;
#endif
        fpvSet = false;
        tpvSet = false;
        playerAnimatorTrans = animator.transform;
        PlayerAnimatorTrans = playerAnimatorTrans;
        GraphBuilder = playerAnimatorTrans.AddMissingComponent<AnimationGraphBuilder>();
        GraphBuilder.Init();
        IsFpv = isFpv;
        if (!isFpv)
        {
            itemAnimatorTpv = animator;
        }
        else
        {
            itemAnimatorTpv = null;
        }
        spine1 = PlayerAnimatorTrans.FindInAllChildren("Spine1");
        spine2 = spine1.Find("Spine2");
        spine3 = spine2.Find("Spine3");

#if NotEditor
        Utils.SetLayerRecursively(gameObject, 24, Utils.ExcludeLayerZoom);
        if (ItemFpv)
        {
            Utils.SetLayerRecursively(ItemFpv.gameObject, 10, Utils.ExcludeLayerZoom);
        }
        if (ItemTpv)
        {
            Utils.SetLayerRecursively(ItemTpv.gameObject, 24, Utils.ExcludeLayerZoom);
        }
#endif
        if (ItemTpv)
        {
            ItemTpv.parent = isFpv ? playerAnimatorTrans.parent : playerAnimatorTrans;
            ItemTpv.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            ItemTpv.localScale = Vector3.one;
        }
        if (!Destroyed)
        {
            Init();
            SetEnabled(false);
            //Log.Out($"Init rig\n{StackTraceUtility.ExtractStackTrace()}");
        }
    }

    protected virtual void Init()
    {
        if (!itemTpv)
        {
            return;
        }

        itemTpv.SetParent(PlayerAnimatorTrans.parent);
        itemTpv.position = Vector3.zero;
        itemTpv.localPosition = Vector3.zero;
        itemTpv.localRotation = Quaternion.identity;
        if (!IsFpv)
        {
            itemAnimatorTpv = PlayerAnimatorTrans.GetComponent<Animator>();
            if (rigTpv.Length > 0)
            {
                foreach (var rig in rigTpv)
                {
                    if (rig.TryGetComponent<RigConverter>(out var rc))
                    {
                        rc.targetRoot = PlayerAnimatorTrans;
                        rc.Rebind();
                    }
                }
            }
        }
        else
        {
            itemAnimatorTpv = null;
        }
    }

    public void Setup()
    {
        if (!PlayerAnimatorTrans)
        {
            Destroy();
            return;
        }
        if (IsFpv && ItemFpv && !fpvSet)
        {
            fpvSet = SetupFpv();
        }
        else if (!IsFpv && ItemTpv && !tpvSet)
        {
            tpvSet = SetupTpv();
        }
    }

    protected abstract bool SetupFpv();

    protected virtual bool SetupTpv()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        itemTpv.SetParent(itemAnimatorTpv.transform.FindInAllChildren(GetParentName(parentNameTpv)));
        itemTpv.position = Vector3.zero;
        itemTpv.localPosition = Vector3.zero;
        itemTpv.localRotation = Quaternion.identity;

        GraphBuilder.InitWeapon(ItemTpv, weaponRuntimeControllerTpv, weaponRigMaskTpv);

        var rigBuilder = PlayerAnimatorTrans.AddMissingComponent<RigBuilder>();
#if NotEditor
        foreach (var layer in rigBuilder.layers)
        {
            if (layer.name == SDCSUtils.IKRIG)
            {
                layer.active = false;
            }
        }
#endif
        if (rigTpv.Length > 0)
        {
            rigBuilder.layers.RemoveAll(r => rigLayerTpv.Any(layer => layer?.name == r.name));
            for (int i = 0; i < rigTpv.Length; i++)
            {
                rigBuilder.layers.Insert(i, rigLayerTpv[i] = new RigLayer(rigTpv[i], true));
            }
        }
        BuildRig(PlayerAnimatorTrans.GetComponent<Animator>(), rigBuilder);

        sw.Stop();
        string info = $"setup tpv animation graph took {sw.ElapsedMilliseconds} ms";
        //info += $"\n{StackTraceUtility.ExtractStackTrace()}";
        Log.Out(info);
        return true;
    }

    public void Remove()
    {
        if (!PlayerAnimatorTrans)
        {
            Destroy();
            return;
        }
        if (IsFpv && ItemFpv && fpvSet)
        {
            RemoveFpv();
            fpvSet = false;
        }
        else if (!IsFpv && ItemTpv && tpvSet)
        {
            RemoveTpv();
            tpvSet = false;
        }
    }

    protected abstract void RemoveFpv();

    protected virtual void RemoveTpv()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        itemTpv.SetParent(PlayerAnimatorTrans.parent);
        itemTpv.position = Vector3.zero;
        itemTpv.localPosition = Vector3.zero;
        itemTpv.localRotation = Quaternion.identity;

        var rigBuilder = PlayerAnimatorTrans.AddMissingComponent<RigBuilder>();
#if NotEditor
        foreach (var layer in rigBuilder.layers)
        {
            if (layer.name == SDCSUtils.IKRIG)
            {
                layer.active = true;
            }
        }
#endif
        if (rigTpv.Length > 0)
        {
            rigBuilder.layers.RemoveAll(r => rigLayerTpv.Any(layer => layer?.name == r.name));
            Array.Clear(rigLayerTpv, 0, rigLayerTpv.Length);

            //rigTpv.transform.SetParent(transform, false);
            //rigTpv.gameObject.SetActive(false);
        }
        BuildRig(PlayerAnimatorTrans.GetComponent<Animator>(), rigBuilder);

        sw.Stop();
        string info = $"destroy tpv animation graph took {sw.ElapsedMilliseconds} ms";
        //info += $"\n{StackTraceUtility.ExtractStackTrace()}";
        Log.Out(info);
    }

    public virtual void Destroy()
    {

        if (AttachmentRef)
        {
            AttachmentRef.parent = transform;
            AttachmentRef = null;
        }

        if (IsCurrent)
        {
            GraphBuilder.SetCurrentTarget(null);
        }
        DestroyFpv();
        DestroyTpv();
#if NotEditor
        Destroyed = true;
#endif
        PlayerAnimatorTrans = null;
        dict_attachments = null;
        list_attached_attachments = null;
        list_activate_attachments = null;

        Component.Destroy(this);
        //Log.Out(StackTraceUtility.ExtractStackTrace());
    }

    public virtual void DestroyFpv()
    {
        if (ItemFpv)
        {
            ItemFpv.parent = null;
            GameObject.Destroy(ItemFpv.gameObject);
        }
        fpvSet = false;
        ItemFpv = null;
        Log.Out("destroy fpv");
    }

    public virtual void DestroyTpv()
    {
        if (ItemTpv)
        {
            ItemTpv.parent = null;
            GameObject.Destroy(ItemTpv.gameObject);
        }
        tpvSet = false;
        ItemTpv = null;
        Log.Out("destroy tpv");
    }

    public virtual void SetEnabled(bool enabled)
    {
        if (Destroyed)
        {
            return;
        }
        if (AttachmentRef)
        {
            AttachmentRef.parent = enabled ? (IsFpv ? ItemFpv : ItemTpvOrSelf) : transform;
        }
        if (enabled)
        {
            if (ItemFpv)
            {
                ItemFpv.gameObject.SetActive(IsFpv);
            }
            if (ItemTpv)
            {
                ItemTpv.gameObject.SetActive(!IsFpv);
            }
            Setup();
        }
        else
        {
            Remove();
            if (ItemFpv)
            {
                ItemFpv.gameObject.SetActive(false);
            }
            if (ItemTpv)
            {
                ItemTpv.gameObject.SetActive(false);
            }
        }
        if (ItemTpv)
        {
            gameObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(!IsFpv);
        }
    }

    protected void BuildRig(Animator animator, RigBuilder rb)
    {
        animator.UnbindAllStreamHandles();
        animator.UnbindAllSceneHandles();
        rb.Build();
        animator.Rebind();
    }

    private readonly static int[] resetHashes = new int[]
    {
        Animator.StringToHash("Reload"),
        Animator.StringToHash("PowerAttack"),
        Animator.StringToHash("UseItem"),
        Animator.StringToHash("ItemUse"),
        Animator.StringToHash("WeaponFire")
    };

#if NotEditor
    //VRoid switch view workaround
    public void OnEnable()
    {
        var player = this.GetLocalPlayerInParent();
        if ((player && player.bFirstPersonView) || ItemTpv)
        {
            gameObject.SetActive(false);
        }
    }

    public virtual void UpdatePlayerAvatar(AvatarController avatarController, bool rigWeaponChanged)
    {
        //var itemCurrent = ItemCurrent;
        //if (itemCurrent && !itemCurrent.gameObject.activeSelf)
        //{
        //    Log.Out("Rigged weapon not active, enabling it...");
        //    SetEnabled(true);
        //}
        if (IsAnimationSet)
        {
            foreach (var hash in resetHashes)
            {
                var role = GraphBuilder.GetWrapperRoleByParamHash(hash);
                if (role == AnimationGraphBuilder.ParamInWrapper.Vanilla || role == AnimationGraphBuilder.ParamInWrapper.Both)
                { 
                    GraphBuilder.VanillaWrapper.ResetTrigger(hash);
                }
            }
        }
        if (IsFpv && fpvSet)
        {
            GraphBuilder.VanillaWrapper.Play("idle", 0, 0f);
            GraphBuilder.VanillaWrapper.SetInteger(AvatarController.weaponHoldTypeHash, -1);
        }
        else if (!IsFpv && tpvSet)
        {
            //avatarController.UpdateInt(AvatarController.weaponHoldTypeHash, 0, false);
            //GraphBuilder.VanillaWrapper.Play("Unarmed", GraphBuilder.VanillaWrapper.GetLayerIndex("StandingIdleTurn"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("RightHandHoldPoses"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("RangedRightHandHoldPoses"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("AdditiveOffsetHoldPoses"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("RightArmHoldPoses"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("BothArmsHoldPoses"), 0);
            //GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("AdditiveAimPoses"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("UpperBodyAttack"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("BowDrawAndFire"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("UpperBodyUseAndReload"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("AdditiveRangedAttack"), 0);
        }
    }

    //public void UpdateTpvSpineRotation(EntityPlayer player)
    //{
    //    if (!IsFpv && tpvSet && player && !player.IsDead())
    //    {
    //        float xOffset = player.rotation.x / 3f;
    //        float yOffset = 0f;
    //        if (player.IsCrouching)
    //        {
    //            xOffset += 10f;
    //            yOffset += 5f;
    //        }
    //        if (player.MovementState > 0)
    //        {
    //            xOffset += player.speedForward;
    //        }
    //        if (Time.timeScale > 0.001f)
    //        {
    //            spine1.transform.localEulerAngles = new Vector3(spine1.transform.localEulerAngles.x - xOffset, spine1.transform.localEulerAngles.y - yOffset, spine1.transform.localEulerAngles.z);
    //            spine2.transform.localEulerAngles = new Vector3(spine2.transform.localEulerAngles.x - xOffset, spine2.transform.localEulerAngles.y - yOffset, spine2.transform.localEulerAngles.z);
    //            spine3.transform.localEulerAngles = new Vector3(spine3.transform.localEulerAngles.x - xOffset, spine3.transform.localEulerAngles.y - yOffset, spine3.transform.localEulerAngles.z);
    //            return;
    //        }
    //        spine1.transform.localEulerAngles = new Vector3(-xOffset, spine1.transform.localEulerAngles.y - yOffset, spine1.transform.localEulerAngles.z);
    //        spine2.transform.localEulerAngles = new Vector3(-xOffset, spine2.transform.localEulerAngles.y - yOffset, spine2.transform.localEulerAngles.z);
    //        spine3.transform.localEulerAngles = new Vector3(-xOffset, spine3.transform.localEulerAngles.y - yOffset, spine3.transform.localEulerAngles.z);
    //    }
    //}

#else
    public void Update()
    {
        if (IsAnimationSet)
        {
            foreach (var hash in resetHashes)
            {
                var role = GraphBuilder.GetWrapperRoleByParamHash(hash);
                if (role == AnimationGraphBuilder.ParamInWrapper.Vanilla || role == AnimationGraphBuilder.ParamInWrapper.Both)
                { 
                    GraphBuilder.VanillaWrapper.ResetTrigger(hash);
                }
            }
        }
        if (IsFpv && fpvSet)
        {
            GraphBuilder.VanillaWrapper.Play("idle", 0, 0f);
        }
        else if (!IsFpv && tpvSet)
        {
            //GraphBuilder.VanillaWrapper.Play("Unarmed", GraphBuilder.VanillaWrapper.GetLayerIndex("StandingIdleTurn"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("RightHandHoldPoses"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("RangedRightHandHoldPoses"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("AdditiveOffsetHoldPoses"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("RightArmHoldPoses"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("BothArmsHoldPoses"), 0);
            //GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("AdditiveAimPoses"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("UpperBodyAttack"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("BowDrawAndFire"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("UpperBodyUseAndReload"), 0);
            GraphBuilder.VanillaWrapper.Play("Empty", GraphBuilder.VanillaWrapper.GetLayerIndex("AdditiveRangedAttack"), 0);
        }
    }
#endif
}