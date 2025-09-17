using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
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

        string str = "false";
        __instance.Properties.ParseString($"ManualEject", ref str);
        __customData.manualEject = bool.Parse(_data.invData.itemValue.GetPropertyOverrideForAction($"ManualEject", str, _data.indexInEntityOfAction));

        __customData.shellPrefabDefault = "";
        __instance.Properties.ParseString("Particles_shell", ref __customData.shellPrefabDefault);
        LoadPEAsset(__customData.shellPrefabDefault);
        __customData.shellPrefabDefaultFpv = "";
        __instance.Properties.ParseString("Particles_shell_Fpv", ref __customData.shellPrefabDefaultFpv);
        LoadPEAsset(__customData.shellPrefabDefaultFpv);

        var ammoItems = Array.ConvertAll(__instance.MagazineItemNames, s => ItemClass.GetItemClass(s, true));
        __customData.ammoShellPrefabs = new string[__instance.MagazineItemNames.Length];
        __customData.ammoShellPrefabsFpv = new string[__instance.MagazineItemNames.Length];
        for (int i = 0; i < __instance.MagazineItemNames.Length; i++)
        {
            var ammoItem = ammoItems[i];
            if (ammoItem != null)
            {
                ammoItem.Properties.ParseString("Particles_shell", ref __customData.ammoShellPrefabs[i]);
                LoadPEAsset(__customData.ammoShellPrefabs[i]);
                ammoItem.Properties.ParseString("Particles_shell_Fpv", ref __customData.ammoShellPrefabsFpv[i]);
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
            var ammoItem = ammoItems[i];
            if (ammoItem != null)
            {
                ammoItem.Properties.ParseString("Particles_shell_effect", ref __customData.ammoEffectPrefabs[i]);
                LoadPEAsset(__customData.ammoEffectPrefabs[i]);
                ammoItem.Properties.ParseString("Particles_shell_effect_Fpv", ref __customData.ammoEffectPrefabsFpv[i]);
                LoadPEAsset(__customData.ammoEffectPrefabsFpv[i]);
            }
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects)), MethodTargetTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_ItemActionEffects(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        int fpvLocalIndex = GameManager.IsDedicatedServer && Application.platform == RuntimePlatform.LinuxServer ? 5 : 7;

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == fpvLocalIndex)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_2).WithLabels(codes[i + 1].ExtractLabels()),
                    CodeInstruction.Call(typeof(ActionModuleShellEjector), nameof(SpawnShellParticle)),
                });
                break;
            }
        }

        return codes;
    }

    private static void SpawnShellParticle(ItemActionData data)
    {
        var customData = (data as IModuleContainerFor<ActionModuleShellEjector.ShellEjectorData>)?.Instance;
        if (customData != null && !customData.manualEject)
        {
            customData.SpawnBoth();
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

        public bool manualEject;

        public ItemActionData actionData;
        public ActionModuleShellEjector module;

        public ShellEjectorData(ItemActionData __instance, ActionModuleShellEjector __customModule)
        {
            this.actionData = __instance;
            module = __customModule;
        }

        public void SpawnParticle()
        {
        }

        public void SpawnShell()
        {
            var player = actionData.invData.holdingEntity as EntityPlayerLocal;
            bool isLocalFpv = player != null && player.bFirstPersonView;
            int ammoIndex = actionData.invData.itemValue.SelectedAmmoTypeIndex;
            SpawnShell(ammoIndex, isLocalFpv);
        }

        public void SpawnEffect()
        {
            var player = actionData.invData.holdingEntity as EntityPlayerLocal;
            bool isLocalFpv = player != null && player.bFirstPersonView;
            int ammoIndex = actionData.invData.itemValue.SelectedAmmoTypeIndex;
            SpawnEffect(ammoIndex, isLocalFpv);
        }

        public void SpawnBoth()
        {
            var player = actionData.invData.holdingEntity as EntityPlayerLocal;
            bool isLocalFpv = player != null && player.bFirstPersonView;
            int ammoIndex = actionData.invData.itemValue.SelectedAmmoTypeIndex;
            SpawnShell(ammoIndex, isLocalFpv);
            SpawnEffect(ammoIndex, isLocalFpv);
        }

        public void SpawnShell(int ammoIndex, bool isLocalFpv)
        {
            string shellPrefab = null;
            if (isLocalFpv)
            {
                if (shellJoint != null)
                {
                    shellPrefab = ammoShellPrefabsFpv[ammoIndex];
                    if (string.IsNullOrEmpty(shellPrefab))
                    {
                        shellPrefab = ammoShellPrefabs[ammoIndex];
                        if (string.IsNullOrEmpty(shellPrefab))
                        {
                            shellPrefab = shellPrefabDefaultFpv;
                            if (string.IsNullOrEmpty(shellPrefab))
                            {
                                shellPrefab = shellPrefabDefault;
                            }
                        }
                    }
                }
            }
            else
            {
                if (shellJoint != null)
                {
                    shellPrefab = ammoShellPrefabs[ammoIndex];
                    if (string.IsNullOrEmpty(shellPrefab))
                    {
                        shellPrefab = shellPrefabDefault;
                    }
                }
            }
            if (!string.IsNullOrEmpty(shellPrefab))
            {
                float light = GameManager.Instance.World.GetLightBrightness(World.worldToBlockPos(shellJoint.transform.position)) / 2f;
                Transform shell = GameManager.Instance.SpawnParticleEffectClientForceCreation(new ParticleEffect(shellPrefab, Vector3.zero, light, Color.clear, null, null, false), actionData.invData.holdingEntity.entityId, true);
                AnimationRiggingManager.ProcessMuzzleFlashParticle(shell, shellJoint);
            }
        }

        public void SpawnEffect(int ammoIndex, bool isLocalFpv)
        {
            string effectPrefab = null;
            if (isLocalFpv)
            {
                if (shellEffectJoint != null)
                {
                    effectPrefab = ammoEffectPrefabsFpv[ammoIndex];
                    if (string.IsNullOrEmpty(effectPrefab))
                    {
                        effectPrefab = ammoEffectPrefabs[ammoIndex];
                        if (string.IsNullOrEmpty(effectPrefab))
                        {
                            effectPrefab = effectPrefabDefaultFpv;
                            if (string.IsNullOrEmpty(effectPrefab))
                            {
                                effectPrefab = effectPrefabDefault;
                            }
                        }
                    }
                }
            }
            else
            {
                if (shellEffectJoint != null)
                {
                    effectPrefab = ammoEffectPrefabs[ammoIndex];
                    if (string.IsNullOrEmpty(effectPrefab))
                    {
                        effectPrefab = effectPrefabDefault;
                    }
                }
            }
            if (!string.IsNullOrEmpty(effectPrefab))
            {
                Transform effect = GameManager.Instance.SpawnParticleEffectClientForceCreation(new ParticleEffect(effectPrefab, Vector3.zero, 1, Color.clear, null, null, false), actionData.invData.holdingEntity.entityId, true);
                AnimationRiggingManager.ProcessMuzzleFlashParticle(effect, shellEffectJoint);
            }
        }
    }
}
