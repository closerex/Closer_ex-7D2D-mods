using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Harmony;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;

namespace KFCommonUtilityLib
{
    [TypeTarget(typeof(ItemClass))]
    public class ItemClassModuleProcessor : IModuleProcessor
    {
        private static class TranspilerHelper
        {
            public static ItemClassModuleProcessor curProcessor;

            private static readonly MethodInfo mtd_load_data_module =
                AccessTools.Method(typeof(ItemClassModuleProcessor), nameof(LoadDataModule));

            public static IEnumerable<CodeInstruction> LoadDataModuleHelper(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();

                for (int i = 0; i < codes.Count; i++)
                {
                    var code = codes[i];
                    if (code.opcode == OpCodes.Call &&
                        code.operand is MethodInfo { IsGenericMethod: true, ContainsGenericParameters: false } mtd &&
                        mtd.GetGenericMethodDefinition() == mtd_load_data_module)
                    {
                        int dataIndex =
                            Array.IndexOf(Array.ConvertAll(curProcessor.arr_fldbd_data, fld => fld?.FieldType),
                                mtd.GetGenericArguments()[0]);
                        if (dataIndex >= 0)
                        {
                            code.operand = AccessTools.Method(curProcessor.type_newInvData,
                                $"{ModuleUtils.CreateFieldName(curProcessor.arr_fldbd_data[dataIndex].FieldType)}.get_Instance");
                            codes.InsertRange(i, new[]
                            {
                                new CodeInstruction(OpCodes.Ldfld, InventoryModuleStorage.DataField).WithLabels(code.ExtractLabels()).WithBlocks(code.ExtractBlocks()),
                                new CodeInstruction(OpCodes.Castclass, curProcessor.type_newInvData)
                            });
                            i += 2;
                        }
                        else
                        {
                            Log.Error($"Accessing invalid ItemActionData module! method: {code.operand}");
                        }
                    }
                }

                return codes;
            }
        }

        private Type type_newInvData;
        private FieldBuilder[] arr_fldbd_data;
        
        public static T LoadDataModule<T>(ItemInventoryData invData) where T : class
        {
            return invData == null ? null : InventoryModuleStorage.Get(invData).LoadModule<T>();
        }

        public static bool IsDataModuleContainerFor<T>(ItemInventoryData invData, out T instance) where T : class
        {
            instance = LoadDataModule<T>(invData);
            return instance != null;
        }
        
        public Type GetModuleTypeByName(string name)
        {
            return ReflectionHelpers.GetTypeWithPrefix("ItemModule", name);
        }

        public void InitModules(ModuleManipulator manipulator)
        {
            TranspilerHelper.curProcessor = this;
            var arr_type_data = manipulator.moduleTypes.Select(static m => m.GetCustomAttribute<TypeDataTargetAttribute>()?.DataType).ToArray();
            if (ModuleManagers.PatchType(InventoryModuleStorage.BaseType, InventoryModuleStorage.BaseType, manipulator.typebd_newTarget, arr_type_data.Where(static t => t != null).ToArray(), new ItemInventoryDataModuleProcessor(manipulator.typebd_newTarget, manipulator.moduleTypes, manipulator.arr_fldbd_modules.Where((f, i) => arr_type_data[i] != null).ToArray(), arr_type_data.Select(static t => t != null).ToArray(), out arr_fldbd_data), out type_newInvData))
            {
                var mtdinf_createdata = typeof(ICustomInvData).GetMethod(nameof(ICustomInvData.createItemInventoryData));
                manipulator.typebd_newTarget.AddInterfaceImplementation(typeof(ICustomInvData));
                var mtdbd_createdata = manipulator.typebd_newTarget.DefineMethod($"{typeof(ICustomInvData).FullName}.{mtdinf_createdata.Name}",
                    MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final,
                    mtdinf_createdata.CallingConvention,
                    mtdinf_createdata.ReturnType,
                    mtdinf_createdata.GetParameters().Select(static p => p.ParameterType).ToArray());
                manipulator.typebd_newTarget.DefineMethodOverride(mtdbd_createdata, mtdinf_createdata);
                mtdinf_createdata.CopyParamInfoTo(mtdbd_createdata);

                var il = mtdbd_createdata.GetILGenerator();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Newobj, type_newInvData.GetDeclaredConstructors().First());
                il.Emit(OpCodes.Ret);
            }
        }

        public void BuildConstructor(ModuleManipulator manipulator, ILGenerator generator)
        {

        }

        public bool MatchSpecialArgs(ModuleManipulator manipulator, ILGenerator generator, ParameterInfo par, MethodPatchInfo mtdpinf_derived, MethodOverrideInfo mtdoinf_target)
        {
            switch (par.Name)
            {
                //load injected data instance
                case "__customData":
                    var fldbd_data = arr_fldbd_data[mtdoinf_target.moduleIndex];
                    if (fldbd_data == null)
                        throw new ArgumentNullException($"No Injected ItemInventoryData in {mtdoinf_target.mtdinf_target.DeclaringType.FullName}! module index {mtdoinf_target.moduleIndex}");
                    int index = -1;
                    ParameterInfo[] paramInfo = mtdoinf_target.mtdinf_base.GetParameters();
                    for (int j = 0; j < paramInfo.Length; j++)
                    {
                        if (typeof(ItemInventoryData).IsAssignableFrom(paramInfo[j].ParameterType))
                        {
                            index = j + 1;
                            break;
                        }
                    }
                    if (index < 0)
                        throw new ArgumentException($"ItemInventoryData is not present in target method! Patch method: {mtdoinf_target.mtdinf_target.DeclaringType.FullName}.{mtdoinf_target.mtdinf_target.Name}");
                    generator.LoadArg(index);
                    generator.Emit(OpCodes.Ldfld, InventoryModuleStorage.DataField);
                    generator.Emit(OpCodes.Castclass, type_newInvData);
                    generator.Emit(par.ParameterType.IsByRef ? OpCodes.Ldflda : OpCodes.Ldfld, fldbd_data);
                    return true;
            }
            return false;
        }

        public bool MatchConstructorArgs(ModuleManipulator manipulator, ILGenerator generator, ParameterInfo par, ParameterBuilder[] paramInfo, Type[] paramTypes, ConstructorInfo ctorinf_target, int moduleIndex)
        {
            return false;
        }

        public void ApplyPatchModifiers(PatchInfo patchInfo, string owner)
        {
            patchInfo.AddTranspilers(owner,
                new HarmonyMethod(AccessTools.Method(typeof(TranspilerHelper),
                    nameof(TranspilerHelper.LoadDataModuleHelper))));
        }

        public bool DefineConstructorArgs(ModuleManipulator manipulator, ConstructorBuilder ctorbd, out ParameterBuilder[] pbs)
        {
            pbs = null;
            return false;
        }
    }
}
