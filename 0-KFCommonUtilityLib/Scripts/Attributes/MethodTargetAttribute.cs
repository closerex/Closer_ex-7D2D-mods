using System;
using HarmonyLib;

namespace KFCommonUtilityLib.Attributes
{
    public interface IMethodTarget
    {
        bool IsStatic { get; }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MethodTargetPrefixAttribute : Attribute, IMethodTarget
    {
        public bool IsStatic => false;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MethodTargetPostfixAttribute : Attribute, IMethodTarget
    {
        public bool IsStatic => false;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MethodTargetTranspilerAttribute : Attribute, IMethodTarget
    {
        public bool IsStatic => true;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class TargetMethodsAttribute : Attribute
    {
        
    }

    public static class IMethodTargetExtension
    {
        public static string GetTargetMethodIdentifier(this HarmonyMethod self)
        {
            return (self.methodName ?? "this[]") + (self.argumentTypes == null ? string.Empty : string.Join(",", Array.ConvertAll(self.argumentTypes, type => type.FullDescription())));
        }
    }
}
