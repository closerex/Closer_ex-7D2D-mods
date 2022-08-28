using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;


namespace VehicleWeaponPatches
{
    [HarmonyPatch(typeof(EntityVehicle))]
    class VehicleManagerPatch
    {
        [HarmonyPatch(nameof(EntityVehicle.Kill))]
        [HarmonyPostfix]
        private static void Postfix_Kill(EntityVehicle __instance)
        {
            var manager = __instance.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
            if (manager != null)
                manager.Cleanup();
        }

        [HarmonyPatch(nameof(EntityVehicle.AttachEntityToSelf))]
        [HarmonyPostfix]
        private static void Postfix_AttachEntityToSelf(Entity _entity, int __result, EntityVehicle __instance)
        {
            if(__result >= 0 && _entity is EntityPlayerLocal)
            {
                var manager = __instance.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
                if (manager != null)
                    manager.OnPlayerEnter(__result);
            }
        }

        [HarmonyPatch("DetachEntity")]
        [HarmonyPostfix]
        private static void Postfix_DetachEntity(Entity _entity, EntityVehicle __instance)
        {
            if (_entity is EntityPlayerLocal)
            {
                var manager = __instance.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
                if (manager != null)
                    manager.OnPlayerDetach();
            }
        }
    }

    [HarmonyPatch(typeof(VehicleManager), nameof(VehicleManager.RemoveAllVehiclesFromMap))]
    public class VehicleCleanupPatch
    {
        private static bool Prefix(VehicleManager __instance, List<EntityVehicle> ___vehiclesActive)
        {
            foreach(var entity in ___vehiclesActive)
            {
                var manager = entity.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
                if (manager != null)
                    manager.Cleanup();
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(vp_FPCamera), "Update3rdPerson")]
    public class CameraPositionPatch
    {
        private static void Postfix(vp_FPCamera __instance)
        {
            __instance.transform.localPosition += VPWeaponManager.CameraOffset;
        }
    }

    [HarmonyPatch(typeof(Vehicle))]
    public class VehicleModPatch
    {
        [HarmonyPatch(nameof(Vehicle.CreateParts))]
        [HarmonyPostfix]
        private static void Postfix_CreateParts(Vehicle __instance, ItemValue ___itemValue)
        {
            if (__instance.FindPart(VPWeaponManager.VehicleWeaponManagerName) is VPWeaponManager manager)
                manager.ApplyModEffect(___itemValue.Clone());
        }

        [HarmonyPatch(nameof(Vehicle.SetItemValue))]
        [HarmonyPostfix]
        private static void Postfix_SetItemValue(Vehicle __instance, ItemValue ___itemValue)
        {
            if (__instance.FindPart(VPWeaponManager.VehicleWeaponManagerName) is VPWeaponManager manager)
                manager.ApplyModEffect(___itemValue.Clone());
        }

        [HarmonyPatch(nameof(Vehicle.SetItemValueMods))]
        [HarmonyPostfix]
        private static void Postfix_SetItemValueMods(Vehicle __instance, ItemValue ___itemValue)
        {
            if (__instance.FindPart(VPWeaponManager.VehicleWeaponManagerName) is VPWeaponManager manager)
                manager.ApplyModEffect(___itemValue.Clone());
        }
    }

    [HarmonyPatch(typeof(PlayerMoveController), "Update")]
    public class PlayerControllerPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo mtd_horn = AccessTools.Method(typeof(EntityVehicle), nameof(EntityVehicle.UseHorn));
            MethodInfo mtd_xui = AccessTools.Method(typeof(XUiC_Radial), nameof(XUiC_Radial.SetActivatableItemData));
            FieldInfo fld_player = AccessTools.Field(typeof(PlayerMoveController), "entityPlayerLocal");
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if(codes[i].Calls(mtd_horn))
                {
                    for (int j = i - 1; j > 0; j--)
                    {
                        if (codes[j].Calls(mtd_xui))
                        {
                            var insert = new CodeInstruction[]
                            {
                                new CodeInstruction(OpCodes.Ldarg_0),
                                CodeInstruction.LoadField(typeof(PlayerMoveController), "entityPlayerLocal"),
                                CodeInstruction.Call(typeof(PlayerControllerPatch), nameof(PlayerControllerPatch.CheckForSwitchingSeat))
                            };
                            insert[0].MoveLabelsFrom(codes[j + 1]);
                            codes.InsertRange(j + 1, insert);
                            break;
                        }
                    }
                    break;
                }
            }

            return codes;
        }

        private static void CheckForSwitchingSeat(EntityPlayerLocal player)
        {
            int seat = PlayerActionsVehicleWeapon.Instance.ActivateSlotWasPressed;
            if(PlayerActionsVehicleWeapon.Instance.HoldSwitchSeat.IsPressed && seat >= 0 && seat < player.AttachedToEntity.GetAttachMaxCount() && seat != player.AttachedToEntity.FindAttachSlot(player))
            {
                if (ConnectionManager.Instance.IsServer)
                    GameManager.Instance.TrySwitchSeatServer(GameManager.Instance.World, player.entityId, player.AttachedToEntity.entityId, seat);
                else
                    ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageVehicleSwitchSeat>().Setup(player.entityId, player.AttachedToEntity.entityId, seat));
                Log.Out($"trying to switch seat to {PlayerActionsVehicleWeapon.Instance.ActivateSlotWasPressed}");
            }
        }
    }
}
