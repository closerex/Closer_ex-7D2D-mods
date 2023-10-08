#if !UNITY_EDITOR
using KFCommonUtilityLib.Scripts.Singletons;
#endif
using System;
using System.Collections;
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
    //[SerializeField]
    //public bool takeOverReloadTime = true;

    [NonSerialized]
    private Transform fpsArms;
    [NonSerialized]
    private RigLayer rigLayer;

    private float weight;

#if !UNITY_EDITOR
    private void Awake()
    {
        itemFpv.gameObject.SetActive(false);
        rig.gameObject.SetActive(false);
        gameObject.AddMissingComponent<AttachmentReference>().attachmentReference = attachmentReference;
    }
#endif

    public void Init(Transform fpsArms)
    {
        if (fpsArms == null)
        {
            Destroy();
            return;
        }
        Stopwatch sw = new Stopwatch();
        sw.Start();

        var itemAnimator = itemFpv.GetComponentInChildren<Animator>();
        //itemAnimator.keepAnimatorStateOnDisable = true;

        var animator = fpsArms.GetComponentInChildren<Animator>();
        fpsArms = animator.transform;
        this.fpsArms = fpsArms;
        itemFpv.SetParent(fpsArms.parent, false);
        itemFpv.position = Vector3.zero;
        itemFpv.localPosition = Vector3.zero;
        itemFpv.localRotation = Quaternion.identity;
        rig.transform.SetParent(fpsArms, false);
        rig.transform.position = Vector3.zero;
        rig.transform.localPosition = Vector3.zero;
        rig.transform.localRotation = Quaternion.identity;
#if !UNITY_EDITOR
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
        rigLayer = new RigLayer(rig, enabled);
        rigBuilder.layers.Add(rigLayer);
        rigBuilder.Build();
        animator.ResolveAllSceneHandles();
        animator.ResolveAllStreamHandles();
        animator.Rebind();

        //animator.Update(0);
        //itemAnimator.Update(0);

        sw.Stop();
        string info = $"setup animation rig took {sw.ElapsedMilliseconds} ms";
        Log.Out(info);

        SetEnabled(false, true);
    }

    public void Destroy()
    {
        if (fpsArms == null)
        {
            attachmentReference?.SetParent(transform);
            GameObject.Destroy(itemFpv.gameObject);
            GameObject.Destroy(rig.gameObject);
            Component.Destroy(this);
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
        //animator.Update(0);

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

        //itemFpv.GetComponentInChildren<Animator>().updateMode = AnimatorUpdateMode.AnimatePhysics;
        //itemFpv.GetComponentInChildren<Animator>().updateMode = AnimatorUpdateMode.Normal;
        attachmentReference?.SetParent(enabled ? itemFpv : transform, false);
        var rigBuilder = fpsArms.AddMissingComponent<RigBuilder>();
        //if (!enabled)
        //    rigBuilder.enabled = false;
        itemFpv.gameObject.SetActive(enabled);
        rigLayer.active = enabled;
        rig.gameObject.SetActive(enabled);
        itemFpv.localPosition = new Vector3(0, 0, enabled ? 0 : -100);
#if UNITY_EDITOR
        //if (enabled)
        //    rigBuilder.enabled = true;
#endif
#if !UNITY_EDITOR
        //if (AnimationRiggingManager.IsHoldingRiggedWeapon)
        //    rigBuilder.enabled = true;
#endif

        gameObject.SetActive(forceDisableRoot ? false : !enabled);
    }
}
