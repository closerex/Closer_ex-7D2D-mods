using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using UniLinq;

namespace KFCommonUtilityLib
{
    public class ItemInventoryDataModuleProcessor : IModuleProcessor
    {
        TypeDefinition typedef_newClass;
        Type[] arr_type_classes;
        FieldDefinition[] arr_flddef_classes;
        bool[] arr_hasdata;
        FieldDefinition[] arr_flddef_invdatas;
        public ItemInventoryDataModuleProcessor(TypeDefinition typedef_newClass, Type[] arr_type_classes, FieldDefinition[] arr_flddef_classes, bool[] arr_hasdata, out FieldDefinition[] arr_flddef_invdatas)
        {
            this.typedef_newClass = typedef_newClass;
            this.arr_type_classes = arr_type_classes;
            this.arr_flddef_classes = arr_flddef_classes;
            this.arr_hasdata = arr_hasdata;
            this.arr_flddef_invdatas = new FieldDefinition[arr_type_classes.Length]; 
            arr_flddef_invdatas = this.arr_flddef_invdatas;
        }

        public bool BuildConstructor(ModuleManipulator manipulator, MethodDefinition mtddef_ctor)
        {
            mtddef_ctor.Parameters.Add(new ParameterDefinition("_item", Mono.Cecil.ParameterAttributes.None, manipulator.module.ImportReference(typeof(ItemClass))));
            mtddef_ctor.Parameters.Add(new ParameterDefinition("_itemStack", Mono.Cecil.ParameterAttributes.None, manipulator.module.ImportReference(typeof(ItemStack))));
            mtddef_ctor.Parameters.Add(new ParameterDefinition("_gameManager", Mono.Cecil.ParameterAttributes.None, manipulator.module.ImportReference(typeof(IGameManager))));
            mtddef_ctor.Parameters.Add(new ParameterDefinition("_holdingEntity", Mono.Cecil.ParameterAttributes.None, manipulator.module.ImportReference(typeof(EntityAlive))));
            mtddef_ctor.Parameters.Add(new ParameterDefinition("_slotIdx", Mono.Cecil.ParameterAttributes.None, manipulator.module.TypeSystem.Int32));
            var il = mtddef_ctor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Ldarg_S, mtddef_ctor.Parameters[3]);
            il.Emit(OpCodes.Ldarg_S, mtddef_ctor.Parameters[4]);
            il.Emit(OpCodes.Call, manipulator.module.ImportReference(manipulator.targetType.GetConstructor(new Type[] { typeof(ItemClass), typeof(ItemStack), typeof(IGameManager), typeof(EntityAlive), typeof(int) })));
            il.Emit(OpCodes.Nop);
            for (int i = 0, j = 0; i < arr_type_classes.Length; i++)
            {
                if (!arr_hasdata[i])
                {
                    arr_flddef_invdatas[i] = null;
                    continue;
                }
                arr_flddef_invdatas[i] = manipulator.arr_flddef_modules[j];
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
                            il.Create(OpCodes.Castclass, typedef_newClass),
                            il.Create(OpCodes.Ldfld, arr_flddef_classes[i]),
                        });
                            return true;
                        default:
                            return false;
                    }
                }))
                {
                    il.Append(ins);
                }
                ModuleManagers.LogOut($"data module {j} {manipulator.moduleTypes[j].FullName} class module {i} {arr_type_classes[i].FullName}");
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
