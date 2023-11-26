using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Scripts.Attributes
{
    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class TypeTargetAttribute : Attribute
    {
        // This is a positional argument
        public TypeTargetAttribute(Type baseType, Type dataType = null)
        {
            BaseType = baseType;
            DataType = dataType;
        }

        public Type BaseType { get; }
        public Type DataType { get; }
    }
}
