using System;

namespace KFCommonUtilityLib.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class TypeTargetExtensionAttribute :  Attribute
    {
        public Type ModuleType { get; }
        public TypeTargetExtensionAttribute(Type moduleType)
        {
            ModuleType = moduleType;
        }
    }
}
