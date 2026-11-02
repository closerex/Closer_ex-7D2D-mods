using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace KFCommonUtilityLib
{
    /// <summary>
    /// Explicit RNE adaptation of the statically recovered KFEditor UniformRange formula.
    /// Not a claim about the original game's rounding mode, initial seed or draw schedule.
    /// The bit contract requires binary32 round-to-nearest-even, gradual underflow
    /// (FTZ/DAZ off), and separate subtraction/multiplication/addition. This class does
    /// not change the calling thread's floating-point environment. Validate each backend.
    /// </summary>
    public static class KFUniformRangeSampler
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public uint Bits;
            [FieldOffset(0)] public float Value;
        }

        /// <summary>
        /// Consumes exactly four LCG draws: range selection, X, Y, then Z.
        /// First/second endpoints correspond to the two recovered native offset groups;
        /// they are neither sorted nor constrained by ordinary Uniform lock-axis rules.
        /// Finite endpoints can still overflow during subtraction; finite output is not
        /// guaranteed. No special contract is made for NaN payloads or infinite inputs.
        /// </summary>
        public static Vector3 SampleNearestEven(ref uint state,
            Vector3 firstStart, Vector3 firstEnd, Vector3 secondStart, Vector3 secondEnd)
        {
            float choice = Add(Multiply(Draw(ref state), 2f), .5f);
            int selected = (int)Math.Round((double)choice, MidpointRounding.ToEven);
            bool second = (selected & ~1) != 0;
            Vector3 start = second ? secondStart : firstStart;
            Vector3 end = second ? secondEnd : firstEnd;
            float ux = Draw(ref state);
            float uy = Draw(ref state);
            float uz = Draw(ref state);
            return new Vector3(
                Interpolate(start.x, end.x, ux),
                Interpolate(start.y, end.y, uy),
                Interpolate(start.z, end.z, uz));
        }

        private static float Draw(ref uint state)
        {
            state = unchecked(state * 0x0bb38435u + 0x3619636bu);
            var bits = new FloatBits { Bits = 0x3f800000u | (state & 0x007fffffu) };
            return Subtract(bits.Value, 1f);
        }

        private static float Interpolate(float start, float end, float fraction)
        {
            return Add(Multiply(Subtract(end, start), fraction), start);
        }

        // Separate calls avoid contraction on the tested Unity Mono backend.
        // NoInlining does not set MXCSR or guarantee other runtimes' intermediate precision.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float Subtract(float a, float b) { return a - b; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float Multiply(float a, float b) { return a * b; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float Add(float a, float b) { return a + b; }
    }
}
