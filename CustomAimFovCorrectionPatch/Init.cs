using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CustomFPVFov;
using HarmonyLib;
using KFCommonUtilityLib;
using UniLinq;
using UnityEngine;

namespace CustomAimFovCorrectionPatch
{
    public class Init : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (inited)
            {
                return;
            }

            inited = true;
            FovOverrides.modPath = _modInstance.Path;
            Log.Out(" Loading Patch: " + GetType());
            var harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch]
    public static class AimFovCorrectionPatch
    {
        [HarmonyPatch(typeof(ActionModuleProceduralAiming.ProceduralAimingData), nameof(ActionModuleProceduralAiming.ProceduralAimingData.UpdateCurrentReference))]
        [HarmonyPostfix]
        private static void Postfix_ProceduralAimingData_UpdateCurrentReference(EntityPlayerLocal ___holdingEntity)
        {
            FovOverrides.UpdatePlayerFov(___holdingEntity);
        }

        [HarmonyPatch(typeof(AimRefData), nameof(AimRefData.UpdateAimFovOverride))]
        [HarmonyPostfix]
        private static void Postfix_ProceduralAimingData_UpdateAimFovOverride(AimRefData __instance)
        {
            AimReference curAimRef = __instance.aimRef;
            if (AimingSettings.HasFlag(AimCorrectionMode.FovByDistance) && curAimRef.applyAimFovCorrection)
            {
                if (curAimRef.asReference)
                {
                    //calculate fov override based on curAimRef.designedAimDistance and modified reference distance
                    if (curAimRef.designedAimDistance > 0)
                    {
                        __instance.targetAimFov = AimFovCorrection(curAimRef.designedAimDistance + __instance.targetAimRefOffset, curAimRef.designedAimDistance, curAimRef.designedAimFov);
                    }
                }
                else if (curAimRef.scopeBase)
                {
                    AimReference defaultReference = curAimRef.scopeBase.defaultReference;
                    if (defaultReference && defaultReference.designedAimFov > 0 && curAimRef.designedAimFov > 0 && curAimRef.designedAimDistance > 0 && (defaultReference.designedAimFov != curAimRef.designedAimFov || defaultReference.designedAimDistance != curAimRef.designedAimDistance))
                    {
                        __instance.targetAimRefOffset = defaultReference.designedAimDistance - curAimRef.designedAimDistance;
                        __instance.targetPosOffset -= __instance.targetAimRefOffset * (__instance.targetRotOffset * Vector3.forward).normalized;
                        __instance.targetAimFov = AimFovCorrection(defaultReference.designedAimDistance, curAimRef.designedAimDistance, curAimRef.designedAimFov);
                    }
                }
            }
        }

        public static float AimFovCorrection(float weaponDistance, float scopeDistance, float scopeFov)
        {
            if (Mathf.Approximately(weaponDistance, scopeDistance))
            {
                return scopeFov;
            }

            float tanHalfFs = Mathf.Tan(scopeFov * 0.5f * Mathf.Deg2Rad);
            float tanHalfFPrime = (scopeDistance / weaponDistance) * tanHalfFs;

            return 2f * Mathf.Atan(tanHalfFPrime) * Mathf.Rad2Deg;
        }

        [HarmonyPatch(typeof(FovOverrides), nameof(FovOverrides.UpdatePlayerFov))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_FovOverrides_UpdatePlayerFov(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var prop_curaimfov = AccessTools.PropertySetter(typeof(FovOverrides), nameof(FovOverrides.CurrentAimFov));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(prop_curaimfov))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.CallClosure<Action<EntityPlayerLocal>>(static (player) =>
                        {
                            if (player.inventory?.holdingItemData?.actionData?[1] is IModuleContainerFor<ActionModuleProceduralAiming.ProceduralAimingData> dataModule)
                            {
                                var data = dataModule.Instance;
                                MatrixBlender.curAimData = data;
                            }
                            else
                            {
                                MatrixBlender.curAimData = null;
                            }
                        })
                    });
                    i += 3;
                }
            }
            return codes;
        }

        private static HashSet<Renderer> scalerRenderers = new();
        [HarmonyPatch(typeof(CameraMatrixOverride), nameof(CameraMatrixOverride.UpdateRendererList))]
        [HarmonyPostfix]
        private static void Postfix_CameraMatrixOverride_UpdateRendererList(CameraMatrixOverride __instance)
        {
            scalerRenderers.Clear();
            var tempScalers = CustomFPVFov.Patches.tempScalers;
            if (tempScalers.Count > 0)
            {
                foreach (var scaler in tempScalers.SelectMany(t => t.transform.GetComponentsInChildren<Renderer>()))
                {
                    scalerRenderers.Add(scaler);
                }
            }
        }

        [HarmonyPatch(typeof(CameraMatrixOverride), nameof(CameraMatrixOverride.OnPreRender))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_CameraMatrixOverride_OnPreRender(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var mtd_persp = AccessTools.Method(typeof(Matrix4x4), nameof(Matrix4x4.Perspective));
            var lbd_newMatrix = generator.DeclareLocal(typeof(Matrix4x4));
            var lbd_prevMatrix = generator.DeclareLocal(typeof(Matrix4x4));
            var lbd_useOrthoMatrix = generator.DeclareLocal(typeof(bool));
            var lbd_orthoMatrixCalcRequired = generator.DeclareLocal(typeof(bool));
            var lbl_calc = generator.DefineLabel();
            var lbl_load_prev_matrix = generator.DefineLabel();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_persp))
                {
                    codes[i + 2].WithLabels(lbl_calc);
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(CameraMatrixOverride), nameof(CameraMatrixOverride.advancedSettings)),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(CameraMatrixOverride), nameof(CameraMatrixOverride.referenceCamera)),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(CameraMatrixOverride), nameof(CameraMatrixOverride.fov)),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(CameraMatrixOverride), nameof(CameraMatrixOverride.nearClipFactor)),
                        new CodeInstruction(OpCodes.Ldloca_S, lbd_newMatrix),
                        CodeInstruction.Call(typeof(MatrixBlender), nameof(MatrixBlender.BuildOrthoMatrix)),
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_useOrthoMatrix),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_orthoMatrixCalcRequired),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_newMatrix), //calc mixed matrix first
                        new CodeInstruction(OpCodes.Stloc_1),
                        new CodeInstruction(OpCodes.Br_S, lbl_calc),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_prevMatrix).WithLabels(lbl_load_prev_matrix),
                        new CodeInstruction(OpCodes.Stloc_1),
                    });
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_newMatrix),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_prevMatrix)
                    });
                    //Log.Out("1");
                    i += 24;
                }
                else if (codes[i].opcode == OpCodes.Stloc_S && codes[i].operand is LocalBuilder { LocalIndex: 4 })
                {
                    var lbl = generator.DefineLabel();
                    codes[i + 1].WithLabels(lbl);
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_orthoMatrixCalcRequired),
                        new CodeInstruction(OpCodes.Brfalse_S, lbl),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_orthoMatrixCalcRequired),
                        new CodeInstruction(OpCodes.Ldloc_S, 4),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_newMatrix),
                        new CodeInstruction(OpCodes.Br_S, lbl_load_prev_matrix)
                    });
                    //Log.Out("2");
                    i += 8;
                }
                else if (codes[i].opcode == OpCodes.Ldloc_S && codes[i].operand is LocalBuilder { LocalIndex: 4 })
                {
                    var lbl_load_prev = generator.DefineLabel();
                    var lbl_loaded = generator.DefineLabel();
                    codes[i].WithLabels(lbl_load_prev);
                    codes[i + 1].WithLabels(lbl_loaded);
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_useOrthoMatrix),
                        new CodeInstruction(OpCodes.Brfalse_S, lbl_load_prev),
                        CodeInstruction.LoadField(typeof(AimFovCorrectionPatch), nameof(scalerRenderers)),
                        CodeInstruction.LoadLocal(10),
                        CodeInstruction.Call(typeof(HashSet<Renderer>), nameof(HashSet<Renderer>.Contains)),
                        new CodeInstruction(OpCodes.Brtrue_S, lbl_load_prev),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_newMatrix),
                        new CodeInstruction(OpCodes.Br_S, lbl_loaded)
                    });
                    //Log.Out("3");
                    i += 9;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(vp_FPWeapon), nameof(vp_FPWeapon.UpdateZoom))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_vp_FPWeapon_UpdateZoom(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_smoothstep = AccessTools.Method(typeof(Mathf), nameof(Mathf.SmoothStep));
            var fld_fov = AccessTools.Field(typeof(vp_FPWeapon), nameof(vp_FPWeapon.RenderingFieldOfView));
            var fld_curfov = AccessTools.Field(typeof(CameraMatrixOverride), nameof(CameraMatrixOverride.fov));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_smoothstep))
                {
                    codes.Insert(i, CodeInstruction.CallClosure<Func<float, float>>(static (input) =>
                    {
                        if (MatrixBlender.curAimData != null)
                        {
                            return MatrixBlender.curAimData.CurAimProcValue;
                        }
                        return input;
                    }));
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (codes[j].LoadsField(fld_fov))
                        {
                            codes.Insert(j + 1, CodeInstruction.CallClosure<Func<float, float>>(static (input) =>
                            {
                                if (MatrixBlender.curAimData != null && AimingSettings.HasFlag(AimCorrectionMode.FovByDistance))
                                {
                                    return MatrixBlender.curAimData.CurTargetAimFovValue;
                                }
                                return input;
                            }));
                        }
                        else if (codes[j].LoadsField(fld_curfov))
                        {
                            codes.Insert(j + 1, CodeInstruction.CallClosure<Func<float, float>>(static (input) =>
                            {
                                if (MatrixBlender.curAimData != null)
                                {
                                    return FovOverrides.CurrentFov;
                                }
                                return input;
                            }));
                        }
                    }
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(vp_FPWeapon), nameof(vp_FPWeapon.Refresh))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_vp_FPWeapon_Refresh(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_smoothstep = AccessTools.Method(typeof(vp_FPWeapon), nameof(vp_FPWeapon.SmoothStep));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_smoothstep))
                {
                    codes.Insert(i, CodeInstruction.CallClosure<Func<float, float>>(static (input) =>
                    {
                        if (MatrixBlender.curAimData != null)
                        {
                            return MatrixBlender.curAimData.CurAimProcValue;
                        }
                        return input;
                    }));
                    i++;
                }
            }

            return codes;
        }
    }

    public static class MatrixBlender
    {
        public static ActionModuleProceduralAiming.ProceduralAimingData curAimData;

        internal static bool BuildOrthoMatrix(CameraMatrixOverride.AdvancedSettings advancedSettings, Camera referenceCamera, float fov, float nearClipFactor, ref Matrix4x4 mixed)
        {
            if (curAimData != null && AimingSettings.HasFlag(AimCorrectionMode.MixedOrtho))
            {
                float t = Mathf.SmoothStep(0, 1, curAimData.CurAimProcValue);
                float w = t * curAimData.CurAimFlattenFactor;
                float focusDistance = curAimData.CurFocusDistance;
                if (w > 0 && focusDistance > 0f)
                {
                    float halfHeight = focusDistance * Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
                    float halfWidth = halfHeight * referenceCamera.aspect;
                    Matrix4x4 ortho = Matrix4x4.Ortho(-halfWidth, halfWidth, -halfHeight, halfHeight, referenceCamera.nearClipPlane * nearClipFactor, referenceCamera.farClipPlane * advancedSettings.farClipFactor);

                    mixed = MatrixLerp(mixed, ortho, w);
                    return true;
                }
            }
            return false;
        }

        public static Matrix4x4 MatrixLerp(Matrix4x4 from, Matrix4x4 to, float time)
        {
            Matrix4x4 ret = new Matrix4x4();
            for (int i = 0; i < 16; i++)
                ret[i] = Mathf.Lerp(from[i], to[i], time);
            return ret;
        }
    }
}
