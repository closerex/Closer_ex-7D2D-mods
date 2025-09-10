#if NotEditor
using KFCommonUtilityLib;
#endif
using UnityEngine;

public class AimReferenceGroup : MonoBehaviour
{
    [SerializeField]
    public AimReference[] aimReferences;
    private bool registered;

#if NotEditor
    private ActionModuleProceduralAiming.ProceduralAimingData data;
    private void Awake()
    {
        if (aimReferences != null)
        {
            foreach (var reference in aimReferences)
            {
                reference.group = this;
            }
        }

        EntityPlayerLocal player = this.GetLocalPlayerInParent();
        if (aimReferences == null || aimReferences.Length == 0 || !player || player.inventory?.holdingItemData?.actionData?[1] is not IModuleContainerFor<ActionModuleProceduralAiming.ProceduralAimingData> module)
        {
            Destroy(this);
            if (aimReferences != null)
            { 
                foreach (var reference in aimReferences)
                {
                    Destroy(reference);
                }
                aimReferences = null;
            }
            return;
        }
        data = module.Instance;
    }

    private void OnEnable()
    {
        if (registered)
        {
            return;
        }
        registered = data.RegisterGroup(aimReferences, gameObject.name);
    }
#endif

    internal void UpdateEnableStates()
    {
#if NotEditor
        if (!registered)
        {
            return;
        }
        data.UpdateCurrentReference();
#endif
    }
}

