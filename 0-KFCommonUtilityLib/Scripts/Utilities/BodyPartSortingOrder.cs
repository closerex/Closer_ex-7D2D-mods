using System.Collections.Generic;

namespace KFCommonUtilityLib
{
    public unsafe struct BodyPartSortingOrder : IComparer<EnumBodyPartHit>
    {
        public const int MAX_BODYPART_COUNT = 8;
        public EnumBodyPartHit mask;
        public fixed byte orders[MAX_BODYPART_COUNT];

        public static int BodyPartToOrderIndex(EnumBodyPartHit part)
        {
            if (part.HasFlag(EnumBodyPartHit.Head))
            {
                return 0;
            }
            if (part.HasFlag(EnumBodyPartHit.Torso))
            {
                return 1;
            }
            if (part.IsArm())
            {
                return 2;
            }
            if (part.IsLeg())
            {
                return 3;
            }
            if (part.HasFlag(EnumBodyPartHit.Special))
            {
                return 4;
            }
            return -1;
        }

        public readonly int Compare(EnumBodyPartHit x, EnumBodyPartHit y)
        {
            int orderx = BodyPartSortingOrder.BodyPartToOrderIndex(x), ordery = BodyPartSortingOrder.BodyPartToOrderIndex(y);
            if (orderx == ordery || orderx < 0 && ordery < 0)
            {
                return 0;
            }
            if (orderx > 0 && ordery < 0)
            {
                return -1;
            }
            if (orderx < 0 && ordery > 0)
            {
                return 1;
            }
            unsafe
            {
                int res = orders[orderx] - orders[ordery];
                if (res == 0)
                {
                    return orderx - ordery;
                }
                return res;
            }
        }
    }
}