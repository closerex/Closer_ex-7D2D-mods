using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

namespace CustomMuzzleFlash
{
    [HarmonyPatch]
    class MuzzlePatch
    {
        [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.ReadFrom))]
        [HarmonyPostfix]
        private static void Postfix_ReadFrom_ItemActionAttack(DynamicProperties _props)
        {
            LoadPEAsset(_props, "Particles_muzzle_fire");
            LoadPEAsset(_props, "Particles_muzzle_fire_fpv");
            LoadPEAsset(_props, "Particles_muzzle_smoke");
            LoadPEAsset(_props, "Particles_muzzle_smoke_fpv");
        }

        [HarmonyPatch(typeof(AutoTurretFireController), nameof(AutoTurretFireController.Init))]
        [HarmonyPostfix]
        private static void Postfix_Init_AutoTurretFireController(DynamicProperties _properties)
        {
            LoadPEAsset(_properties, "ParticlesMuzzleFire");
            LoadPEAsset(_properties, "ParticlesMuzzleSmoke");
	    }

        private static void LoadPEAsset(DynamicProperties _props, string _key)
        {
            if (_props.Values.TryGetValue(_key, out string val) && !string.IsNullOrEmpty(val) && !ParticleEffect.IsAvailable(val))
            {
                ParticleEffect.LoadAsset(val);
            }
        }

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ItemActionEffects_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            int smokeLocalIndex = GameManager.IsDedicatedServer && Application.platform == RuntimePlatform.LinuxServer ? 10 : 12;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == smokeLocalIndex)
                {
                    for (int j = i + 1; j < codes.Count - 2; j++)
                    {
                        if (codes[j].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[j].operand).LocalIndex == smokeLocalIndex && codes[j + 2].Branches(out var lbl))
                        {
                            codes.InsertRange(j + 3, new[]
                            {
                                new CodeInstruction(OpCodes.Ldloc_0).WithLabels(codes[j + 3].ExtractLabels()),
                                new CodeInstruction(OpCodes.Ldloc_S, codes[j].operand),
                                CodeInstruction.Call(typeof(MuzzlePatch), nameof(SetSmokeParticleParent))
                            });
                            break;
                        }
                    }
                    break;
                }
            }

            return codes;
        }

        private static void SetSmokeParticleParent(ItemActionRanged.ItemActionDataRanged _data, Transform smoke)
        {
            if (!smoke)
            {
                return;
            }
            if (_data.IsDoubleBarrel && _data.invData.itemValue.Meta == 0)
            {
                smoke.SetParent(_data.muzzle2, false);
            }
            else
            {
                smoke.SetParent(_data.muzzle, false);
            }
            smoke.localPosition = Vector3.zero;
            smoke.localRotation = Quaternion.identity;
            if (_data.invData.holdingEntity is EntityPlayerLocal player && player.bFirstPersonView)
            {
                Utils.SetLayerRecursively(smoke.gameObject, 10);
            }
        }
    }
}
