using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Reflection;
using UniLinq;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace KFCommonUtilityLib
{
    public class ItemActionModuleProcessor : IModuleProcessor
    {
        TypeDefinition typedef_newActionData;
        FieldDefinition[] arr_flddef_data;

        public Type GetModuleTypeByName(string name)
        {
            return ReflectionHelpers.GetTypeWithPrefix("ActionModule", name);
        }

        public bool BuildConstructor(ModuleManipulator manipulator, MethodDefinition mtddef_ctor)
        {
            manipulator.BuildDefaultConstructor(mtddef_ctor);
            var ins_ret = mtddef_ctor.Body.Instructions[mtddef_ctor.Body.Instructions.Count - 1];
            mtddef_ctor.Body.Instructions.RemoveAt(mtddef_ctor.Body.Instructions.Count - 1);
            var il = mtddef_ctor.Body.GetILProcessor();
            // check and set required user data bits
            byte bitsUsed = 0;
            for (int i = 0; i < manipulator.moduleTypes.Length; i++)
            {
                Type moduleType = manipulator.moduleTypes[i];
                var attr = moduleType.GetCustomAttribute<RequireUserDataBits>();
                if (attr == null)
                    continue;

                var fld_mask = AccessTools.Field(moduleType, attr.MaskField);
                if (fld_mask == null)
                    throw new ArgumentException($"Field {attr.MaskField} not found in module type {moduleType.FullName}!");
                if (Type.GetTypeCode(fld_mask.FieldType) != TypeCode.Int32)
                    throw new ArgumentException($"Field {attr.MaskField} in module type {moduleType.FullName} is not of type Int32!");

                var fld_shift = AccessTools.Field(moduleType, attr.ShiftField);
                if (fld_shift == null)
                    throw new ArgumentException($"Field {attr.ShiftField} not found in module type {moduleType.FullName}!");
                if (Type.GetTypeCode(fld_shift.FieldType) != TypeCode.Byte)
                    throw new ArgumentException($"Field {attr.ShiftField} in module type {moduleType.FullName} is not of type Byte!");

                int shift = 32 - attr.Bits - bitsUsed;
                int mask = ((1 << attr.Bits) - 1) << shift;
                bitsUsed += attr.Bits;
                var flddef_module = manipulator.arr_flddef_modules[i];
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, flddef_module);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldc_I4, mask);
                il.Emit(OpCodes.Stfld, manipulator.module.ImportReference(fld_mask));
                il.Emit(OpCodes.Ldc_I4, shift);
                il.Emit(OpCodes.Stfld, manipulator.module.ImportReference(fld_shift));
            }
            il.Append(ins_ret);
            return true;
        }

        public void InitModules(ModuleManipulator manipulator, Type targetType, Type baseType, params Type[] moduleTypes)
        {
            ModuleDefinition module = manipulator.module;
            //Find ItemActionData subtype
            MethodInfo mtdinf_create_data = null;
            {
                Type type_itemActionBase = targetType;
                while (baseType.IsAssignableFrom(type_itemActionBase))
                {
                    mtdinf_create_data = type_itemActionBase.GetMethod(nameof(ItemAction.CreateModifierData), BindingFlags.Public | BindingFlags.Instance);
                    if (mtdinf_create_data != null)
                        break;
                    mtdinf_create_data = mtdinf_create_data.GetBaseDefinition();
                }
            }

            //ACTION MODULE DATA TYPES
            var arr_type_data = moduleTypes.Select(m => m.GetCustomAttribute<TypeDataTargetAttribute>()?.DataType).ToArray();
            //Create new ItemActionData
            //Find CreateModifierData
            MethodDefinition mtddef_create_data = module.ImportReference(mtdinf_create_data).Resolve();
            //ItemActionData subtype is the return type of CreateModifierData
            TypeReference typeref_actiondata = ((MethodReference)mtddef_create_data.Body.Instructions[mtddef_create_data.Body.Instructions.Count - 2].Operand).DeclaringType;
            //Get type by assembly qualified name since it might be from mod assembly
            Type type_itemActionData = Type.GetType(Assembly.CreateQualifiedName(typeref_actiondata.Module.Assembly.Name.Name, typeref_actiondata.FullName));
            MethodReference mtdref_data_ctor;
            if (ModuleManagers.PatchType(type_itemActionData, typeof(ItemActionData), arr_type_data.Where(static t => t != null).ToArray(), new ItemActionDataModuleProcessor(manipulator.typedef_newTarget, moduleTypes, manipulator.arr_flddef_modules, arr_type_data.Select(static t => t != null).ToArray(), out arr_flddef_data), out var str_data_type_name) && ModuleManagers.TryFindInCur(str_data_type_name, out typedef_newActionData))
            {
                module.Types.Remove(typedef_newActionData);
                manipulator.typedef_newTarget.NestedTypes.Add(typedef_newActionData);
                mtdref_data_ctor = typedef_newActionData.GetConstructors().FirstOrDefault();
            }
            else
            {
                mtdref_data_ctor = module.ImportReference(type_itemActionData.GetConstructor(new Type[] { typeof(ItemInventoryData), typeof(int) }));
            }

            //Create ItemAction.CreateModifierData override
            MethodDefinition mtddef_create_modifier_data = new MethodDefinition(nameof(ItemAction.CreateModifierData), MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot, module.ImportReference(typeof(ItemActionData)));
            mtddef_create_modifier_data.Parameters.Add(new ParameterDefinition("_invData", Mono.Cecil.ParameterAttributes.None, module.ImportReference(typeof(ItemInventoryData))));
            mtddef_create_modifier_data.Parameters.Add(new ParameterDefinition("_indexInEntityOfAction", Mono.Cecil.ParameterAttributes.None, module.TypeSystem.Int32));
            var il = mtddef_create_modifier_data.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Newobj, mtdref_data_ctor);
            il.Emit(OpCodes.Ret);
            manipulator.typedef_newTarget.Methods.Add(mtddef_create_modifier_data);
        }

        public bool MatchSpecialArgs(ParameterDefinition par, MethodDefinition mtddef_target, MethodPatchInfo mtdpinf_derived, int moduleIndex, List<Instruction> list_inst_pars, ILProcessor il)
        {
            switch (par.Name)
            {
                //load injected data instance
                case "__customData":
                    var flddef_data = arr_flddef_data[moduleIndex];
                    if (flddef_data == null)
                        throw new ArgumentNullException($"No Injected ItemActionData in {mtddef_target.DeclaringType.FullName}! module index {moduleIndex}");
                    int index = -1;
                    for (int j = 0; j < mtdpinf_derived.Method.Parameters.Count; j++)
                    {
                        if (mtdpinf_derived.Method.Parameters[j].ParameterType.Name == "ItemActionData")
                        {
                            index = j;
                            break;
                        }
                    }
                    if (index < 0)
                        throw new ArgumentException($"ItemActionData is not present in target method! Patch method: {mtddef_target.DeclaringType.FullName}.{mtddef_target.Name}");
                    list_inst_pars.Add(MonoCecilExtensions.LoadArgAtIndex(index, false, false, mtdpinf_derived.Method.Parameters, il));
                    //list_inst_pars.Add(il.Create(OpCodes.Ldarg_S, mtdpinf_derived.Method.Parameters[index]));
                    list_inst_pars.Add(il.Create(OpCodes.Castclass, typedef_newActionData));
                    list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, flddef_data));
                    return true;
            }
            return false;
        }
    }
}
