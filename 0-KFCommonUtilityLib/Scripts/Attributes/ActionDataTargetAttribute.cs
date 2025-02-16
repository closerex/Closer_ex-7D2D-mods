using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Scripts.Attributes
{
    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ActionDataTargetAttribute : Attribute
    {
        public ActionDataTargetAttribute(Type DataType)
        {
            this.DataType = DataType;
        }
        public Type DataType { get; }
    }
}
