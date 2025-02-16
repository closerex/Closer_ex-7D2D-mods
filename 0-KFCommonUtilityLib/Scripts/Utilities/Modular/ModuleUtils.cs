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
    }
}
