using System;

namespace KFCommonUtilityLib.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class RequireUserDataBits : Attribute
    {
        public RequireUserDataBits(string maskField, string shiftField, byte bits)
        {
            MaskField = maskField;
            ShiftField = shiftField;
            Bits = bits;
        }

        public string MaskField { get; }
        public string ShiftField { get; }
        public byte Bits { get; }

        public static int ExtractUserDataBits(ref int userData, int mask, byte shift)
        {
            int extracted = (int)((uint)(userData & mask) >> shift);
            userData &= ~mask;
            return extracted;
        }

        public static int InjectUserDataBits(int userData, int bitsToInject, byte shift)
        {
            return userData |= bitsToInject << shift;
        }
    }
}
