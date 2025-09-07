using System;

namespace KFCommonUtilityLib.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
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
