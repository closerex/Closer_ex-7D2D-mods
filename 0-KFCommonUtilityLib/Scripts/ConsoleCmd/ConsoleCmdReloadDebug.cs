﻿using KFCommonUtilityLib.Scripts.Singletons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ConsoleCmdReloadDebug : ConsoleCmdAbstract
{
    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (player != null)
        {
            var inv = player.inventory;
            var holdingGun = inv.GetHoldingGun() as ItemActionRanged;
            var holdingGunData = (inv.holdingItemData.actionData[0] as ItemActionRanged.ItemActionDataRanged);
            var reference = AnimationRiggingManager.FpvTransformReference;
            Log.Out($"\n" +
                $"holding item idx: {inv.holdingItemIdx} name: {inv.holdingItem.Name} isReloading: {holdingGunData.isReloading} canReload: {holdingGun.CanReload(holdingGunData)} isReloadCancelled: {holdingGunData.isReloadCancelled}\n" +
                $"hand item: {((AvatarLocalPlayerController)player.emodel.avatarController).HeldItemTransform.name}\n" +
                $"rigging item is Idle: {reference.fpvAnimator.GetCurrentAnimatorStateInfo(0).IsName("1stP_Idle")} reload updated this frame: {reference.targets.itemFpv.GetComponentInChildren<AnimationReloadEvents>().ReloadUpdatedThisFrame} animator state: {reference.fpvAnimator.isActiveAndEnabled}");
        }
    }

    protected override string[] getCommands()
    {
        return new []{ "reloaddebug", "rd" };
    }

    protected override string getDescription()
    {
        return "Troubleshooting reload related issues.";
    }
}