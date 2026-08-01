using HarmonyLib;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace KFCommonUtilityLib
{
    public static class ModuleUtils
    {
        public static string CreateFieldName(Type moduleType)
        {
            return (moduleType.FullName + "_" + moduleType.Assembly.GetName().Name).ReplaceInvalidChar();
        }

        public static string CreateTypeName(Type baseType, params Type[] moduleTypes)
        {
            string typeName = baseType.FullName + "_" + baseType.Assembly.GetName().Name;
            string moduleName = moduleTypes.Where(static type => type != null)
                                           .Select(static type => type.FullName + "_" + type.Assembly.GetName().Name)
                                           .Join(static s => s, "__");
            typeName += "__" + ComputeHash(moduleName);
            typeName = typeName.ReplaceInvalidChar();
            return typeName;
        }

        public static void CopyParamInfoTo(this MethodBase from, DynamicMethodDefinition to)
        {
            var paramInfo = from.GetParameters();
            int offset = from.IsStatic ? 0 : 1;
            if (!from.IsStatic)
            {
                to.Definition.Parameters[0].Name = "_self";
            }
            for (int i = 0; i < paramInfo.Length; i++)
            {
                var index = i + offset;
                to.Definition.Parameters[index].Attributes = (Mono.Cecil.ParameterAttributes)paramInfo[i].Attributes;
                to.Definition.Parameters[index].Name = paramInfo[i].Name;
                if (paramInfo[i].HasDefaultValue)
                {
                    to.Definition.Parameters[index].Constant = paramInfo[i].DefaultValue;
                }
            }
        }

        public static ParameterBuilder[] CopyParamInfoTo(this MethodBase from, MethodBuilder to)
        {
            var paramInfo = from.GetParameters();
            ParameterBuilder[] res = new ParameterBuilder[paramInfo.Length];
            for (int i = 0; i < paramInfo.Length; i++)
            {
                res[i] = to.DefineParameter(i + 1, paramInfo[i].Attributes, paramInfo[i].Name);
                if (paramInfo[i].HasDefaultValue)
                {
                    res[i].SetConstant(paramInfo[i].DefaultValue);
                }
            }
            return res;
        }

        public static ParameterBuilder[] CopyParamInfoTo(this MethodBase from, ConstructorBuilder to)
        {
            var paramInfo = from.GetParameters();
            ParameterBuilder[] res = new ParameterBuilder[paramInfo.Length];
            for (int i = 0; i < paramInfo.Length; i++)
            {
                res[i] = to.DefineParameter(i + 1, paramInfo[i].Attributes, paramInfo[i].Name);
                if (paramInfo[i].HasDefaultValue)
                {
                    res[i].SetConstant(paramInfo[i].DefaultValue);
                }
            }
            return res;
        }

        private static string ReplaceInvalidChar(this string self)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var c in self)
            {
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

        private static string ComputeHash(string input)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hash = md5.ComputeHash(bytes);

            return hash.ToHexString();
        }

        public static HarmonyMethod CloneFix(this HarmonyMethod hm, Type declaringType = null)
        {
            var res = hm.Clone();
            res.methodType ??= MethodType.Normal;
            res.declaringType ??= declaringType;
            return res;
        }
        
        // private members from parent type is not returned by GetMethods
        public static IEnumerable<MethodInfo> GetAllMethods(this Type type, BindingFlags flags)
        {
            IEnumerable<MethodInfo> res = type.GetMethods(flags);
            flags = flags ^ BindingFlags.Public | BindingFlags.NonPublic;
            var workingType = type.BaseType;
            while (!workingType.IsAssignableFrom(typeof(object)))
            {
                res = res.Concat(workingType.GetMethods(flags));
                workingType = workingType.BaseType;
            }
            return res;
        }
    }
}
