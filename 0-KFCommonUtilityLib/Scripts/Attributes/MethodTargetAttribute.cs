using System;
using HarmonyLib;

namespace KFCommonUtilityLib.Attributes
{
    public interface IMethodTarget
    {

    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MethodTargetPrefixAttribute : Attribute, IMethodTarget
    {
        public MethodTargetPrefixAttribute()
        {

        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MethodTargetPostfixAttribute : Attribute, IMethodTarget
    {
        public MethodTargetPostfixAttribute()
        {

        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MethodTargetTranspilerAttribute : Attribute, IMethodTarget
    {
        public MethodTargetTranspilerAttribute()
        {

        }
    }

    public static class IMethodTargetExtension
    {
        public static string GetTargetMethodIdentifier(this HarmonyMethod self)
        {
            return (self.methodName ?? "this[]") + (self.argumentTypes == null ? string.Empty : string.Join(",", Array.ConvertAll(self.argumentTypes, type => type.FullDescription())));
        }
    }
}
