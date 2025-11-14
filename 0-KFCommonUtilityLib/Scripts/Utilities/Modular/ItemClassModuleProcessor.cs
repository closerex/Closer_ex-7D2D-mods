using KFCommonUtilityLib.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace KFCommonUtilityLib
{
    public class ItemClassModuleProcessor : IModuleProcessor
    {
        TypeDefinition typedef_newInvData;
        FieldDefinition[] arr_flddef_data;

        public Type GetModuleTypeByName(string name)
        {
            return ReflectionHelpers.GetTypeWithPrefix("ItemModule", name);
        }
        public bool BuildConstructor(ModuleManipulator manipulator, MethodDefinition mtddef_ctor)
        {
            return false;
        }

        public void InitModules(ModuleManipulator manipulator, Type targetType, Type baseType, params Type[] moduleTypes)
        {
            ModuleDefinition module = manipulator.module;
            //Find ItemInventoryData subtype
            MethodInfo mtdinf_create_invdata = null;
            {
                Type type_itemClassBase = targetType;
                while (baseType.IsAssignableFrom(type_itemClassBase))
                {
                    mtdinf_create_invdata = type_itemClassBase.GetMethod(nameof(ItemClass.createItemInventoryData), BindingFlags.Public | BindingFlags.Instance);
                    if (mtdinf_create_invdata != null)
                        break;
                    mtdinf_create_invdata = mtdinf_create_invdata.GetBaseDefinition();
                }
            }

            //CLASS MODULE DATA TYPES
            var arr_type_data = moduleTypes.Select(static m => m.GetCustomAttribute<TypeDataTargetAttribute>()?.DataType).ToArray();
            //Find createItemInventoryData
            MethodDefinition mtddef_create_invdata = module.ImportReference(mtdinf_create_invdata).Resolve();
            //ItemInventoryData subtype is the return type of createItemInventoryData
            TypeReference typeref_invdata = ((MethodReference)mtddef_create_invdata.Body.Instructions[mtddef_create_invdata.Body.Instructions.Count - 2].Operand).DeclaringType;
            //Get type by assembly qualified name since it might be from mod assembly
            Type type_itemInvData = Type.GetType(Assembly.CreateQualifiedName(typeref_invdata.Module.Assembly.Name.Name, typeref_invdata.FullName));
            MethodReference mtdref_data_ctor;
            if (ModuleManagers.PatchType(type_itemInvData, typeof(ItemInventoryData), arr_type_data.Where(static t => t != null).ToArray(), new ItemInventoryDataModuleProcessor(manipulator.typedef_newTarget, moduleTypes, manipulator.arr_flddef_modules, arr_type_data.Select(static t => t != null).ToArray(), out arr_flddef_data), out var str_data_type_name) && ModuleManagers.TryFindInCur(str_data_type_name, out typedef_newInvData))
            {
                module.Types.Remove(typedef_newInvData);
                manipulator.typedef_newTarget.NestedTypes.Add(typedef_newInvData);
                mtdref_data_ctor = typedef_newInvData.GetConstructors().FirstOrDefault();
            }
            else
            {
                mtdref_data_ctor = module.ImportReference(type_itemInvData.GetConstructor(new Type[] { typeof(ItemClass), typeof(ItemStack), typeof(IGameManager), typeof(EntityAlive), typeof(int) }));
            }

            //Create ItemClass.createItemInventoryData override
            MethodDefinition mtddef_create_inventory_data = new MethodDefinition(nameof(ItemClass.createItemInventoryData), MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot, module.ImportReference(typeof(ItemInventoryData)));
            mtddef_create_inventory_data.Parameters.Add(new ParameterDefinition("_itemStack", Mono.Cecil.ParameterAttributes.None, manipulator.module.ImportReference(typeof(ItemStack))));
            mtddef_create_inventory_data.Parameters.Add(new ParameterDefinition("_gameManager", Mono.Cecil.ParameterAttributes.None, manipulator.module.ImportReference(typeof(IGameManager))));
            mtddef_create_inventory_data.Parameters.Add(new ParameterDefinition("_holdingEntity", Mono.Cecil.ParameterAttributes.None, manipulator.module.ImportReference(typeof(EntityAlive))));
            mtddef_create_inventory_data.Parameters.Add(new ParameterDefinition("_slotIdx", Mono.Cecil.ParameterAttributes.None, manipulator.module.TypeSystem.Int32));
            var il = mtddef_create_inventory_data.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Ldarg_S, mtddef_create_inventory_data.Parameters[3]);
            il.Emit(OpCodes.Newobj, mtdref_data_ctor);
            il.Emit(OpCodes.Ret);
            manipulator.typedef_newTarget.Methods.Add(mtddef_create_inventory_data);
        }

        public bool MatchSpecialArgs(ParameterDefinition par, MethodDefinition mtddef_target, MethodPatchInfo mtdpinf_derived, int moduleIndex, List<Instruction> list_inst_pars, ILProcessor il)
        {
            switch (par.Name)
            {
                //load injected data instance
                case "__customData":
                    var flddef_data = arr_flddef_data[moduleIndex];
                    if (flddef_data == null)
                        throw new ArgumentNullException($"No Injected ItemInventoryData in {mtddef_target.DeclaringType.FullName}! module index {moduleIndex}");
                    int index = -1;
                    for (int j = 0; j < mtdpinf_derived.Method.Parameters.Count; j++)
                    {
                        if (mtdpinf_derived.Method.Parameters[j].ParameterType.Name == "ItemInventoryData")
                        {
                            index = j;
                            break;
                        }
                    }
                    if (index < 0)
                        throw new ArgumentException($"ItemInventoryData is not present in target method! Patch method: {mtddef_target.DeclaringType.FullName}.{mtddef_target.Name}");
                    list_inst_pars.Add(MonoCecilExtensions.LoadArgAtIndex(index, false, false, mtdpinf_derived.Method.Parameters, il));
                    list_inst_pars.Add(il.Create(OpCodes.Castclass, typedef_newInvData));
                    list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, flddef_data));
                    return true;
            }
            return false;
        }
    }
}
