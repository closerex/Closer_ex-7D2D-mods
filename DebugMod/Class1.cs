using HarmonyLib;
using KFCommonUtilityLib.Harmony;
using MonoMod;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using UniLinq;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace DebugMod
{
    public class Class1 : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (!inited)
            {
                inited = true;
                Log.Out("Loading Patch: " + GetType());
                var harmony = new HarmonyLib.Harmony(GetType().ToString());
                harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
                //try
                //{
                //    TestAssemblyBuilder.AssemblyBuilderTest();
                //}
                //catch (Exception e)
                //{
                //    Log.Exception(e);
                //}
                // TestAssemblyBuilder.MonoModTest();
            }
        }
    }

    public static class TestAssemblyBuilder
    {
        public static void AssemblyBuilderTest()
        {
            var assemblyName = new AssemblyName("Example");
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave, Path.Combine(ModManager.GetMod("DebugMod").Path, "DMDDump"));
            var dynamicModule = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll", true);
            // Add IgnoresAccessChecksTo attribute
            assemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(IgnoresAccessChecksToAttribute).GetConstructor(new Type[] { typeof(string) }),
                                                                          new object[] { typeof(List<>).Assembly.GetName().Name }));
            assemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(SecurityPermissionAttribute).GetConstructor(new Type[] { typeof(SecurityAction) }),
                                                                          new object[] { SecurityAction.RequestMinimum },
                                                                          new[] { typeof(SecurityPermissionAttribute).GetProperty(nameof(SecurityPermissionAttribute.SkipVerification)),
                                                                              typeof(SecurityAttribute).GetProperty(nameof(SecurityAttribute.Unrestricted)) },
                                                                          new object[] { true,
                                                                                         true }));
            assemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(UnverifiableCodeAttribute).GetConstructor(Type.EmptyTypes), new object[0]));
            assemblyBuilder.SetMonoCorlibInternal(true);
            var typeBuilder = dynamicModule.DefineType("ABTest");
            var methodBuilder = typeBuilder.DefineMethod("CallTest", MethodAttributes.Public | MethodAttributes.Static);
            var il = methodBuilder.GetILGenerator();
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(TestAssemblyBuilder), nameof(Call)));
            //il.Emit(OpCodes.Ret);
            typeBuilder.CreateType();
            //var method = typeBuilder.CreateType().GetMethod("CallTest");
            //method.Invoke(null, null);
            assemblyBuilder.Save("Example.dll");
        }

        public static void MonoModTest()
        {
            //Switches.SetSwitchValue("DMDType", "methodbuilder");
            //Switches.SetSwitchValue("DMDDumpTo", Path.Combine(ModManager.GetMod("DebugMod").Path, "DMDDump"));
            //DynamicMethodDefinition dmd = new("Test", null, Type.EmptyTypes);

            //var il = dmd.GetILGenerator();
            ////il.Emit(Mono.Cecil.Cil.OpCodes.Call, dmd.Module.ImportReference(AccessTools.Method(typeof(TestAssemblyBuilder), nameof(Call))));
            //il.Emit(OpCodes.Ret);

            //dmd.Generate().Invoke(null, null);
            //Switches.ClearSwitchValue("DMDType");
            //Switches.ClearSwitchValue("DMDDumpTo");

            var assemblyName = new AssemblyName("Example");
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave, Path.Combine(ModManager.GetMod("DebugMod").Path, "DMDDump"));
            var dynamicModule = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll", true);
            var typeBuilder = dynamicModule.DefineType("ABTest");

            var dmd = new DynamicMethodDefinition("Test", typeof(int), new Type[] { typeof(int), typeof(int)});

            var il = dmd.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ret);

            dmd.GenerateMethodBuilder(typeBuilder);

            Type type = typeBuilder.CreateType();
            var method = type.GetMethod("Test");
            assemblyBuilder.Save("Example.dll");
        }

        private static void Call()
        {
            Log.Out("SOMETHING BEING CALLED");
        }
    }

    //[HarmonyPatch]
    public static class Patches
    {
        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetHoldingItemTransform))]
        [HarmonyPostfix]
        private static void Postfix_SetHoldingItemTransform_EntityAlive(EntityAlive __instance, Transform _transform)
        {
            if (__instance is EntityPlayer)
                Log.Out($"{__instance.GetDebugName()} set holding item transform {(_transform ? _transform.name : "null")}\n{StackTraceUtility.ExtractStackTrace()}");
        }

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.CloneModel), new[] { typeof(World), typeof(ItemValue), typeof(Vector3), typeof(Transform), typeof(BlockShape.MeshPurpose), typeof(TextureFullArray)} )]
        [HarmonyPrefix]
        private static void Prefix_CloneModel_ItemClass(ItemClass __instance, ItemValue _itemValue)
        {
            //if (__instance.renderGameObject && __instance.Actions?[0] is ItemActionRanged)
            //{
            //    __instance.renderGameObject.GetOrAddComponent<LogHelper>();
            //}
            Log.Out($"clone model on item {__instance.GetItemName()} item value {_itemValue.ItemClassOrMissing.GetItemName()}\n{StackTraceUtility.ExtractStackTrace()}");
        }
    }

    //[HarmonyPatch]
    public static class Patches1
    {
        [HarmonyPatch(typeof(ItemActionDynamic), nameof(ItemActionDynamic.GetDamageEntity))]
        [HarmonyPostfix]
        private static void Postfix_GetDamageEntity(ItemActionDynamic __instance, ItemValue _itemValue, float __result)
        {
            Log.Out($"Get Damage Entity on item {_itemValue.ItemClass.Name} action {__instance.ActionIndex} result {__result}\n{StackTraceUtility.ExtractStackTrace()}");
        }
    }

    //[HarmonyPatch]
    public static class Patches2
    {
        private static MinEventParams savedParams = new();
        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.FireEvent))]
        [HarmonyPostfix]
        private static void Postfix_FireEvent(EntityAlive __instance, MinEventTypes _eventType, bool useInventory)
        {
            if (_eventType == MinEventTypes.onOtherDamagedSelf)
            {
                MinEventParams.CopyTo(__instance.MinEventContext, savedParams);
                Log.Out($"FireEvent useInventory {useInventory} instance {__instance.GetDebugName()} self {savedParams.Self.GetDebugName()} other {(savedParams.Other ? savedParams.Other.GetDebugName() : "none")}\n{StackTraceUtility.ExtractStackTrace()}");
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.FireEvent))]
        [HarmonyPostfix]
        private static void Postfix_FireEvent(Inventory __instance, MinEventTypes _eventType, MinEventParams _eventParms)
        {
            if (_eventType == MinEventTypes.onOtherDamagedSelf)
            {
                Log.Out($"FireEvent on inventory param item {(_eventParms?.ItemValue?.ItemClass == null ? "none" : _eventParms.ItemValue.ItemClass.GetLocalizedItemName())}\n{StackTraceUtility.ExtractStackTrace()}");
            }
        }

        private static MinEventParams itemParams = new();
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.FireEvent))]
        [HarmonyPostfix]
        private static void Postfix_FireEvent(ItemClass __instance, MinEventTypes _eventType, MinEventParams _eventParms)
        {
            if (_eventType == MinEventTypes.onOtherDamagedSelf)
            {
                MinEventParams.CopyTo(_eventParms, itemParams);
                Log.Out($"FireEvent on item {__instance.GetItemName()} self {itemParams.Self.GetDebugName()} other {(itemParams.Other ? itemParams.Other.GetDebugName() : "none")}\n{StackTraceUtility.ExtractStackTrace()}");
            }
        }
    }

    [HarmonyPatch]
    public static class CVarTrackPatches
    {
        [HarmonyPatch(typeof(EntityBuffs), nameof(EntityBuffs.SetCustomVar))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_SetCustomVar_EntityBuffs(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var mtd_log = AccessTools.Method(typeof(Log), nameof(Log.Out), new[] { typeof(string) });

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_log))
                {
                    codes.Insert(i + 1, CodeInstruction.CallClosure<Action>(static () =>
                    {
                        Log.Out(StackTraceUtility.ExtractStackTrace());
                    }));
                }
            }

            return codes;
        }
    }

    //[HarmonyPatch]
    public static class DLSSTrackerPatches
    {
        [HarmonyPatch(typeof(DLSS), "ConfigureCameraViewport")]
        [HarmonyPrefix]
        private static void Prefix_ConfigureCameraViewport_DLSS(PostProcessRenderContext context)
        {
            var _camera = context.camera;
            if (_camera.name == "KFPiPCam")
            {
                Log.Out($"ConfigureCameraViewport on camera {_camera.name} pixel size: {_camera.pixelWidth}x{_camera.pixelHeight}");
            }
        }

        [HarmonyPatch(typeof(DLSS), "ConfigureCameraViewport")]
        [HarmonyPostfix]
        private static void Postfix_ConfigureCameraViewport_DLSS(PostProcessRenderContext context, Rect ____originalRect, Vector2Int ___displaySize, Vector2Int ___renderSize)
        {
            var _camera = context.camera;
            if (_camera.name == "KFPiPCam")
            {
                Log.Out($"ConfigureCameraViewport finished, originalRect {____originalRect} displaySize {___displaySize} renderSize {___renderSize} camera pixel size {_camera.pixelWidth}x{_camera.pixelHeight} camera aspect {_camera.aspect} camera rect {_camera.rect}");
            }
        }

        [HarmonyPatch(typeof(PostProcessLayer), "LateUpdate")]
        [HarmonyPrefix]
        private static void Prefix_LateUpdate_PostProcessLayer(PostProcessLayer __instance)
        {
            var _camera = __instance.GetComponent<Camera>();
            if (_camera.name == "KFPiPCam")
            {
                Log.Out($"LateUpdate on camera {_camera.name} pixel size: {_camera.pixelWidth}x{_camera.pixelHeight}");
            }
        }

        [HarmonyPatch(typeof(PostProcessLayer), "LateUpdate")]
        [HarmonyPostfix]
        private static void Postfix_LateUpdate_PostProcessLayer(PostProcessLayer __instance)
        {
            var _camera = __instance.GetComponent<Camera>();
            if (_camera.name == "KFPiPCam")
            {
                Log.Out($"LateUpdate finished on camera {_camera.name} pixel size: {_camera.pixelWidth}x{_camera.pixelHeight}");
            }
        }

        //[HarmonyPatch(typeof(PostProcessLayer), "OnPostRender")]
        //[HarmonyPrefix]
        //private static void Prefix_OnPostRender_PostProcessLayer(PostProcessLayer __instance)
        //{
        //    var _camera = __instance.GetComponent<Camera>();
        //    if (_camera.name == "KFPiPCam")
        //    {
        //        Log.Out($"OnPostRender on camera {_camera.name} pixel size: {_camera.pixelWidth}x{_camera.pixelHeight}");
        //    }
        //}

        //[HarmonyPatch(typeof(PostProcessLayer), "OnPostRender")]
        //[HarmonyPostfix]
        //private static void Postfix_OnPostRender_PostProcessLayer(PostProcessLayer __instance)
        //{
        //    var _camera = __instance.GetComponent<Camera>();
        //    if (_camera.name == "KFPiPCam")
        //    {
        //        Log.Out($"OnPostRender finished on camera {_camera.name} pixel size: {_camera.pixelWidth}x{_camera.pixelHeight}");
        //    }
        //}

        //[HarmonyPatch(typeof(PostProcessLayer), "OnPreCull")]
        //[HarmonyPrefix]
        //private static void Prefix_OnPreCull_PostProcessLayer(PostProcessLayer __instance)
        //{
        //    var _camera = __instance.GetComponent<Camera>();
        //    if (_camera.name == "KFPiPCam")
        //    {
        //        Log.Out($"OnPreCull on camera {_camera.name} pixel size: {_camera.pixelWidth}x{_camera.pixelHeight}");
        //    }
        //}

        //[HarmonyPatch(typeof(PostProcessLayer), "OnPreCull")]
        //[HarmonyPostfix]
        //private static void Postfix_OnPreCull_PostProcessLayer(PostProcessLayer __instance)
        //{
        //    var _camera = __instance.GetComponent<Camera>();
        //    if (_camera.name == "KFPiPCam")
        //    {
        //        Log.Out($"OnPreCull finished on camera {_camera.name} pixel size: {_camera.pixelWidth}x{_camera.pixelHeight}");
        //    }
        //}

        //[HarmonyPatch(typeof(PostProcessLayer), "OnPreRender")]
        //[HarmonyPrefix]
        //private static void Prefix_OnPreRender_PostProcessLayer(PostProcessLayer __instance)
        //{
        //    var _camera = __instance.GetComponent<Camera>();
        //    if (_camera.name == "KFPiPCam")
        //    {
        //        Log.Out($"OnPreRender on camera {_camera.name} pixel size: {_camera.pixelWidth}x{_camera.pixelHeight}");
        //    }
        //}

        //[HarmonyPatch(typeof(PostProcessLayer), "OnPreRender")]
        //[HarmonyPostfix]
        //private static void Postfix_OnPreRender_PostProcessLayer(PostProcessLayer __instance)
        //{
        //    var _camera = __instance.GetComponent<Camera>();
        //    if (_camera.name == "KFPiPCam")
        //    {
        //        Log.Out($"OnPreRender finished on camera {_camera.name} pixel size: {_camera.pixelWidth}x{_camera.pixelHeight}");
        //    }
        //}

        //[HarmonyPatch(typeof(PostProcessLayer), "OnRenderImage")]
        //[HarmonyPrefix]
        //private static void Prefix_OnRenderImage_PostProcessLayer(PostProcessLayer __instance)
        //{
        //    var _camera = __instance.GetComponent<Camera>();
        //    if (_camera.name == "KFPiPCam")
        //    {
        //        Log.Out($"OnRenderImage on camera {_camera.name} pixel size: {_camera.pixelWidth}x{_camera.pixelHeight}");
        //    }
        //}

        //[HarmonyPatch(typeof(PostProcessLayer), "OnRenderImage")]
        //[HarmonyPostfix]
        //private static void Postfix_OnRenderImage_PostProcessLayer(PostProcessLayer __instance)
        //{
        //    var _camera = __instance.GetComponent<Camera>();
        //    if (_camera.name == "KFPiPCam")
        //    {
        //        Log.Out($"OnRenderImage finished on camera {_camera.name} pixel size: {_camera.pixelWidth}x{_camera.pixelHeight}");
        //    }
        //}
    }

    [DisallowMultipleComponent]
    public class LogHelper : MonoBehaviour
    {
        void Awake()
        {
            Log.Out($"item created {gameObject.name}\n{StackTraceUtility.ExtractStackTrace()}");
        }

        void OnEnable()
        {
            Log.Out($"item enabled {gameObject.name}\n{StackTraceUtility.ExtractStackTrace()}");
        }
    }
}
