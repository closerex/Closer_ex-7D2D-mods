using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;

namespace KFCommonUtilityLib
{
    public class ItemActionDataModuleProcessor : IModuleProcessor
    {
        TypeBuilder typebd_newAction;
        Type[] arr_type_actions;
        FieldBuilder[] arr_fldbd_actions;
        bool[] arr_hasdata;
        FieldBuilder[] arr_fldbd_actiondatas;
        public ItemActionDataModuleProcessor(TypeBuilder typebd_newAction, Type[] arr_type_actions, FieldBuilder[] arr_fldbd_actions, bool[] arr_hasdata, out FieldBuilder[] arr_fldbd_actiondatas)
        {
            this.typebd_newAction = typebd_newAction;
            this.arr_type_actions = arr_type_actions;
            this.arr_fldbd_actions = arr_fldbd_actions;
            this.arr_hasdata = arr_hasdata;
            this.arr_fldbd_actiondatas = new FieldBuilder[arr_type_actions.Length]; 
            arr_fldbd_actiondatas = this.arr_fldbd_actiondatas;
        }

        public void BuildConstructor(ModuleManipulator manipulator, ILGenerator generator)
        {

        }

        public bool DefineConstructorArgs(ModuleManipulator manipulator, ConstructorBuilder ctorbd, out ParameterBuilder[] paramInfo)
        {
            paramInfo = new[]
            {
                ctorbd.DefineParameter(1, ParameterAttributes.None, "_inventoryData"),
                ctorbd.DefineParameter(2, ParameterAttributes.None, "_indexInEntityOfAction")
            };

            return true;
        }

        public Type GetModuleTypeByName(string name)
        {
            throw new NotImplementedException();
        }

        public void InitModules(ModuleManipulator manipulator)
        {
            for (int i = 0, j = 0; i < arr_fldbd_actiondatas.Length; i++)
            {
                if (!arr_hasdata[i])
                {
                    continue;
                }
                arr_fldbd_actiondatas[i] = manipulator.arr_fldbd_modules[j++];
            }
        }

        public bool MatchConstructorArgs(ModuleManipulator manipulator, ILGenerator generator, ParameterInfo par, ParameterBuilder[] paramInfo, Type[] paramTypes, ConstructorInfo ctorinf_target, int moduleIndex)
        {
            switch (par.Name)
            {
                case "__instance":
                    generator.Emit(OpCodes.Ldarg_0);
                    return true;
                case "__customModule":
                    generator.Emit(OpCodes.Ldarg_1);
                    generator.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(ItemInventoryData), nameof(ItemInventoryData.item)));
                    generator.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions)));
                    generator.Emit(OpCodes.Ldarg_2);
                    generator.Emit(OpCodes.Ldelem_Ref);
                    generator.Emit(OpCodes.Castclass, typebd_newAction);
                    generator.Emit(OpCodes.Ldfld, arr_fldbd_actions[moduleIndex]);
                    return true;
                default:
                    return false;
            }
        }

        public bool MatchSpecialArgs(ModuleManipulator manipulator, ILGenerator generator, ParameterInfo par, MethodPatchInfo mtdpinf_derived, MethodOverrideInfo mtdoinf_target)
        {
            return false;
        }
    }
}
