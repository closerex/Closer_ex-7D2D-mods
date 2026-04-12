using HarmonyLib;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace KFCommonUtilityLib
{
    public interface IModuleProcessor
    {
        void InitModules(ModuleManipulator manipulator);
        bool DefineConstructorArgs(ModuleManipulator manipulator, ConstructorBuilder ctorbd, out ParameterBuilder[] pbs);
        void BuildConstructor(ModuleManipulator manipulator, ILGenerator generator);
        Type GetModuleTypeByName(string name);
        bool MatchSpecialArgs(ModuleManipulator manipulator, ILGenerator generator, ParameterInfo par, MethodPatchInfo mtdpinf_derived, MethodOverrideInfo mtdoinf_target);
        bool MatchConstructorArgs(ModuleManipulator manipulator, ILGenerator generator, ParameterInfo par, ParameterBuilder[] paramInfo, Type[] paramTypes, ConstructorInfo ctorinf_target, int moduleIndex);
    }
}
