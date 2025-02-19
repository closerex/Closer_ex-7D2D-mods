using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib
{
    public struct ItemClassModuleProcessor : IModuleProcessor
    {

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

        }

        public bool MatchSpecialArgs(ParameterDefinition par, MethodDefinition mtddef_target, MethodPatchInfo mtdpinf_derived, int moduleIndex, List<Instruction> list_inst_pars, ILProcessor il)
        {
            return false;
        }
    }
}
