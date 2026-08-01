using System;

namespace KFCommonUtilityLib.Attributes
{
    public class ModuleTypeReferenceAttribute : Attribute
    {
        public Type FinalType { get; internal set; }
        public Type[] ModuleTypes { get; internal set; }
    }
}