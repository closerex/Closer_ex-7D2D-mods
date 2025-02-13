﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using UnityEngine.Scripting;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace KFCommonUtilityLib
{
    public class ItemActionModuleProcessor : IModuleProcessor
    {
        TypeDefinition typedef_newActionData;
        Type[] arr_type_data;
        TypeReference[] arr_typeref_data;
        FieldDefinition[] arr_flddef_data;
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
            arr_type_data = manipulator.arr_attr_modules.Select(a => a.DataType).ToArray();
            arr_typeref_data = arr_type_data.Select(a => a != null ? module.ImportReference(a) : null).ToArray();
            //Create new ItemActionData
            //Find CreateModifierData
            MethodDefinition mtddef_create_data = module.ImportReference(mtdinf_create_data).Resolve();
            //ItemActionData subtype is the return type of CreateModifierData
            TypeReference typeref_actiondata = ((MethodReference)mtddef_create_data.Body.Instructions[mtddef_create_data.Body.Instructions.Count - 2].Operand).DeclaringType;
            //Get type by assembly qualified name since it might be from mod assembly
            Type type_itemActionData = Type.GetType(Assembly.CreateQualifiedName(typeref_actiondata.Module.Assembly.Name.Name, typeref_actiondata.FullName));
            typedef_newActionData = new TypeDefinition(null, ModuleUtils.CreateTypeName(type_itemActionData, arr_type_data), TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.NestedPublic | TypeAttributes.Sealed, module.ImportReference(typeref_actiondata));
            typedef_newActionData.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(PreserveAttribute).GetConstructor(Array.Empty<Type>()))));
            manipulator.typedef_newTarget.NestedTypes.Add(typedef_newActionData);

            //Create ItemActionData field
            arr_flddef_data = new FieldDefinition[moduleTypes.Length];
            for (int i = 0; i < moduleTypes.Length; i++)
            {
                if (arr_typeref_data[i] != null)
                {
                    TypeReference typeref_data = arr_typeref_data[i];
                    Type type_data = arr_type_data[i];
                    FieldDefinition flddef_data = new FieldDefinition(ModuleUtils.CreateFieldName(type_data), FieldAttributes.Public, typeref_data);
                    typedef_newActionData.Fields.Add(flddef_data);
                    arr_flddef_data[i] = flddef_data;

                    ModuleUtils.MakeContainerFor(module, manipulator.typeref_interface, typedef_newActionData, type_data, flddef_data, typeref_data);
                }
            }

            //Create ItemActionData constructor
            MethodDefinition mtddef_ctor_data = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            mtddef_ctor_data.Parameters.Add(new ParameterDefinition("_inventoryData", Mono.Cecil.ParameterAttributes.None, module.ImportReference(typeof(ItemInventoryData))));
            mtddef_ctor_data.Parameters.Add(new ParameterDefinition("_indexInEntityOfAction", Mono.Cecil.ParameterAttributes.None, module.TypeSystem.Int32));
            FieldReference fldref_invdata_item = module.ImportReference(typeof(ItemInventoryData).GetField(nameof(ItemInventoryData.item)));
            FieldReference fldref_item_actions = module.ImportReference(typeof(ItemClass).GetField(nameof(ItemClass.Actions)));
            var il = mtddef_ctor_data.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldarg_1));
            il.Append(il.Create(OpCodes.Ldarg_2));
            il.Append(il.Create(OpCodes.Call, module.ImportReference(type_itemActionData.GetConstructor(new Type[] { typeof(ItemInventoryData), typeof(int) }))));
            il.Append(il.Create(OpCodes.Nop));
            for (int i = 0; i < arr_flddef_data.Length; i++)
            {
                if (arr_type_data[i] == null)
                    continue;
                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Ldarg_1));
                il.Append(il.Create(OpCodes.Ldarg_2));
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldfld, fldref_invdata_item);
                il.Emit(OpCodes.Ldfld, fldref_item_actions);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, manipulator.typedef_newTarget);
                il.Emit(OpCodes.Ldfld, manipulator.arr_flddef_modules[i]);
                il.Append(il.Create(OpCodes.Newobj, module.ImportReference(arr_type_data[i].GetConstructor(new Type[] { typeof(ItemInventoryData), typeof(int), moduleTypes[i] }))));
                il.Append(il.Create(OpCodes.Stfld, arr_flddef_data[i]));
                il.Append(il.Create(OpCodes.Nop));
            }
            il.Append(il.Create(OpCodes.Ret));
            typedef_newActionData.Methods.Add(mtddef_ctor_data);

            //Create ItemAction.CreateModifierData override
            MethodDefinition mtddef_create_modifier_data = new MethodDefinition(nameof(ItemAction.CreateModifierData), MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot, module.ImportReference(typeof(ItemActionData)));
            mtddef_create_modifier_data.Parameters.Add(new ParameterDefinition("_invData", Mono.Cecil.ParameterAttributes.None, module.ImportReference(typeof(ItemInventoryData))));
            mtddef_create_modifier_data.Parameters.Add(new ParameterDefinition("_indexInEntityOfAction", Mono.Cecil.ParameterAttributes.None, module.TypeSystem.Int32));
            il = mtddef_create_modifier_data.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Newobj, mtddef_ctor_data);
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
                        throw new ArgumentNullException($"No Injected ItemActionData in {mtddef_target.DeclaringType.FullName}!");
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
                    list_inst_pars.Add(il.Create(OpCodes.Ldarg_S, mtdpinf_derived.Method.Parameters[index]));
                    list_inst_pars.Add(il.Create(OpCodes.Castclass, typedef_newActionData));
                    list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, flddef_data));
                    return true;
            }
            return false;
        }
    }
}
