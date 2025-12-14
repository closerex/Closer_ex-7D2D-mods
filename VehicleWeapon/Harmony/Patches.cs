using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;


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

        //[HarmonyPatch(nameof(EntityVehicle.AttachEntityToSelf))]
        //[HarmonyPostfix]
        //private static void Postfix_AttachEntityToSelf(Entity _entity, int __result, EntityVehicle __instance)
        //{
        //    if(__result >= 0 && _entity is EntityPlayerLocal)
        //    {
        //        PlayerActionsVehicleExtra.Instance.Enabled = true;
        //        Log.Out(PlayerActionsVehicleExtra.Instance.Enabled.ToString());
        //        var manager = __instance.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
        //        if (manager != null)
        //            manager.OnPlayerEnter(__result);
        //    }
        //}

        [HarmonyPatch(nameof(EntityVehicle.DetachEntity))]
        [HarmonyPostfix]
        private static void Postfix_DetachEntity(Entity _entity, EntityVehicle __instance)
        {
            if (_entity is EntityPlayerLocal)
            {
                PlayerActionsVehicleExtra.Instance.Enabled = false;
                var manager = __instance.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
                if (manager != null)
                    manager.OnPlayerDetach();
            }
        }
    }

    [HarmonyPatch(typeof(EntityPlayerLocal))]
    public class EntityPlayerLocalPatches
    {
        [HarmonyPatch(nameof(EntityPlayerLocal.AttachToEntity))]
        [HarmonyPostfix]
        private static void Postfix_AttachToEntity(EntityPlayerLocal __instance, int __result)
        {
            if (__result < 0)
                return;

            if(__instance.AttachedToEntity is EntityVehicle entity)
            {
                PlayerActionsVehicleExtra.Instance.Enabled = true;
                var manager = entity.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
                if (manager != null)
                    manager.OnPlayerEnter(__result);
            }
        }
    }

    [HarmonyPatch(typeof(VehicleManager), nameof(VehicleManager.RemoveAllVehiclesFromMap))]
    public class VehicleCleanupPatch
    {
        private static bool Prefix(VehicleManager __instance)
        {
            foreach(var entity in __instance.vehiclesActive)
            {
                var manager = entity.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
                manager?.Cleanup();
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.Update3rdPerson))]
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
        private static void Postfix_CreateParts(Vehicle __instance)
        {
            if (__instance.FindPart(VPWeaponManager.VehicleWeaponManagerName) is VPWeaponManager manager)
                manager.ApplyModEffect(__instance.itemValue.Clone());
        }

        [HarmonyPatch(nameof(Vehicle.SetItemValue))]
        [HarmonyPostfix]
        private static void Postfix_SetItemValue(Vehicle __instance)
        {
            if (__instance.FindPart(VPWeaponManager.VehicleWeaponManagerName) is VPWeaponManager manager)
                manager.ApplyModEffect(__instance.itemValue.Clone());
        }

        [HarmonyPatch(nameof(Vehicle.SetItemValueMods))]
        [HarmonyPostfix]
        private static void Postfix_SetItemValueMods(Vehicle __instance)
        {
            if (__instance.FindPart(VPWeaponManager.VehicleWeaponManagerName) is VPWeaponManager manager)
                manager.ApplyModEffect(__instance.itemValue.Clone());
        }

        [HarmonyPatch(nameof(Vehicle.GetIKTargets))]
        [HarmonyPostfix]
        private static void Postfix_GetIKTargets(int slot, ref List<IKController.Target> __result, Vehicle __instance)
        {
            if (slot == 0)
                return;

            foreach (var part in __instance.GetParts())
            {
                if (part is VehicleWeaponRotatorBase rotator && rotator.ikTargets != null)
                {
                    if (__result == null)
                        __result = new List<IKController.Target>();
                    __result.AddRange(rotator.ikTargets);
                    Log.Out("adding ik from rotator!");
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
    public class PlayerControllerPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo mtd_horn = AccessTools.Method(typeof(EntityVehicle), nameof(EntityVehicle.UseHorn));
            MethodInfo mtd_xui = AccessTools.Method(typeof(XUiC_Radial), nameof(XUiC_Radial.SetActivatableItemData));
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if(codes[i].Calls(mtd_horn))
                {
                    for (int j = i - 1; j > 0; j--)
                    {
                        if (codes[j].Calls(mtd_xui))
                        {
                            codes.InsertRange(j + 1, new CodeInstruction[]
                            {
                                new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[j + 1].ExtractLabels()),
                                CodeInstruction.LoadField(typeof(PlayerMoveController), nameof(PlayerMoveController.entityPlayerLocal)),
                                CodeInstruction.Call(typeof(PlayerControllerPatch), nameof(CheckForSwitchingSeat))
                            });
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
            int seat = PlayerActionsVehicleExtra.Instance.ActivateSlotWasPressed;
            if(PlayerActionsVehicleExtra.Instance.HoldSwitchSeat.IsPressed && seat >= 0)
                Log.Out($"trying to switch seat to {PlayerActionsVehicleExtra.Instance.ActivateSlotWasPressed}");

            if(PlayerActionsVehicleExtra.Instance.HoldSwitchSeat.IsPressed && seat >= 0 && seat < player.AttachedToEntity.GetAttachMaxCount() && seat != player.AttachedToEntity.FindAttachSlot(player))
            {
                if (ConnectionManager.Instance.IsServer)
                    GameManager.Instance.TrySwitchSeatServer(GameManager.Instance.World, player.entityId, player.AttachedToEntity.entityId, seat);
                else
                    ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageVehicleSwitchSeat>().Setup(player.entityId, player.AttachedToEntity.entityId, seat));
            }
        }
    }

    [HarmonyPatch(typeof(EModelBase), nameof(EModelBase.RemoveIKController))]
    public class IKPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var mtd_destroy = AccessTools.Method(typeof(UnityEngine.Object), nameof(UnityEngine.Object.Destroy), new Type[] {typeof(UnityEngine.Object) });
            var codes = instructions.ToList();
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_destroy))
                {
                    codes.RemoveAt(i);
                    codes.Insert(i, CodeInstruction.Call(typeof(UnityEngine.Object), nameof(UnityEngine.Object.DestroyImmediate), new Type[] {typeof(UnityEngine.Object)}));
                    break;
                }
            }
            return codes;
        }
    }

    [HarmonyPatch(typeof(NGuiWdwInGameHUD), nameof(NGuiWdwInGameHUD.OnGUI))]
    public class GUIPatch
    {
        private static void Postfix()
        {
            if (VPWeaponManager.CurrentInstance != null)
            {
                VPWeaponManager.CurrentInstance.GUIUpdate();
            }
        }
    }

    [HarmonyPatch(typeof(MinEffectController), nameof(MinEffectController.ParseXml))]
    public class VehicleModParsePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_parse = AccessTools.Method(typeof(MinEffectGroup), nameof(MinEffectGroup.ParseXml));

            for(int i = 0;i < codes.Count;i++)
            {
                if (codes[i].Calls(mtd_parse))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(codes[i - 1].opcode, codes[i - 1].operand),
                        new CodeInstruction(OpCodes.Ldarg_2),
                        new CodeInstruction(OpCodes.Ldarg_3),
                        CodeInstruction.Call(typeof(VehicleModParsePatch), nameof(ParseVehicleWeaponModEffect))
                    });
                    i += 5;
                }
            }

            return codes;
        }

        private static void ParseVehicleWeaponModEffect(MinEffectGroup _group, XElement _element, MinEffectController.SourceParentType _type, object _parentPointer)
        {
            if (_type == MinEffectController.SourceParentType.ItemModifierClass && _parentPointer is int itemId)
            {
                if (_element.HasAttribute("vehicle_weapon"))
                {
                    if (!VPWeaponManager.VehicleWeaponModMapping.TryGetValue(itemId, out var list))
                    {
                        list = new List<(string vmodName, MinEffectGroup group)>();
                        VPWeaponManager.VehicleWeaponModMapping[itemId] = list;
                    }
                    list.Add((_element.GetAttribute("vehicle_weapon"), _group));
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveAndCleanupWorld))]
    public class VehicleModCleanupPatch
    {
        private static void Postfix()
        {
            VPWeaponManager.VehicleWeaponModMapping.Clear();
        }
    }

    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.CreateParts))]
    public class VehiclePartsPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_getstring = AccessTools.Method(typeof(DynamicProperties), nameof(DynamicProperties.GetString), new[] { typeof(string) });

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_getstring))
                {
                    codes.InsertRange(i + 6, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloca_S, 2),
                        CodeInstruction.Call(typeof(KeyValuePair<string, DynamicProperties>), "get_Key"),
                        new CodeInstruction(OpCodes.Ldstr, "."),
                        CodeInstruction.Call(typeof(string), nameof(string.Contains), new[]{typeof(string)}),
                        new CodeInstruction(OpCodes.Brtrue_S, codes[i + 5].operand)
                    });
                    break;
                }
            }
            return codes;
        }
    }
    //[HarmonyPatch(typeof(IKController), nameof(IKController.OnAnimatorIK))]
    //public class IKDebugPatch
    //{
    //    private static void Postfix()
    //    {
    //        Log.Out("IK!");
    //    }
    //}
}
