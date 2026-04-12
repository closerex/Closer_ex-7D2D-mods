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
    public class ItemClassModuleProcessor : IModuleProcessor
    {
        Type type_newInvData;
        FieldBuilder[] arr_fldbd_data;

        public Type GetModuleTypeByName(string name)
        {
            return ReflectionHelpers.GetTypeWithPrefix("ItemModule", name);
        }

        public void InitModules(ModuleManipulator manipulator)
        {
            //Find ItemInventoryData subtype
            MethodInfo mtdinf_create_invdata = null;
            {
                Type type_itemClassBase = manipulator.targetType;
                while (manipulator.baseType.IsAssignableFrom(type_itemClassBase))
                {
                    mtdinf_create_invdata = type_itemClassBase.GetMethod(nameof(ItemClass.createItemInventoryData), BindingFlags.Public | BindingFlags.Instance);
                    if (mtdinf_create_invdata != null)
                        break;
                    type_itemClassBase= type_itemClassBase.BaseType;
                }
            }

            //CLASS MODULE DATA TYPES
            var arr_type_data = manipulator.moduleTypes.Select(static m => m.GetCustomAttribute<TypeDataTargetAttribute>()?.DataType).ToArray();
            using (var dmd = new DynamicMethodDefinition(mtdinf_create_invdata))
            {
                //Find createItemInventoryData
                //ItemInventoryData subtype is the return type of createItemInventoryData
                var typeref_invdata = ((Mono.Cecil.MethodReference)dmd.Definition.Body.Instructions.Last(static i => i.OpCode == Mono.Cecil.Cil.OpCodes.Newobj).Operand).DeclaringType;
                //Get type by assembly qualified name since it might be from mod assembly
                Type type_itemInvData = typeref_invdata.ResolveReflection();
                ModuleManagers.LogOut($"createItemInventoryData return type {type_itemInvData.AssemblyQualifiedName}");
                if (ModuleManagers.PatchType(type_itemInvData, typeof(ItemInventoryData), manipulator.typebd_newTarget, arr_type_data.Where(static t => t != null).ToArray(), new ItemInventoryDataModuleProcessor(manipulator.typebd_newTarget, manipulator.moduleTypes, manipulator.arr_fldbd_modules.Where((f, i) => arr_type_data[i] != null).ToArray(), arr_type_data.Select(static t => t != null).ToArray(), out arr_fldbd_data), out type_newInvData))
                {
                    //Create ItemClass.createItemInventoryData override
                    var mtdref_data_ctor = type_newInvData.GetDeclaredConstructors().FirstOrDefault();
                    var mtdbd_createdata = manipulator.typebd_newTarget.DefineMethod(nameof(ItemClass.createItemInventoryData),
                                                                                     MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot | MethodAttributes.Final,
                                                                                     mtdinf_create_invdata.CallingConvention,
                                                                                     mtdinf_create_invdata.ReturnType,
                                                                                     mtdinf_create_invdata.ReturnParameter.GetRequiredCustomModifiers().Length > 0 ? mtdinf_create_invdata.ReturnParameter.GetRequiredCustomModifiers() : null,
                                                                                     mtdinf_create_invdata.ReturnParameter.GetOptionalCustomModifiers().Length > 0 ? mtdinf_create_invdata.ReturnParameter.GetOptionalCustomModifiers() : null,
                                                                                     mtdinf_create_invdata.GetParameters().Select(static p => p.ParameterType).ToArray(),
                                                                                     mtdinf_create_invdata.GetParameters().Select(static p => { var mod = p.GetRequiredCustomModifiers(); return mod != null && mod.Length > 0 ? mod : null; }).ToArray(),
                                                                                     mtdinf_create_invdata.GetParameters().Select(static p => { var mod = p.GetOptionalCustomModifiers(); return mod != null && mod.Length > 0 ? mod : null; }).ToArray());
                    manipulator.typebd_newTarget.DefineMethodOverride(mtdbd_createdata, mtdinf_create_invdata);

                    ParameterInfo[] paramInfo = mtdinf_create_invdata.GetParameters();
                    for (int i = 0; i < paramInfo.Length; i++)
                    {
                        mtdbd_createdata.DefineParameter(i + 1, paramInfo[i].Attributes, paramInfo[i].Name);
                    }
                    var il = mtdbd_createdata.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldarg_3);
                    il.Emit(OpCodes.Ldarg_S, 4);
                    il.Emit(OpCodes.Newobj, mtdref_data_ctor);
                    il.Emit(OpCodes.Ret);
                }
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

        public bool DefineConstructorArgs(ModuleManipulator manipulator, ConstructorBuilder ctorbd, out ParameterBuilder[] pbs)
        {
            pbs = null;
            return false;
        }
    }
}
