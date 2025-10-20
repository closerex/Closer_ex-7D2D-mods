using HarmonyLib;
using PI.NGSS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class CameraAccessPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodInfo[]
            {
                AccessTools.Method(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.guiDrawCrosshair)),
                AccessTools.Method(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Update)),
                AccessTools.Method(typeof(ItemActionZoom), nameof(ItemActionZoom.ConsumeScrollWheel)),
                AccessTools.Method(typeof(ItemActionZoom), nameof(ItemActionZoom.OnHoldingUpdate)),
                AccessTools.Method(typeof(ItemActionZoom), nameof(ItemActionZoom.StopHolding)),
                AccessTools.Method(typeof(NGuiWdwDebugPanels), nameof(NGuiWdwDebugPanels.showDebugPanel_PlayerEffectInfo)),
                AccessTools.Method(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))
            };
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_get = AccessTools.Method(typeof(Component), nameof(Component.GetComponent), null, new[] { typeof(Camera) });
            var fld_camera = AccessTools.Field(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.playerCamera));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_get))
                {
                    codes.RemoveAt(i);
                    codes[i - 1].operand = fld_camera;
                    i--;
                }
            }

            return codes;
        }
    }

    [HarmonyPatch]
    public static class  vp_FPCameraPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodInfo[]
            {
                AccessTools.Method(typeof(vp_FPCamera), nameof(vp_FPCamera.Awake)),
                AccessTools.Method(typeof(vp_FPCamera), nameof(vp_FPCamera.RefreshZoom)),
                AccessTools.Method(typeof(vp_FPCamera), nameof(vp_FPCamera.SnapZoom)),
                AccessTools.Method(typeof(vp_FPCamera), nameof(vp_FPCamera.UpdateZoom)),
            };
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var mtd_get_by_component = AccessTools.Method(typeof(Component), nameof(Component.GetComponent), null, new[] { typeof(Camera) });
            var mtd_get_by_gameobject = AccessTools.Method(typeof(GameObject), nameof(GameObject.GetComponent), null, new[] { typeof(Camera) });
            for (int i = 0; i < codes.Count; i++)
            {
                bool called = false;
                bool byComponent = false;
                if (codes[i].Calls(mtd_get_by_component))
                {
                    called = true;
                    byComponent = true;
                }
                else if (codes[i].Calls(mtd_get_by_gameobject))
                {
                    called = true;
                }

                if (!called)
                {
                    continue;
                }

                int insert = byComponent ? i : i - 1;
                int removeCount = byComponent ? 1 : 2;
                codes.RemoveRange(insert, removeCount);
                codes.Insert(insert, CodeInstruction.CallClosure<Func<vp_FPCamera, Camera>>(static (vp) =>
                {
                    return vp.FPController.localPlayer?.playerCamera ?? vp.GetComponent<Camera>();
                }));
                i += 1 - removeCount;
            }
            return codes;
        }
    }

    [HarmonyPatch]
    public static class CameraPatches
    {
        [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.FixedUpdate))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_vp_FPCamera_FixedUpdate(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_has_ran = AccessTools.Field(typeof(vp_FPCamera), nameof(vp_FPCamera.hasLateUpdateRan));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_has_ran))
                {
                    codes[i + 3].ExtractLabels();
                    codes.RemoveRange(i - 1, 4);
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Awake))]
        [HarmonyPostfix]
        private static void Postfix_EntityPlayerLocal_Awake(EntityPlayerLocal __instance)
        {
            CameraLateUpdater.Init(__instance);
            CameraAnimationUpdater.Init();
        }

        [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController.SetInRightHand))]
        [HarmonyPostfix]
        private static void Postfix_AvatarLocalPlayerController_SetInRightHand(Transform _transform)
        {
            CameraLateUpdater.UpdateHoldingItem(_transform);
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.switchModelView))]
        [HarmonyPostfix]
        private static void Postfix_switchModelView_EntityPlayerLocal(EntityPlayerLocal __instance)
        {
            CameraLateUpdater.UpdateHoldingItem(__instance.inventory?.GetHoldingItemTransform());
        }

        [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.LateUpdate))]
        [HarmonyPostfix]
        private static void Postfix_vp_FPCamera_LateUpdate()
        {
            CameraLateUpdater.LateUpdate(Time.deltaTime);
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveAndCleanupWorld))]
        [HarmonyPostfix]
        private static void Postfix_SaveAndCleanupWorld_GameManager()
        {
            CameraLateUpdater.Cleanup();
        }
    }

    //[HarmonyPatch]
    public static class CameraClonePatches
    {
        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Awake))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Awake(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            codes.InsertRange(codes.Count - 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[codes.Count - 1].ExtractLabels()),
                CodeInstruction.CallClosure<Action<EntityPlayerLocal>>(static (player) =>
                {
                    Transform newCameraContainerTemp = new GameObject("NewCameraContainerTemp").transform;
                    newCameraContainerTemp.gameObject.SetActive(false);
                    newCameraContainerTemp.SetParent(player.cameraContainerTransform, false);

                    List<Transform> list_children = new List<Transform>();
                    for (int i = 0; i < player.cameraTransform.childCount; i++)
                    {
                        Transform child = player.cameraTransform.GetChild(i);
                        list_children.Add(child);
                        child.SetParent(null, true);
                    }
                    Transform newCameraTransform = GameObject.Instantiate(player.cameraTransform, newCameraContainerTemp);
                    foreach (Transform child in list_children)
                    {
                        child.SetParent(player.cameraTransform, true);
                    }

                    newCameraTransform.name = "NewCameraTransform";
                    Component.DestroyImmediate(newCameraTransform.GetComponent<vp_FPCamera>());
                    Component.DestroyImmediate(newCameraTransform.GetComponent<LightViewer>());
                    Component.DestroyImmediate(newCameraTransform.GetComponent<ShaderGlobalsHelper>());
                    Component.DestroyImmediate(newCameraTransform.GetComponent<CameraMatrixOverride>());
                    Component.DestroyImmediate(newCameraTransform.GetComponent<ScreenEffects>());

                    //delay till attaching LocalPlayer script
                    player.cameraTransform.GetComponent<Camera>().enabled = false;
                    player.cameraTransform.GetComponent<StreamingController>().enabled = false;
                    player.cameraTransform.GetComponent<FlareLayer>().enabled = false;
                    player.cameraTransform.GetComponent<AudioListener>().enabled = false;
                    player.cameraTransform.GetComponent<OnPostRenderDispatcher>().enabled = false;
                    player.cameraTransform.GetComponent<PostProcessLayer>().enabled = false;
                    player.cameraTransform.GetComponent<PostProcessVolume>().enabled = false;
                    player.cameraTransform.GetComponent<ShinyScreenSpaceRaytracedReflections.ShinySSRR>().enabled = false;
                    player.cameraTransform.GetComponent<NGSS_FrustumShadows_7DTD>().enabled = false;
                    player.cameraTransform.GetComponent<HorizonBasedAmbientOcclusion.HBAO>().enabled = false;

                    Camera newCamera = newCameraTransform.GetComponent<Camera>();
                    newCameraTransform.SetParent(player.cameraContainerTransform, true);
                    newCameraTransform.localPosition = player.cameraTransform.localPosition;
                    newCameraTransform.localRotation = player.cameraTransform.localRotation;
                    newCameraTransform.localScale = player.cameraTransform.localScale;
                    if (player.finalCamera != player.playerCamera)
                    {
                        player.finalCamera.transform.SetParent(newCameraTransform, true);
                    }
                    else
                    {
                        player.finalCamera = newCamera;
                    }
                    player.playerCamera = newCamera;
                    player.cameraTransform.GetComponent<CameraMatrixOverride>().referenceCamera = newCamera;
                    GameObject.DestroyImmediate(newCameraContainerTemp.gameObject);
                })
            });
            return codes;
        }

        [HarmonyPatch(typeof(EntityFactory), nameof(EntityFactory.CreateEntity), new[] { typeof(EntityCreationData) })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_CreateEntity(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_add = AccessTools.Method(typeof(GameObject), nameof(GameObject.AddComponent), null, new Type[] { typeof(LocalPlayer) });

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_add))
                {
                    codes[i + 1] = CodeInstruction.CallClosure<Action<LocalPlayer>>(static (localPlayer) =>
                    {
                        var player = localPlayer.entityPlayerLocal;
                        var newCameraTransform = player.transform.Find("NewCameraTransform");
                        if (newCameraTransform)
                        {
                            newCameraTransform.GetComponent<LocalPlayerCamera>().cameraType = LocalPlayerCamera.CameraType.Main;
                            var cameraUpdater = newCameraTransform.AddMissingComponent<LocalPlayerCameraUpdater>();
                            cameraUpdater.cameraMatrixOverride = player.cameraTransform.GetComponent<CameraMatrixOverride>();
                            cameraUpdater.UpdatePosition(player.vp_FPCamera);
                            cameraUpdater.UpdateRotation(player.vp_FPCamera);
                        }
                        player.cameraTransform.GetComponent<LocalPlayerCamera>().enabled = false;
                    });
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.FixedUpdate))]
        [HarmonyPrefix]
        private static void Prefix_vp_FPCamera_FixedUpdate(vp_FPCamera __instance)
        {
            LocalPlayerCameraUpdater.FindUpdater(__instance.Parent)?.RestoreRotation(__instance);
        }

        [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.FixedUpdate))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_vp_FPCamera_FixedUpdate(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var mtd_update = AccessTools.Method(typeof(vp_FPCamera), nameof(vp_FPCamera.UpdateSprings));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_update))
                {
                    var lbl_pop = generator.DefineLabel();
                    var lbl_next = generator.DefineLabel();
                    codes[i - 1].WithLabels(lbl_next);
                    codes.InsertRange(i - 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(vp_FPCamera), nameof(vp_FPCamera.Parent))),
                        CodeInstruction.Call(typeof(LocalPlayerCameraUpdater), nameof(LocalPlayerCameraUpdater.FindUpdater)),
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Brfalse_S, lbl_pop),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.Call(typeof(LocalPlayerCameraUpdater), nameof(LocalPlayerCameraUpdater.ApplyDiffToInput)),
                        new CodeInstruction(OpCodes.Br_S, lbl_next),
                        new CodeInstruction(OpCodes.Pop).WithLabels(lbl_pop)
                    });
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.FixedUpdate))]
        [HarmonyPostfix]
        private static void Postfix_vp_FPCamera_FixedUpdate(vp_FPCamera __instance)
        {
            LocalPlayerCameraUpdater.FindUpdater(__instance.Parent)?.SaveAndResetRotation(__instance);
        }

        [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.LateUpdate))]
        [HarmonyPrefix]
        private static void Prefix_vp_FPCamera_LateUpdate(vp_FPCamera __instance)
        {
            LocalPlayerCameraUpdater.FindUpdater(__instance.Parent)?.RestoreRotation(__instance);
        }

        [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.LateUpdate))]
        [HarmonyPostfix]
        private static void Postfix_vp_FPCamera_LateUpdate(vp_FPCamera __instance)
        {
            LocalPlayerCameraUpdater.FindUpdater(__instance.Parent)?.SaveAndResetRotation(__instance);
        }

        [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.UpdateInput))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_vp_FPCamera_UpdateInput(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            

            for (int i = codes.Count - 2; i >= 0; i--)
            {
                if (codes[i].opcode == OpCodes.Ret)
                {
                    var lbd_updater = generator.DeclareLocal(typeof(LocalPlayerCameraUpdater));
                    var lbd_mode = generator.DeclareLocal(typeof(bool));
                    var lbl_valid = generator.DefineLabel();
                    var lbl_vanilla = generator.DefineLabel();
                    var lbl_mode_input = generator.DefineLabel();
                    var code = codes[i + 1];
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[i + 1].ExtractLabels()),
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(vp_FPCamera), nameof(vp_FPCamera.Parent))),
                        CodeInstruction.Call(typeof(LocalPlayerCameraUpdater), nameof(LocalPlayerCameraUpdater.FindUpdater)),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_updater),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_updater),
                        new CodeInstruction(OpCodes.Brtrue_S, lbl_valid),
                        new CodeInstruction(OpCodes.Ret),
                        new CodeInstruction(OpCodes.Ldc_I4_1).WithLabels(lbl_valid),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_mode),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_updater),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.Call(typeof(LocalPlayerCameraUpdater), nameof(LocalPlayerCameraUpdater.ApplyFinal)),
                        new CodeInstruction(OpCodes.Br_S, lbl_vanilla),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_updater).WithLabels(lbl_mode_input),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.Call(typeof(LocalPlayerCameraUpdater), nameof(LocalPlayerCameraUpdater.ApplyInput)),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_mode),
                    });
                    code.WithLabels(lbl_vanilla);

                    var lbl_update_input = generator.DefineLabel();

                    codes.InsertRange(codes.Count - 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_mode),
                        new CodeInstruction(OpCodes.Brfalse_S, lbl_update_input),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_updater),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.Call(typeof(LocalPlayerCameraUpdater), nameof(LocalPlayerCameraUpdater.UpdateFinal)),
                        new CodeInstruction(OpCodes.Br_S, lbl_mode_input),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_updater).WithLabels(lbl_update_input),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.Call(typeof(LocalPlayerCameraUpdater), nameof(LocalPlayerCameraUpdater.UpdateInput))
                    });

                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.SetRotation), new[] {typeof(Vector2), typeof(bool)})]
        [HarmonyPostfix]
        private static void Postfix_vp_FPCamera_SetRotation1(vp_FPCamera __instance)
        {
            LocalPlayerCameraUpdater.FindUpdater(__instance.Parent)?.UpdateRotation(__instance);
        }

        [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.SetRotation), new[] {typeof(Vector2)})]
        [HarmonyPostfix]
        private static void Postfix_vp_FPCamera_SetRotation2(vp_FPCamera __instance)
        {
            LocalPlayerCameraUpdater.FindUpdater(__instance.Parent)?.UpdateRotation(__instance);
        }

        [HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.DoCameraCollision))]
        [HarmonyPostfix]
        private static void Postfix_vp_FPCamera_DoCameraCollision(vp_FPCamera __instance)
        {
            LocalPlayerCameraUpdater.FindUpdater(__instance.Parent)?.UpdatePosition(__instance);
        }
    }

    // ScreenEffects.SetScreenEffect?
}
public interface IRootMovementUpdater
{
    public void LateUpdateMovement(Transform playerCameraTransform, Transform playerOriginTransform, bool isRiggedWeapon, float _dt);
    public int Priority { get; }
}

public class RootMovementUpdaterComparer : IComparer<IRootMovementUpdater>
{
    public int Compare(IRootMovementUpdater x, IRootMovementUpdater y)
    {
        return x.Priority.CompareTo(y.Priority);
    }
}

public static class CameraLateUpdater
{
    private static SortedSet<IRootMovementUpdater> sset_updaters = new SortedSet<IRootMovementUpdater>(new RootMovementUpdaterComparer());
    private static EntityPlayerLocal player;
    private static Transform playerOriginTransform;
    private static Transform vanillaOriginTransform;
    private static bool isRigWeapon;
    private static Vector3 rigWeaponLocalPosition;
    private static Quaternion rigWeaponLocalRotation;

    public static void Init(EntityPlayerLocal player)
    {
        CameraLateUpdater.player = player;
        vanillaOriginTransform = player.cameraTransform.FindInAllChildren("Hips");
    }

    public static void Cleanup()
    {
        player = null;
        playerOriginTransform = null;
        isRigWeapon = false;
        sset_updaters.Clear();
    }

    public static void UpdateHoldingItem(Transform transform)
    {
        if (!player)
        {
            return;
        }
        AnimationTargetsAbs targets = transform?.GetComponent<AnimationTargetsAbs>();

        if (targets && !targets.Destroyed && targets.ItemFpv && targets is RigTargets)
        {
            playerOriginTransform = targets.ItemAnimator.transform;
            isRigWeapon = true;
            rigWeaponLocalPosition = playerOriginTransform.localPosition;
            rigWeaponLocalRotation = playerOriginTransform.localRotation;
        }
        else
        {
            playerOriginTransform = vanillaOriginTransform;
            isRigWeapon = false;
            rigWeaponLocalPosition = Vector3.zero;
            rigWeaponLocalRotation = Quaternion.identity;
        }
    }

    public static void RegisterUpdater(IRootMovementUpdater updater)
    {
        //Log.Out($"added updater priority {updater.Priority} type {updater.GetType().FullName}\n{StackTraceUtility.ExtractStackTrace()}");
        if (!sset_updaters.Contains(updater))
        {
            sset_updaters.Add(updater);
        }
    }

    public static void UnregisterUpdater(IRootMovementUpdater updater)
    {
        sset_updaters.RemoveWhere(u => u == updater);
        //Log.Out($"remove updater type {updater.GetType().FullName}\n{StackTraceUtility.ExtractStackTrace()}");
    }

    public static void LateUpdate(float _dt)
    {
        if (!player || !playerOriginTransform)
        {
            return;
        }
        if (isRigWeapon)
        {
            playerOriginTransform.SetLocalPositionAndRotation(rigWeaponLocalPosition, rigWeaponLocalRotation);
        }
        if (!player.vp_FPController.enabled)
        {
            player.cameraTransform.position = player.vp_FPController.SmoothPosition;
            player.cameraTransform.localPosition += player.vp_FPCamera.m_PositionSpring.State + player.vp_FPCamera.m_PositionSpring2.State;
        }
        foreach (var updater in sset_updaters)
        {
            updater?.LateUpdateMovement(player.cameraTransform, playerOriginTransform, isRigWeapon, _dt);
        }
    }
}

public static class CameraAnimationUpdater
{
    private static Vector3 camPosOffset;
    private static Quaternion camRotOffset;
    private static bool valueSuppliedThisFrame;

    public static void SupplyCameraOffset(Vector3 camPosOffset,  Quaternion camRotOffset)
    {
        CameraAnimationUpdater.valueSuppliedThisFrame = true;
        CameraAnimationUpdater.camPosOffset += camPosOffset;
        CameraAnimationUpdater.camRotOffset *= camRotOffset;
    }

    public static void Init()
    {
        CameraLateUpdater.RegisterUpdater(new CameraAnimationMovementUpdater());
    }

    private class CameraAnimationMovementUpdater : IRootMovementUpdater
    {
        public int Priority => 900;
        private Vector3 camPosOffsetCur;
        private Quaternion camRotOffsetCur;
        private float failsafeLerpTimeTotal = 0.2f, failsafeLerpTimeCur = 0f;

        public void LateUpdateMovement(Transform playerCameraTransform, Transform playerOriginTransform, bool isRiggedWeapon, float _dt)
        {
            if (valueSuppliedThisFrame)
            {
                valueSuppliedThisFrame = false;
                if (failsafeLerpTimeCur > 0)
                {
                    failsafeLerpTimeCur += _dt;
                    float t = failsafeLerpTimeCur / failsafeLerpTimeTotal;
                    camPosOffsetCur = Vector3.Lerp(camPosOffsetCur, camPosOffset, t);
                    camRotOffsetCur = Quaternion.Slerp(camRotOffsetCur, camRotOffset, t);
                    if (failsafeLerpTimeCur > failsafeLerpTimeTotal)
                    {
                        failsafeLerpTimeCur = 0;
                    }
                }
                else
                {
                    camPosOffsetCur = camPosOffset;
                    camRotOffsetCur = camRotOffset;
                }
            }
            else
            {
                failsafeLerpTimeCur += _dt;
                if (failsafeLerpTimeCur > failsafeLerpTimeTotal)
                {
                    failsafeLerpTimeCur = failsafeLerpTimeTotal;
                }
                float t = failsafeLerpTimeCur / failsafeLerpTimeTotal;
                camPosOffsetCur = Vector3.Lerp(camPosOffsetCur, Vector3.zero, t);
                camRotOffsetCur = Quaternion.Slerp(camRotOffsetCur, Quaternion.identity, t);
            }
            camPosOffset = Vector3.zero;
            camRotOffset = Quaternion.identity;
            playerCameraTransform.localPosition += camPosOffsetCur;
            playerCameraTransform.localRotation *= camRotOffsetCur;
        }
    }
}