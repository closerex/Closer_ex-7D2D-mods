#if NotEditor
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Utilities;
#endif
using UnityEngine;

public class AnimationAmmoUpdateState : StateMachineBehaviour
{
#if NotEditor
    private static int[] hash_states = new[]
    {
        Animator.StringToHash("ammoCount"),
        Animator.StringToHash("ammoCount1"),
        Animator.StringToHash("ammoCount2"),
        Animator.StringToHash("ammoCount3"),
        Animator.StringToHash("ammoCount4")
    };
    //private InventorySlotGurad slotGuard = new InventorySlotGurad();

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        //var player = GameManager.Instance.World?.GetPrimaryPlayer();
        var player = animator.GetComponentInParent<EntityAlive>();
        //if(slotGuard.IsValid(player))
        //{
        //    SetAmmoCountForEntity(player, slotGuard.Slot);
        //}
        if (player)
        {
            var targets = AnimationRiggingManager.GetActiveRigTargetsFromPlayer(player);
            if (targets)
            {
                SetAmmoCountForEntity(player, targets.SlotIndex);
            }
        }
    }

    public static void SetAmmoCountForEntity(EntityAlive entity, int slot)
    {
        if (entity)
        {
            var invData = entity.inventory?.slots?[slot];
            if (invData?.actionData != null)
            {
                //var mapping = MultiActionManager.GetMappingForEntity(entity.entityId);
                var mapping = (invData.actionData[0] as IModuleContainerFor<ActionModuleAlternative.AlternativeData>)?.Instance?.mapping;
                if (mapping != null)
                {
                    var metaIndices = mapping.indices;
                    for (int i = 0; i < mapping.ModeCount; i++)
                    {
                        int metaIndex = metaIndices.GetMetaIndexForMode(i);
                        int meta = invData.itemValue.GetMetaByMode(i);
                        entity.emodel.avatarController.UpdateInt(hash_states[metaIndex], meta);
                        if (ConsoleCmdReloadLog.LogInfo)
                        {
                            Log.Out($"Setting ammoCount{(metaIndex > 0 ? metaIndex.ToString() : "")} to {meta}, stack trace:\n{StackTraceUtility.ExtractStackTrace()}");
                        }
                        //animator.SetInteger(hash_states[metaIndex], meta);
                    }
                }
                else
                {
                    entity.emodel.avatarController.UpdateInt(hash_states[0], invData.itemValue.Meta);
                    if (ConsoleCmdReloadLog.LogInfo)
                    {
                        Log.Out($"Setting ammoCount to {invData.itemValue.Meta}, stack trace:\n{StackTraceUtility.ExtractStackTrace()}");
                    }
                    //animator.SetInteger(hash_states[0], entity.inventory.holdingItemItemValue.Meta);
                }
            }
        }
    }
#endif
}