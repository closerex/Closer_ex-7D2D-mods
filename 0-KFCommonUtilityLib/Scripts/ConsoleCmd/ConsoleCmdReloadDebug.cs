using KFCommonUtilityLib.Scripts.StaticManagers;
using System.Collections.Generic;

public class ConsoleCmdReloadDebug : ConsoleCmdAbstract
{
    public override int DefaultPermissionLevel => 1000;

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (player != null)
        {
            var inv = player.inventory;
            var holdingGun = inv.GetHoldingGun() as ItemActionRanged;
            var holdingGunData = (inv.holdingItemData.actionData[holdingGun.ActionIndex] as ItemActionRanged.ItemActionDataRanged);
            var reference = AnimationRiggingManager.FpvTransformReference;
            Log.Out($"\n" +
                $"holding item idx: {inv.holdingItemIdx} name: {inv.holdingItem.Name} isReloading: {holdingGunData.isReloading} canReload: {holdingGun.CanReload(holdingGunData)} isReloadCancelled: {holdingGunData.isReloadCancelled}\n" +
                $"hand item: {((AvatarLocalPlayerController)player.emodel.avatarController).HeldItemTransform.name}\n" +
                $"rigging item is Idle: {reference.fpvAnimator.GetCurrentAnimatorStateInfo(0).IsName("1stP_Idle")} animator state: {reference.fpvAnimator.isActiveAndEnabled}");
        }
    }

    public override string[] getCommands()
    {
        return new[] { "reloaddebug", "rdebug" };
    }

    public override string getDescription()
    {
        return "Troubleshooting reload related issues.";
    }
}