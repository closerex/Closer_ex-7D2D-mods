using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Harmony;
using MonoMod.Utils;
using System;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;

namespace KFCommonUtilityLib
{
    public class ItemActionModuleProcessor : IModuleProcessor
    {
        Type typebd_newActionData;
        FieldBuilder[] arr_flddef_data;

        public Type GetModuleTypeByName(string name)
        {
            return ReflectionHelpers.GetTypeWithPrefix("ActionModule", name);
        }

        public void InitModules(ModuleManipulator manipulator)
        {
            //Find ItemActionData subtype
            MethodInfo mtdinf_create_data = null;
            {
                Type type_itemActionBase = manipulator.targetType;
                while (manipulator.baseType.IsAssignableFrom(type_itemActionBase))
                {
                    mtdinf_create_data = type_itemActionBase.GetMethod(nameof(ItemAction.CreateModifierData), BindingFlags.Public | BindingFlags.Instance);
                    if (mtdinf_create_data != null)
                        break;
                    type_itemActionBase = type_itemActionBase.BaseType;
                }
            }

            //ACTION MODULE DATA TYPES
            var arr_type_data = manipulator.moduleTypes.Select(m => m.GetCustomAttribute<TypeDataTargetAttribute>()?.DataType).ToArray();
            //Create new ItemActionData
            //Find CreateModifierData
            using (var dmd = new DynamicMethodDefinition(mtdinf_create_data))
            {
                //ItemActionData subtype is the return type of CreateModifierData
                var typeref_actiondata = ((Mono.Cecil.MethodReference)dmd.Definition.Body.Instructions.Last(static i => i.OpCode == Mono.Cecil.Cil.OpCodes.Newobj).Operand).DeclaringType;
                //Get type by assembly qualified name since it might be from mod assembly
                Type type_itemActionData = typeref_actiondata.ResolveReflection();
                ModuleManagers.LogOut($"CreateModifierData return type {type_itemActionData.AssemblyQualifiedName}");
                if (ModuleManagers.PatchType(type_itemActionData, typeof(ItemActionData), manipulator.typebd_newTarget, arr_type_data.Where(static t => t != null).ToArray(), new ItemActionDataModuleProcessor(manipulator.typebd_newTarget, manipulator.moduleTypes, manipulator.arr_fldbd_modules.Where((f, i) => arr_type_data[i] != null).ToArray(), arr_type_data.Select(static t => t != null).ToArray(), out arr_flddef_data), out typebd_newActionData))
                {
                    var ctorinf_actiondata = typebd_newActionData.GetDeclaredConstructors().FirstOrDefault();
                    var mtdbd_createdata = manipulator.typebd_newTarget.DefineMethod(nameof(ItemAction.CreateModifierData),
                                                                                     MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot | MethodAttributes.Final,
                                                                                     mtdinf_create_data.CallingConvention,
                                                                                     mtdinf_create_data.ReturnType,
                                                                                     mtdinf_create_data.ReturnParameter.GetRequiredCustomModifiers().Length > 0 ? mtdinf_create_data.ReturnParameter.GetRequiredCustomModifiers() : null,
                                                                                     mtdinf_create_data.ReturnParameter.GetOptionalCustomModifiers().Length > 0 ? mtdinf_create_data.ReturnParameter.GetOptionalCustomModifiers() : null,
                                                                                     mtdinf_create_data.GetParameters().Select(static p => p.ParameterType).ToArray(),
                                                                                     mtdinf_create_data.GetParameters().Select(static p => { var mod = p.GetRequiredCustomModifiers(); return mod != null && mod.Length > 0 ? mod : null; }).ToArray(),
                                                                                     mtdinf_create_data.GetParameters().Select(static p => { var mod = p.GetOptionalCustomModifiers(); return mod != null && mod.Length > 0 ? mod : null; }).ToArray());
                    manipulator.typebd_newTarget.DefineMethodOverride(mtdbd_createdata, mtdinf_create_data);
                    ParameterInfo[] paramInfo = mtdinf_create_data.GetParameters();
                    for (int i = 0; i < paramInfo.Length; i++)
                    {
                        mtdbd_createdata.DefineParameter(i + 1, paramInfo[i].Attributes, paramInfo[i].Name);
                    }
                    var il = mtdbd_createdata.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Newobj, ctorinf_actiondata);
                    il.Emit(OpCodes.Ret);
                }
            }
        }

        public void BuildConstructor(ModuleManipulator manipulator, ILGenerator generator)
        {
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
                var flddef_module = manipulator.arr_fldbd_modules[i];
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldfld, flddef_module);
                generator.Emit(OpCodes.Dup);
                generator.Emit(OpCodes.Ldc_I4, mask);
                generator.Emit(OpCodes.Stfld, fld_mask);
                generator.Emit(OpCodes.Ldc_I4, shift);
                generator.Emit(OpCodes.Stfld, fld_shift);
            }
        }

        public bool MatchSpecialArgs(ModuleManipulator manipulator, ILGenerator generator, ParameterInfo par, MethodPatchInfo mtdpinf_derived, MethodOverrideInfo mtdoinf_target)
        {
            switch (par.Name)
            {
                //load injected data instance
                case "__customData":
                    var flddef_data = arr_flddef_data[mtdoinf_target.moduleIndex];
                    if (flddef_data == null)
                        throw new ArgumentNullException($"No Injected ItemActionData in {mtdoinf_target.mtdinf_target.DeclaringType.FullName}! module index {mtdoinf_target.moduleIndex}");
                    int index = -1;
                    ParameterInfo[] paramInfo = mtdoinf_target.mtdinf_base.GetParameters();
                    for (int i = 0; i < paramInfo.Length; i++)
                    {
                        if (typeof(ItemActionData).IsAssignableFrom(paramInfo[i].ParameterType))
                        {
                            index = i + 1;
                            break;
                        }
                    }
                    if (index < 0)
                        throw new ArgumentException($"ItemActionData is not present in target method! Patch method: {mtdoinf_target.mtdinf_target.DeclaringType.FullName}.{mtdoinf_target.mtdinf_target.Name}");
                    generator.LoadArg(index);
                    generator.Emit(OpCodes.Castclass, typebd_newActionData);
                    generator.Emit(par.ParameterType.IsByRef ? OpCodes.Ldflda : OpCodes.Ldfld, flddef_data);
                    return true;
            }
            return false;
        }

        public bool MatchConstructorArgs(ModuleManipulator manipulator, ILGenerator generator, ParameterInfo par, ParameterBuilder[] paramInfo, Type[] paramTypes, ConstructorInfo ctorinf_target, int moduleIndex)
        {
            return false;
        }

        public bool DefineConstructorArgs(ModuleManipulator manipulator, ConstructorBuilder ctorbd, out ParameterBuilder[] pbs)
        {
            pbs = null;
            return false;
        }
    }
}
