using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Scripts.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MethodTargetPrefixAttribute : Attribute
    {
        public MethodTargetPrefixAttribute(string targetMethod, Type preferredType = null, Type[] @params = null)
        {
            TargetMethod = targetMethod;
            PreferredType = preferredType;
            Params = @params;
        }

        public string GetTargetMethodIdentifier()
        {
            return TargetMethod + (Params == null ? string.Empty : string.Join("_", Array.ConvertAll(Params, type => type.FullName)));
        }

        public string TargetMethod { get; }
        public Type PreferredType { get; }
        public Type[] Params { get; }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MethodTargetPostfixAttribute : Attribute
    {
        public MethodTargetPostfixAttribute(string targetMethod, Type preferredType = null, Type[] @params = null)
        {
            TargetMethod = targetMethod;
            PreferredType = preferredType;
            Params = @params;
        }

        public string TargetMethod { get; }
        public Type PreferredType { get; }
        public Type[] Params { get; }
    }
}
