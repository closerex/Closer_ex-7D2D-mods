using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib
{
    public static class ModuleUtils
    {
        public static string CreateFieldName(Type moduleType)
        {
            return (moduleType.FullName + "_" + moduleType.Assembly.GetName().Name).ReplaceInvalidChar();
        }

        public static string CreateFieldName(TypeReference moduleType)
        {
            return (moduleType.FullName + "_" + moduleType.Module.Assembly.Name.Name).ReplaceInvalidChar();
        }

        public static string CreateTypeName(Type itemActionType, params Type[] moduleTypes)
        {
            string typeName = itemActionType.FullName + "_" + itemActionType.Assembly.GetName().Name;
            foreach (Type type in moduleTypes)
            {
                if (type != null)
                    typeName += "__" + type.FullName + "_" + type.Assembly.GetName().Name;
            }
            typeName = typeName.ReplaceInvalidChar();
            return typeName;
        }

        public static string CreateTypeName(TypeReference itemActionType, params TypeReference[] moduleTypes)
        {
            string typeName = itemActionType.FullName + "_" + itemActionType.Module.Assembly.Name.Name;
            foreach (TypeReference type in moduleTypes)
            {
                if (type != null)
                    typeName += "__" + type.FullName + "_" + type.Module.Assembly.Name.Name;
            }
            typeName = typeName.ReplaceInvalidChar();
            return typeName;
        }

        private static string ReplaceInvalidChar(this string self)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < self.Length; i++)
            {
                char c = self[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static void MakeContainerFor(ModuleDefinition module, TypeReference typeref_interface, TypeDefinition typedef_container, Type type_module, FieldDefinition flddef_module, TypeReference typeref_module)
        {
            typedef_container.Interfaces.Add(new InterfaceImplementation(typeref_interface.MakeGenericInstanceType(typeref_module)));
            PropertyDefinition propdef_instance = new PropertyDefinition("Instance", Mono.Cecil.PropertyAttributes.None, typeref_module);
            MethodDefinition mtddef_instance_getter = new MethodDefinition("get_Instance", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final, typeref_module);
            mtddef_instance_getter.Overrides.Add(module.ImportReference(AccessTools.Method(typeof(IModuleContainerFor<>).MakeGenericType(type_module), "get_Instance")));
            typedef_container.Methods.Add(mtddef_instance_getter);
            mtddef_instance_getter.Body = new Mono.Cecil.Cil.MethodBody(mtddef_instance_getter);
            var generator = mtddef_instance_getter.Body.GetILProcessor();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, flddef_module);
            generator.Emit(OpCodes.Ret);
            propdef_instance.GetMethod = mtddef_instance_getter;
            typedef_container.Properties.Add(propdef_instance);
        }
    }
}
