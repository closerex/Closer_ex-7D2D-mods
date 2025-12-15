using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using UniLinq;

namespace KFCommonUtilityLib
{
    public class ItemActionDataModuleProcessor : IModuleProcessor
    {
        TypeDefinition typedef_newAction;
        Type[] arr_type_actions;
        FieldDefinition[] arr_flddef_actions;
        bool[] arr_hasdata;
        FieldDefinition[] arr_flddef_actiondatas;
        public ItemActionDataModuleProcessor(TypeDefinition typedef_newAction, Type[] arr_type_actions, FieldDefinition[] arr_flddef_actions, bool[] arr_hasdata, out FieldDefinition[] arr_flddef_actiondatas)
        {
            this.typedef_newAction = typedef_newAction;
            this.arr_type_actions = arr_type_actions;
            this.arr_flddef_actions = arr_flddef_actions;
            this.arr_hasdata = arr_hasdata;
            this.arr_flddef_actiondatas = new FieldDefinition[arr_type_actions.Length]; 
            arr_flddef_actiondatas = this.arr_flddef_actiondatas;
        }

        public bool BuildConstructor(ModuleManipulator manipulator, MethodDefinition mtddef_ctor)
        {
            mtddef_ctor.Parameters.Add(new ParameterDefinition("_inventoryData", Mono.Cecil.ParameterAttributes.None, manipulator.module.ImportReference(typeof(ItemInventoryData))));
            mtddef_ctor.Parameters.Add(new ParameterDefinition("_indexInEntityOfAction", Mono.Cecil.ParameterAttributes.None, manipulator.module.TypeSystem.Int32));
            FieldReference fldref_invdata_item = manipulator.module.ImportReference(typeof(ItemInventoryData).GetField(nameof(ItemInventoryData.item)));
            FieldReference fldref_item_actions = manipulator.module.ImportReference(typeof(ItemClass).GetField(nameof(ItemClass.Actions)));
            var il = mtddef_ctor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, manipulator.module.ImportReference(manipulator.targetType.GetConstructor(new Type[] { typeof(ItemInventoryData), typeof(int) })));
            il.Emit(OpCodes.Nop);
            for (int i = 0, j = 0; i < arr_type_actions.Length; i++)
            {
                if (!arr_hasdata[i])
                {
                    arr_flddef_actiondatas[i] = null;
                    continue;
                }
                arr_flddef_actiondatas[i] = manipulator.arr_flddef_modules[j];
                ModuleManagers.LogOut($"data module {j} {manipulator.moduleTypes[j].FullName} action module {i} {arr_type_actions[i].FullName}");
                foreach (var ins in manipulator.MatchConstructorArguments(mtddef_ctor, manipulator.module.ImportReference(manipulator.moduleTypes[j].GetConstructors().FirstOrDefault()).Resolve(), j, (par, mtddef_original, mtddef_target, moduleIndex, list_special_args) =>
                {
                    switch (par.Name)
                    {
                        case "__instance":
                            list_special_args.Add(il.Create(OpCodes.Ldarg_0));
                            return true;
                        case "__customModule":
                            list_special_args.AddRange(new[]
                            {
                                il.Create(OpCodes.Ldarg_1),
                                il.Create(OpCodes.Ldfld, fldref_invdata_item),
                                il.Create(OpCodes.Ldfld, fldref_item_actions),
                                il.Create(OpCodes.Ldarg_2),
                                il.Create(OpCodes.Ldelem_Ref),
                                il.Create(OpCodes.Castclass, typedef_newAction),
                                il.Create(OpCodes.Ldfld, arr_flddef_actions[i]),
                            });
                            return true;
                        default:
                            return false;
                    }
                }))
                {
                    il.Append(ins);
                }
                //il.Emit(OpCodes.Ldarg_0);
                //il.Emit(OpCodes.Dup);
                //il.Emit(OpCodes.Ldarg_1);
                //il.Emit(OpCodes.Ldarg_2);
                //il.Emit(OpCodes.Ldarg_1);
                //il.Emit(OpCodes.Ldfld, fldref_invdata_item);
                //il.Emit(OpCodes.Ldfld, fldref_item_actions);
                //il.Emit(OpCodes.Ldarg_2);
                //il.Emit(OpCodes.Ldelem_Ref);
                //il.Emit(OpCodes.Castclass, typedef_newAction);
                //il.Emit(OpCodes.Ldfld, arr_flddef_actions[i]);
                //il.Emit(OpCodes.Newobj, manipulator.module.ImportReference(manipulator.moduleTypes[j].GetConstructor(new Type[] { typeof(ItemActionData), typeof(ItemInventoryData), typeof(int), arr_type_actions[i] })));
                //il.Emit(OpCodes.Stfld, manipulator.arr_flddef_modules[j]);
                //il.Emit(OpCodes.Nop);
                j++;
            }
            il.Emit(OpCodes.Ret);
            return true;
        }

        public Type GetModuleTypeByName(string name)
        {
            throw new NotImplementedException();
        }

        public void InitModules(ModuleManipulator manipulator, Type targetType, Type baseType, params Type[] moduleTypes)
        {

        }

        public bool MatchSpecialArgs(ParameterDefinition par, MethodDefinition mtddef_target, MethodPatchInfo mtdpinf_derived, int moduleIndex, List<Instruction> list_inst_pars, ILProcessor il)
        {
            return false;
        }
    }
}
