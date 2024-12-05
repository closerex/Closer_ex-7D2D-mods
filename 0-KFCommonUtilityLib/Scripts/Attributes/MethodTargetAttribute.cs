using System;

namespace KFCommonUtilityLib.Scripts.Attributes
{
    public interface IMethodTarget
    {
        string TargetMethod { get; }
        Type PreferredType { get; }
        Type[] Params { get; }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MethodTargetPrefixAttribute : Attribute, IMethodTarget
    {
        public MethodTargetPrefixAttribute(string targetMethod, Type preferredType = null, Type[] @params = null)
        {
            TargetMethod = targetMethod;
            PreferredType = preferredType;
            Params = @params;
        }

        public string TargetMethod { get; }
        public Type PreferredType { get; }
        public Type[] Params { get; }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MethodTargetPostfixAttribute : Attribute, IMethodTarget
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

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MethodTargetTranspilerAttribute : Attribute, IMethodTarget
    {
        public MethodTargetTranspilerAttribute(string targetMethod, Type preferredType = null, Type[] @params = null)
        {
            TargetMethod = targetMethod;
            PreferredType = preferredType;
            Params = @params;
        }

        public string TargetMethod { get; }
        public Type PreferredType { get; }
        public Type[] Params { get; }
    }

    public static class IMethodTargetExtension
    {
        public static string GetTargetMethodIdentifier(this IMethodTarget self)
        {
            return self.TargetMethod + (self.Params == null ? string.Empty : string.Join("_", Array.ConvertAll(self.Params, type => type.FullName)));
        }
    }
}
