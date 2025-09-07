using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class TypeDataTargetAttribute : Attribute
    {
        public TypeDataTargetAttribute(Type DataType)
        {
            this.DataType = DataType;
        }
        public Type DataType { get; }
    }
}
