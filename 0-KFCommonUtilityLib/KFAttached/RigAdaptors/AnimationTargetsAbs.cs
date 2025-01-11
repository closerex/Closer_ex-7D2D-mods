using UnityEngine;

[AddComponentMenu("")]
public abstract class AnimationTargetsAbs : MonoBehaviour
{
    [SerializeField]
    protected Transform itemTpv;
    protected Animator itemAnimatorTpv;

    public abstract Transform ItemFpv { get; set; }
    public abstract Transform AttachmentRef { get; }
    public Transform ItemTpv { get => itemTpv; set => itemTpv = value; }
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

    public virtual void Init(Transform playerAnimatorTrans, bool isFpv)
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

        if (ItemTpv)
        {
            ItemTpv.parent = isFpv ? playerAnimatorTrans.parent : playerAnimatorTrans;
            ItemTpv.SetAsFirstSibling();
            ItemTpv.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            ItemTpv.localScale = Vector3.one;
        }

        SetEnabled(false);
    }

    public void Setup()
    {
        if (!PlayerAnimatorTrans)
        {
            Destroy();
            return;
        }
        if (IsFpv)
        {
            SetupFpv();
        }
        else
        {
            SetupTpv();
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
        if (IsFpv)
        {
            RemoveFpv();
        }
        else
        {
            RemoveTpv();
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
        }

        DestroyFpv();
        DestroyTpv();
        PlayerAnimatorTrans = null;

        Component.Destroy(this);
    }

    public void DestroyRemote()
    {
        if (ItemTpv)
        {
            DestroyFpv();
        }
        else
        {
            Destroy();
        }
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