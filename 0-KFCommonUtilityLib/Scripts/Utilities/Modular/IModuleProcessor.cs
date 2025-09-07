using KFCommonUtilityLib;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib
{
    public interface IModuleProcessor
    {
        void InitModules(ModuleManipulator manipulator, Type targetType, Type baseType, params Type[] moduleTypes);
        bool BuildConstructor(ModuleManipulator manipulator, MethodDefinition mtddef_ctor);
        Type GetModuleTypeByName(string name);
        bool MatchSpecialArgs(ParameterDefinition par, MethodDefinition mtddef_target, MethodPatchInfo mtdpinf_derived, int moduleIndex, List<Instruction> list_inst_pars, ILProcessor il);
    }
}
