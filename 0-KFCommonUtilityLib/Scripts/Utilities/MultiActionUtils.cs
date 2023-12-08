using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Scripts.Utilities
{
    public static class MultiActionUtils
    {

        public static FastTags ActionIndexToTag(int index)
        {
            switch (index)
            {
                case 0:
                    return FastTags.Parse("primary");
                case 1:
                    return FastTags.Parse("secondary");
                case 2:
                    return FastTags.Parse("tertiary");
                case 3:
                    return FastTags.Parse("quaternary");
                case 4:
                    return FastTags.Parse("quinary");
                default:
                    throw new IndexOutOfRangeException("ItemAction count is limited to 5!");
            }
        }
    }
}
