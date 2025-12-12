using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

namespace FPVLegs
{
    [HarmonyPatch]
    public static class FPVLegPatches
    {
        [HarmonyPatch(typeof(SDCSUtils), nameof(SDCSUtils.CreateVizTP))]
        [HarmonyPrefix]
        private static void Prefix_SDCSUtils_CreateTP(EntityAlive entity, ref bool isFPV, out bool __state)
        {
            __state = isFPV;
            if (entity is EntityPlayerLocal)
            {
                entity.emodel.IsFPV = false;
                isFPV = false;
            }
        }

        [HarmonyPatch(typeof(SDCSUtils), nameof(SDCSUtils.CreateVizTP))]
        [HarmonyPostfix]
        private static void Postfix_SDCSUtils_CreateTP(EntityAlive entity, ref bool isFPV, bool __state)
        {
            if (entity is EntityPlayerLocal player)
            {
                entity.emodel.IsFPV = __state;
                isFPV = __state;
                if (__state)
                {
                    UpdateTPVMeshState(entity, false);
                }

                UpdateTPVRendererState(entity, !player.bFirstPersonView || !player.vp_FPCamera.Locked3rdPerson);
            }
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.SetCameraAttachedToPlayer))]
        [HarmonyPostfix]
        private static void Postfix_EntityPlayerLocal_SetCameraAttachedToPlayer(EntityPlayerLocal __instance)
        {
            UpdateTPVRendererState(__instance, !__instance.bFirstPersonView || !__instance.vp_FPCamera.Locked3rdPerson);
        }

        public static void UpdateTPVMeshState(EntityAlive entity, bool enabled)
        {
            //Log.Out($"[FPVLegs] EntityPlayerLocal.UpdateTPVMeshState called - enabled {enabled}\n{StackTraceUtility.ExtractStackTrace()}");
            var model = entity.emodel.GetModelTransform();
            if (!model)
            {
                return;
            }
            foreach (Transform child in model)
            {
                if (child.name.Contains("head", StringComparison.OrdinalIgnoreCase) || child.name == "hands" || child.name.Contains("hair", StringComparison.OrdinalIgnoreCase))
                {
                    child.gameObject.SetActive(enabled);
                    //Log.Out($"[FPVLegs] Set {child.name} active: {enabled}");
                }
            }

            foreach (var cloth in model.GetComponentsInChildren<Cloth>())
            {
                cloth.enabled = enabled;
            }

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = (enabled || (!FPVLegMode.disableShadow && !model.TryGetComponent<Cloth>(out _))) ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    skinnedMeshRenderer.forceMatrixRecalculationPerRender = !enabled;
                }
                renderer.motionVectorGenerationMode = enabled ? MotionVectorGenerationMode.Camera : MotionVectorGenerationMode.ForceNoMotion;
            }
        }

        public static void UpdateTPVRendererState(EntityAlive entity, bool enabled)
        {
            var model = entity.emodel.GetModelTransform();
            if (!model)
            {
                return;
            }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.forceRenderingOff = !enabled;
            }
        }

        private static void UpdateTPVAnimatorState(EntityPlayerLocal player)
        {
            //var animator = player.emodel?.GetModelTransform()?.GetComponent<Animator>();
            var animator = player.emodel?.avatarController?.GetAnimator();
            player.vp_FPCamera.gameObject.GetOrAddComponent<FPVLegCameraCallback>().enabled = player.bFirstPersonView;
            if (animator)
            {
                animator.enabled = true;
                animator.gameObject.GetOrAddComponent<FPVLegHelper>().Init(player);
            }
            else
            {
                Log.Warning($"TPV animator null {StackTraceUtility.ExtractStackTrace()}");
            }
        }

        [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController.TPVResetAnimPose))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_AvatarLocalPlayerController_TPVResetAnimPose(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_frames = AccessTools.Field(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController.tpvDisableInFrames));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].StoresField(fld_frames))
                {
                    codes[i - 1].opcode = OpCodes.Ldc_I4_0;
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.switchModelView))]
        [HarmonyPostfix]
        private static void Postfix_EntityPlayerLocal_switchModelView(EntityPlayerLocal __instance, EnumEntityModelView modelView)
        {
            UpdateTPVAnimatorState(__instance);
            if (modelView == EnumEntityModelView.ThirdPerson)
            {
                var model = __instance.emodel?.GetModelTransform();
                if (model)
                {
                    model.localPosition = Vector3.zero;
                }
            }
            //UpdateTPVMeshState(__instance, modelView != EnumEntityModelView.FirstPerson);
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.AfterPlayerRespawn))]
        [HarmonyPostfix]
        private static void Postfix_EntityPlayerLocal_AfterPlayerRespawn(EntityPlayerLocal __instance)
        {
            UpdateTPVAnimatorState(__instance);
            UpdateTPVMeshState(__instance, !__instance.bFirstPersonView);
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.SetAlive))]
        [HarmonyPostfix]
        private static void Postfix_EntityPlayerLocal_SetAlive(EntityPlayerLocal __instance)
        {
            UpdateTPVAnimatorState(__instance);
            UpdateTPVMeshState(__instance, !__instance.bFirstPersonView);
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Kill))]
        [HarmonyPostfix]
        private static void Postfix_EntityPlayerLocal_Kill(EntityPlayerLocal __instance)
        {
            UpdateTPVMeshState(__instance, true);
        }

        //[HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.FixedUpdate))]
        //[HarmonyTranspiler]
        //private static IEnumerable<CodeInstruction> Transpiler_vp_FPCamera_FixedUpdate(IEnumerable<CodeInstruction> instructions)
        //{
        //    var codes = new List<CodeInstruction>(instructions);
        //    var mtd_shake = AccessTools.Method(typeof(vp_FPCamera), nameof(vp_FPCamera.UpdateShakes));
        //    for (int i = 0; i < codes.Count; i++)
        //    {
        //        if (codes[i].Calls(mtd_shake))
        //        {
        //            codes.InsertRange(i + 1, new[]
        //            {
        //                new CodeInstruction(OpCodes.Ldarg_0),
        //                CodeInstruction.Call(typeof(FPVLegPatches), nameof(UpdateTpvPosition))
        //            });
        //            break;
        //        }
        //    }
        //    return codes;
        //}

        //[HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.LateUpdate))]
        //[HarmonyPostfix]
        //private static void Postfix_vp_FPCamera_LateUpdate(vp_FPCamera __instance)
        //{
        //    UpdateTpvPosition(__instance);
        //}

        [HarmonyPatch(typeof(MinEventActionAttachPrefabToHeldItem), nameof(MinEventActionAttachPrefabToHeldItem.Execute))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_MinEventActionAttachPrefabToHeldItem_Execute(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            var fld_pos = AccessTools.Field(typeof(MinEventActionAttachPrefabToHeldItem), nameof(MinEventActionAttachPrefabToHeldItem.local_offset));
            var prop_scale = AccessTools.PropertySetter(typeof(Transform), nameof(Transform.localScale));
            var prop_one = AccessTools.PropertyGetter(typeof(Vector3), nameof(Vector3.one));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_pos))
                {
                    codes.InsertRange(i - 2, new[]
                    {
                        new CodeInstruction(codes[i - 2].opcode, codes[i - 2].operand),
                        new CodeInstruction(OpCodes.Callvirt, prop_one),
                        new CodeInstruction(OpCodes.Callvirt, prop_scale)
                    });
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.setHoldingItemTransform))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Inventory_setHoldingItemTransform(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var prop_pos = AccessTools.PropertySetter(typeof(Transform), nameof(Transform.position));
            var prop_scale = AccessTools.PropertySetter(typeof(Transform), nameof(Transform.localScale));
            var prop_one = AccessTools.PropertyGetter(typeof(Vector3), nameof(Vector3.one));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(prop_pos))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(codes[i - 2].opcode, codes[i - 2].operand),
                        new CodeInstruction(OpCodes.Callvirt, prop_one),
                        new CodeInstruction(OpCodes.Callvirt, prop_scale)
                    });
                    break;
                }
            }
            return codes;
        }
    }

    public class FPVLegHelper : MonoBehaviour
    {
        private Animator animator;
        private (int stateID, int layerID)[] layersToDisable;
        private EntityPlayerLocal player;
        public Transform spine, spine1, spine2, spine3, neck, head, fpvHead, lShoulder, rShoulder, lUpperArm, rUpperArm, lUpperArmRoll, rUpperArmRoll, lLowerArmRoll, rLowerArmRoll, lLowerArm, rLowerArm, lHand, rHand;
        private float spineAngle = -10, spine1Angle = 0, spine2Angle = 0, spine3Angle = -30;

        public void Init(EntityPlayerLocal player)
        {
            this.player = player;
            fpvHead = player.cameraTransform.FindInChildren("Head");
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            layersToDisable = new[]
            {
                (Animator.StringToHash("Empty"), animator.GetLayerIndex("RightHandHoldPoses")),
                (Animator.StringToHash("Empty"), animator.GetLayerIndex("RangedRightHandHoldPoses")),
                (Animator.StringToHash("Empty"), animator.GetLayerIndex("AdditiveOffsetHoldPoses")),
                (Animator.StringToHash("Empty"), animator.GetLayerIndex("RightArmHoldPoses")),
                (Animator.StringToHash("Empty"), animator.GetLayerIndex("BothArmsHoldPoses")),
                (Animator.StringToHash("Empty"), animator.GetLayerIndex("UpperBodyAttack")),
                (Animator.StringToHash("Empty"), animator.GetLayerIndex("BowDrawAndFire")),
                (Animator.StringToHash("Empty"), animator.GetLayerIndex("UpperBodyUseAndReload")),
                (Animator.StringToHash("Empty"), animator.GetLayerIndex("AdditiveRangedAttack")),
            };
            spine = transform.FindInChilds("Spine");
            spine1 = spine.Find("Spine1");
            spine2 = spine1.Find("Spine2");
            spine3 = spine2.Find("Spine3");
            neck = spine3.Find("Neck");
            head = neck.Find("Head");
            lShoulder = spine3.Find("LeftShoulder");
            rShoulder = spine3.Find("RightShoulder");
            lUpperArm = lShoulder.Find("LeftArm");
            rUpperArm = rShoulder.Find("RightArm");
            lUpperArmRoll = lUpperArm.Find("LeftArmRoll");
            rUpperArmRoll = rUpperArm.Find("RightArmRoll");
            lLowerArmRoll = lUpperArmRoll.Find("LeftForeArm");
            rLowerArmRoll = rUpperArmRoll.Find("RightForeArm");
            lLowerArm = lLowerArmRoll.Find("LeftForeArmRoll");
            rLowerArm = rLowerArmRoll.Find("RightForeArmRoll");
            lHand = lLowerArm.Find("LeftHand");
            rHand = rLowerArm.Find("RightHand");
        }

        internal void LateUpdateTransform()
        {
            spine.localEulerAngles = new Vector3(0f, 0f, 0f);
            spine1.localEulerAngles = new Vector3(0f, 0f, 0f);
            spine2.localEulerAngles = new Vector3(0f, 0f, 0f);
            spine3.localEulerAngles = new Vector3(0f, 0f, 0f);
            neck.localEulerAngles = new Vector3(0f, 0f, 0f);
            head.localEulerAngles = new Vector3(0f, 0f, 0f);
        }

        private void Update()
        {
            if (animator && player && player.IsAlive() && player.bFirstPersonView)
            {
                animator.SetInteger(AvatarController.weaponHoldTypeHash, 0);
            }
        }

        private void LateUpdate()
        {
            if (animator && player && player.emodel)
            {
                if (!player.emodel.IsRagdollActive)
                {
                    if (player.bFirstPersonView)
                    {
                        foreach (var layer in layersToDisable)
                        {
                            animator.Play(layer.stateID, layer.layerID, 0f);
                        }

                        if (!FPVLegMode.newMode)
                        {
                            spine.localEulerAngles = new Vector3(spineAngle, 0f, 0f);
                            spine1.localEulerAngles = new Vector3(spine1Angle, 0f, 0f);
                            spine2.localEulerAngles = new Vector3(spine2Angle, 0f, 0f);
                            spine3.localEulerAngles = new Vector3(spine3Angle, 0f, 0f);
                            neck.localEulerAngles = new Vector3(0f, 0f, 0f);
                            head.localEulerAngles = new Vector3(0f, 0f, 0f);
                        }
                        lShoulder.localEulerAngles = new Vector3(0f, -7f, 0f);
                        rShoulder.localEulerAngles = new Vector3(0f, 187f, 180f);
                        lUpperArm.localScale = Vector3.zero;
                        rUpperArm.localScale = Vector3.zero;
                    }
                }
                else
                {
                    lUpperArm.localScale = Vector3.one;
                    rUpperArm.localScale = Vector3.one;
                }

                //lUpperArm.localEulerAngles = new Vector3(0f, 0f, 45f);
                //rUpperArm.localEulerAngles = new Vector3(0f, 0f, 45f);
                //lUpperArmRoll.localEulerAngles = new Vector3(0f, 0f, 0f);
                //rUpperArmRoll.localEulerAngles = new Vector3(0f, 0f, 0f);
                //lLowerArmRoll.localEulerAngles = new Vector3(0f, 45f, 0f);
                //rLowerArmRoll.localEulerAngles = new Vector3(0f, 45f, 0f);
                //lLowerArm.localEulerAngles = new Vector3(0f, 0f, 0f);
                //rLowerArm.localEulerAngles = new Vector3(0f, 0f, 0f);
                //lHand.localEulerAngles = new Vector3(0f, 0f, 0f);
                //rHand.localEulerAngles = new Vector3(0f, 0f, 0f);
            }
        }
    }

    public class FPVLegCameraCallback : MonoBehaviour
    {
        private static Vector3 legOffset = new Vector3(0f, 0.25f, -0.4f);
        private static Vector3 headOffset = new Vector3(0f, 0.05f, -0.3f);
        private vp_FPCamera vp_camera;

        public void OnEnable()
        {
            vp_camera = GetComponent<vp_FPCamera>();
            if (!vp_camera)
            {
                Destroy(this);
            }
        }

        public void OnPreCull()
        {
            UpdateTpvPosition();
        }

        public void OnPostRender()
        {
            var model = vp_camera.FPController.localPlayer.emodel?.GetModelTransform();
            if (model)
            {
                model.localPosition = Vector3.zero;
            }
        }

        private void UpdateTpvPosition()
        {
            if (!vp_camera.FPController?.localPlayer)
            {
                return;
            }
            var model = vp_camera.FPController.localPlayer.emodel?.GetModelTransform();
            if (model && vp_camera.transform.parent)
            {
                var helper = model.GetComponent<FPVLegHelper>();
                if (FPVLegMode.newMode)
                {
                    helper.LateUpdateTransform();
                    var originalModelPos = model.position;
                    var targetHeadPos = vp_camera.transform.parent.TransformPoint(vp_camera.transform.localPosition + headOffset);
                    var headTrans = vp_camera.FPController.localPlayer.emodel.GetHeadTransform();
                    model.position += targetHeadPos - headTrans.position;
                }
                else
                {
                    model.localPosition = legOffset;
                    model.position += vp_camera.transform.parent.TransformDirection(vp_camera.transform.localPosition - vp_camera.m_PositionSpring.RestState);
                }
            }
        }
    }
}
