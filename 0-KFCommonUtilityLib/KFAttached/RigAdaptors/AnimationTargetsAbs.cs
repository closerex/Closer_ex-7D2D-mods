using UnityEngine;

[AddComponentMenu("")]
public abstract class AnimationTargetsAbs : MonoBehaviour
{
    [SerializeField]
    protected Transform itemTpv;
    protected Animator itemAnimatorTpv;
    protected bool fpvSet = false;
    protected bool tpvSet = false;

    public abstract Transform ItemFpv { get; protected set; }
    public abstract Transform AttachmentRef { get; protected set; }
    public Transform ItemTpv { get => itemTpv; protected set => itemTpv = value; }
    public Transform ItemTpvOrSelf => itemTpv ? itemTpv : transform;
    public bool IsFpv { get; set; }
    public bool Destroyed { get; protected set; }
    protected Transform PlayerAnimatorTrans { get; private set; }
    public Animator ItemAnimator => IsFpv ? ItemAnimatorFpv : ItemAnimatorTpv;
    public Transform ItemCurrent => IsFpv ? ItemFpv : ItemTpv;
    public Transform ItemCurrentOrDefault => IsFpv ? ItemFpv : ItemTpvOrSelf;

    protected abstract Animator ItemAnimatorFpv { get; }
    protected virtual Animator ItemAnimatorTpv => itemAnimatorTpv;

    protected virtual void Awake()
    {
        foreach (var bindings in GetComponentsInChildren<TransformActivationBinding>(true))
        {
            bindings.targets = this;
        }
        gameObject.GetOrAddComponent<AttachmentReference>().attachmentReference = AttachmentRef;
    }

    public void Init(Transform playerAnimatorTrans, bool isFpv)
    {
        if (Destroyed)
        {
            return;
        }
        if (!playerAnimatorTrans)
        {
            Destroy();
            return;
        }
        fpvSet = false;
        tpvSet = false;
        var animator = playerAnimatorTrans.GetComponentInChildren<Animator>();
        playerAnimatorTrans = animator.transform;
        PlayerAnimatorTrans = playerAnimatorTrans;
        IsFpv = isFpv;
        if (!isFpv)
        {
            itemAnimatorTpv = animator;
        }
        else
        {
            itemAnimatorTpv = null;
        }

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
            ItemTpv.SetAsFirstSibling();
            ItemTpv.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            ItemTpv.localScale = Vector3.one;
        }

        Init();
        SetEnabled(false);
    }

    protected abstract void Init();

    public void Setup()
    {
        if (!PlayerAnimatorTrans)
        {
            Destroy();
            return;
        }
        if (IsFpv && !fpvSet)
        {
            SetupFpv();
            fpvSet = true;
        }
        else if (!IsFpv && !tpvSet)
        {
            SetupTpv();
            tpvSet = true;
        }
    }

    protected abstract void SetupFpv();

    protected virtual void SetupTpv()
    {

    }

    public void Remove()
    {
        if (!PlayerAnimatorTrans)
        {
            Destroy();
            return;
        }
        if (IsFpv && fpvSet)
        {
            RemoveFpv();
            fpvSet = false;
        }
        else if (!IsFpv && tpvSet)
        {
            RemoveTpv();
            tpvSet = false;
        }
    }

    protected abstract void RemoveFpv();

    protected virtual void RemoveTpv()
    {

    }

    public virtual void Destroy()
    {
#if NotEditor
        Destroyed = true;
#endif

        if (AttachmentRef)
        {
            AttachmentRef.parent = transform;
            AttachmentRef = null;
        }

        DestroyFpv();
        DestroyTpv();
        PlayerAnimatorTrans = null;

        Component.Destroy(this);
    }

    public virtual void DestroyFpv()
    {
        if (ItemFpv)
        {
            if (IsFpv && PlayerAnimatorTrans)
            {
                RemoveFpv();
            }
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
            if (!IsFpv && PlayerAnimatorTrans)
            {
                RemoveTpv();
            }
            ItemTpv.parent = null;
            GameObject.Destroy(ItemTpv.gameObject);
        }
        tpvSet = false;
        ItemTpv = null;
        Log.Out("destroy tpv");
    }

    public virtual void SetEnabled(bool enabled)
    {
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

#if NotEditor
    //VRoid switch view workaround
    public void OnEnable()
    {
        var player = GetComponentInParent<EntityPlayerLocal>();
        if ((player != null && player.bFirstPersonView) || ItemTpv)
        {
            gameObject.SetActive(false);
        }
    }

    public virtual void UpdatePlayerAvatar(AvatarController avatarController, bool rigWeaponChanged)
    {
        var itemCurrent = ItemCurrent;
        if (itemCurrent && !itemCurrent.gameObject.activeSelf)
        {
            Log.Out("Rigged weapon not active, enabling it...");
            SetEnabled(true);
        }
    }
#endif
}