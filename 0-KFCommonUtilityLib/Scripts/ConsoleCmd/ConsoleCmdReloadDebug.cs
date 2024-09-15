using KFCommonUtilityLib.Scripts.StaticManagers;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class ConsoleCmdReloadDebug : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
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
                $"rigging item is Idle: {reference.fpvAnimator.GetCurrentAnimatorStateInfo(0).IsName("1stP_Idle")} animator state: {reference.fpvAnimator.isActiveAndEnabled}" +
                $"\n{((AvatarLocalPlayerController)player.emodel.avatarController).FPSArms.animator.GetComponent<RigBuilder>().layers.Select(l => l.name + $": active {l.active} weight {l.rig.weight}\n" + PrintRigAndTransform(l.rig)).Join()}");
        }
    }

    private static string PrintRigAndTransform(Rig parent)
    {
        string str = "";
        foreach (var child in parent.GetComponentsInChildren<MultiRotationConstraint>())
        {
            str += "".PadLeft(4) + $"{child.name} weight {child.weight} constrained {child.data.constrainedObject.name}/pos:{child.data.constrainedObject.localPosition}/rot:{child.data.constrainedObject.localEulerAngles}\n";
            foreach (var source in child.data.sourceObjects)
            {
                str += "".PadLeft(8) + $"source {source.transform.name}/pos:{source.transform.localPosition}/rot:{source.transform.localEulerAngles} weight {source.weight}\n";
            }
        }
        foreach (var child in parent.GetComponentsInChildren<TwoBoneIKConstraint>())
        {
            str += "".PadLeft(4) + $"{child.name} weight {child.weight} constrained {child.data.target.name}/pos:{child.data.target.localPosition}/rot:{child.data.target.localEulerAngles}/pos weight:{child.data.targetPositionWeight}/rot weight:{child.data.targetRotationWeight}\n";
        }
        return str;
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