using System;

namespace KFCommonUtilityLib.Scripts.Attributes
{
    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class TypeTargetAttribute : Attribute
    {
        // This is a positional argument
        public TypeTargetAttribute(Type baseType)
        {
            BaseType = baseType;
        }

        public Type BaseType { get; }
    }
}
