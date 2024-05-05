#if NotEditor
using KFCommonUtilityLib.Scripts.StaticManagers;
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

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var player = GameManager.Instance.World?.GetPrimaryPlayer();
        SetAmmoCountForEntity(player);
    }

    public static void SetAmmoCountForEntity(EntityAlive entity)
    {
        if (entity)
        {
            if (entity.inventory?.holdingItemData?.actionData != null)
            {
                var mapping = MultiActionManager.GetMappingForEntity(entity.entityId);
                if (mapping != null)
                {
                    var metaIndices = mapping.indices;
                    for (int i = 0; i < mapping.ModeCount; i++)
                    {
                        int metaIndex = metaIndices.GetMetaIndexForMode(i);
                        int meta = entity.inventory.holdingItemItemValue.GetMetaByMode(i);
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
                    entity.emodel.avatarController.UpdateInt(hash_states[0], entity.inventory.holdingItemItemValue.Meta);
                    if (ConsoleCmdReloadLog.LogInfo)
                    {
                        Log.Out($"Setting ammoCount to {entity.inventory.holdingItemItemValue.Meta}, stack trace:\n{StackTraceUtility.ExtractStackTrace()}");
                    }
                    //animator.SetInteger(hash_states[0], entity.inventory.holdingItemItemValue.Meta);
                }
            }
        }
    }
#endif
}