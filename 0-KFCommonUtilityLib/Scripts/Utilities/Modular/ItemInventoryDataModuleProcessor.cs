using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;

namespace KFCommonUtilityLib
{
    public class ItemInventoryDataModuleProcessor : IModuleProcessor
    {
        TypeBuilder typebd_newClass;
        Type[] arr_type_classes;
        FieldBuilder[] arr_fldbd_classes;
        bool[] arr_hasdata;
        FieldBuilder[] arr_fldbd_invdatas;
        public ItemInventoryDataModuleProcessor(TypeBuilder typebd_newClass, Type[] arr_type_classes, FieldBuilder[] arr_fldbd_classes, bool[] arr_hasdata, out FieldBuilder[] arr_fldbd_invdatas)
        {
            this.typebd_newClass = typebd_newClass;
            this.arr_type_classes = arr_type_classes;
            this.arr_fldbd_classes = arr_fldbd_classes;
            this.arr_hasdata = arr_hasdata;
            this.arr_fldbd_invdatas = new FieldBuilder[arr_type_classes.Length]; 
            arr_fldbd_invdatas = this.arr_fldbd_invdatas;
        }

        public void BuildConstructor(ModuleManipulator manipulator, ILGenerator generator)
        {

        }

        public bool DefineConstructorArgs(ModuleManipulator manipulator, ConstructorBuilder ctorbd, out ParameterBuilder[] pbs)
        {
            pbs = new ParameterBuilder[]
            {
                ctorbd.DefineParameter(1, ParameterAttributes.None, "_item"),
                ctorbd.DefineParameter(2, ParameterAttributes.None, "_itemStack"),
                ctorbd.DefineParameter(3, ParameterAttributes.None, "_gameManager"),
                ctorbd.DefineParameter(4, ParameterAttributes.None, "_holdingEntity"),
                ctorbd.DefineParameter(5, ParameterAttributes.None, "_slotIdx")
            };
            return true;
        }

        public Type GetModuleTypeByName(string name)
        {
            throw new NotImplementedException();
        }

        public void InitModules(ModuleManipulator manipulator)
        {
            for (int i = 0, j = 0; i < arr_fldbd_invdatas.Length; i++)
            {
                if (!arr_hasdata[i])
                {
                    continue;
                }
                arr_fldbd_invdatas[i] = manipulator.arr_fldbd_modules[j++];
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
                    generator.Emit(OpCodes.Castclass, typebd_newClass);
                    generator.Emit(OpCodes.Ldfld, arr_fldbd_classes[moduleIndex]);
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
