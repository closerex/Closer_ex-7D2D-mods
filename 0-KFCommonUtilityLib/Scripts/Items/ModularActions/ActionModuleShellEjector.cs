using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(ShellEjectorData))]
public class ActionModuleShellEjector
{
    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemActionRanged __instance, ItemActionData _data, ShellEjectorData __customData)
    {
        var rangedData = _data as ItemActionRanged.ItemActionDataRanged;
        string indexExtension = (_data.indexInEntityOfAction > 0 ? _data.indexInEntityOfAction.ToString() : "");
        string jointName = _data.invData.itemValue.GetPropertyOverrideForAction($"ShellJoint_Name", $"ShellJoint{indexExtension}", _data.indexInEntityOfAction);
        __customData.shellJoint = AnimationRiggingManager.GetTransformOverrideByName(rangedData.invData.model, jointName);

        jointName = _data.invData.itemValue.GetPropertyOverrideForAction($"ShellEffectJoint_Name", $"ShellEffectJoint{indexExtension}", _data.indexInEntityOfAction);
        __customData.shellEffectJoint = AnimationRiggingManager.GetTransformOverrideByName(rangedData.invData.model, jointName);

        __customData.shellPrefabDefault = "";
        __instance.Properties.ParseString("Particles_shell", ref __customData.shellPrefabDefault);
        LoadPEAsset(__customData.shellPrefabDefault);
        __customData.shellPrefabDefaultFpv = "";
        __instance.Properties.ParseString("Particles_shell_Fpv", ref __customData.shellPrefabDefaultFpv);
        LoadPEAsset(__customData.shellPrefabDefaultFpv);

        __customData.ammoShellPrefabs = new string[__instance.MagazineItemNames.Length];
        __customData.ammoShellPrefabsFpv = new string[__instance.MagazineItemNames.Length];
        for (int i = 0; i < __instance.MagazineItemNames.Length; i++)
        {
            var ammoItem = ItemClass.GetItemClass(__instance.MagazineItemNames[i]);
            if (ammoItem != null)
            {
                ammoItem.Properties.ParseString("Particle_shell", ref __customData.ammoShellPrefabs[i]);
                LoadPEAsset(__customData.ammoShellPrefabs[i]);
                ammoItem.Properties.ParseString("Particle_shell_Fpv", ref __customData.ammoShellPrefabsFpv[i]);
                LoadPEAsset(__customData.ammoShellPrefabsFpv[i]);
            }
        }

        __customData.effectPrefabDefault = "";
        __instance.Properties.ParseString("Particles_shell_effect", ref __customData.effectPrefabDefault);
        LoadPEAsset(__customData.effectPrefabDefault);
        __customData.effectPrefabDefaultFpv = "";
        __instance.Properties.ParseString("Particles_shell_effect_Fpv", ref __customData.effectPrefabDefaultFpv);
        LoadPEAsset(__customData.effectPrefabDefaultFpv);
        __customData.ammoEffectPrefabs = new string[__instance.MagazineItemNames.Length];
        __customData.ammoEffectPrefabsFpv = new string[__instance.MagazineItemNames.Length];
        for (int i = 0; i < __instance.MagazineItemNames.Length; i++)
        {
            var ammoItem = ItemClass.GetItemClass(__instance.MagazineItemNames[i]);
            if (ammoItem != null)
            {
                ammoItem.Properties.ParseString("Particle_shell_effect", ref __customData.ammoEffectPrefabs[i]);
                LoadPEAsset(__customData.ammoEffectPrefabs[i]);
                ammoItem.Properties.ParseString("Particle_shell_effect_Fpv", ref __customData.ammoEffectPrefabsFpv[i]);
                LoadPEAsset(__customData.ammoEffectPrefabsFpv[i]);
            }
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects)), MethodTargetTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_ItemActionEffects(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 7)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_2).WithLabels(codes[i + 1].ExtractLabels()),
                    new CodeInstruction(OpCodes.Ldloc_1),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldloc_S, codes[i].operand),
                    CodeInstruction.Call(typeof(ActionModuleShellEjector), nameof(SpawnShellParticle)),
                });
                break;
            }
        }

        return codes;
    }

    private static void SpawnShellParticle(ItemActionData data, EntityAlive entity, EntityPlayerLocal player, bool isLocalFpv)
    {
        var itemActionDataRanged = data as ItemActionRanged.ItemActionDataRanged;
        var customData = (data as IModuleContainerFor<ActionModuleShellEjector.ShellEjectorData>)?.Instance;
        if (itemActionDataRanged != null && customData != null)
        {
            int ammoIndex = data.invData.itemValue.SelectedAmmoTypeIndex;
            string shellPrefab = null, effectPrefab = null;
            if (isLocalFpv)
            {
                if (customData.shellJoint != null)
                {
                    shellPrefab = customData.ammoShellPrefabsFpv[ammoIndex];
                    if (string.IsNullOrEmpty(shellPrefab))
                    {
                        shellPrefab = customData.ammoShellPrefabs[ammoIndex];
                        if (string.IsNullOrEmpty(shellPrefab))
                        {
                            shellPrefab = customData.shellPrefabDefaultFpv;
                            if (string.IsNullOrEmpty(shellPrefab))
                            {
                                shellPrefab = customData.shellPrefabDefault;
                            }
                        }
                    }
                }

                if (customData.shellEffectJoint != null)
                {
                    effectPrefab = customData.ammoEffectPrefabsFpv[ammoIndex];
                    if (string.IsNullOrEmpty(effectPrefab))
                    {
                        effectPrefab = customData.ammoEffectPrefabs[ammoIndex];
                        if (string.IsNullOrEmpty(effectPrefab))
                        {
                            effectPrefab = customData.effectPrefabDefaultFpv;
                            if (string.IsNullOrEmpty(effectPrefab))
                            {
                                effectPrefab = customData.effectPrefabDefault;
                            }
                        }
                    }
                }
            }
            else
            {
                if (customData.shellJoint != null)
                {
                    shellPrefab = customData.ammoShellPrefabs[ammoIndex];
                    if (string.IsNullOrEmpty(shellPrefab))
                    {
                        shellPrefab = customData.shellPrefabDefault;
                    }
                }

                if (customData.shellEffectJoint != null)
                {
                    effectPrefab = customData.ammoEffectPrefabs[ammoIndex];
                    if (string.IsNullOrEmpty(effectPrefab))
                    {
                        effectPrefab = customData.effectPrefabDefault;
                    }
                }
            }
            if (!string.IsNullOrEmpty(shellPrefab))
            {
                float light = GameManager.Instance.World.GetLightBrightness(World.worldToBlockPos(customData.shellJoint.transform.position)) / 2f;
                Transform shell = GameManager.Instance.SpawnParticleEffectClientForceCreation(new ParticleEffect(shellPrefab, Vector3.zero, light, Color.clear, null, null, false), entity.entityId, true);
                AnimationRiggingManager.ProcessMuzzleFlashParticle(shell, customData.shellJoint);
            }
            if (!string.IsNullOrEmpty(effectPrefab))
            {
                Transform effect = GameManager.Instance.SpawnParticleEffectClientForceCreation(new ParticleEffect(effectPrefab, Vector3.zero, 1, Color.clear, null, null, false), entity.entityId, true);
                AnimationRiggingManager.ProcessMuzzleFlashParticle(effect, customData.shellEffectJoint);
            }
        }
    }

    private static void LoadPEAsset(string pe)
    {
        if (!string.IsNullOrEmpty(pe) && !ParticleEffect.IsAvailable(pe))
        {
            ParticleEffect.LoadAsset(pe);
        }
    }

    public class ShellEjectorData
    {
        public Transform shellJoint;
        public string shellPrefabDefault, shellPrefabDefaultFpv;
        public string[] ammoShellPrefabs, ammoShellPrefabsFpv;

        public Transform shellEffectJoint;
        public string effectPrefabDefault, effectPrefabDefaultFpv;
        public string[] ammoEffectPrefabs, ammoEffectPrefabsFpv;

        public ShellEjectorData(ItemActionData actionData, ItemInventoryData _invData, int _indexInEntityOfAction, ActionModuleShellEjector _module)
        {

        }
    }
}
