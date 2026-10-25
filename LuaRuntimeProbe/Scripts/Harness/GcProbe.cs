using System;
using System.Reflection;

namespace LuaRuntimeProbe.Harness
{
    /// <summary>
    /// Access to GC allocation counters.
    ///
    /// GC.GetTotalAllocatedBytes only exists on newer runtimes, so it is looked
    /// up by reflection.  When it is missing the value is reported as
    /// "unavailable" (-1) and never as 0, because 0 would be a false claim of a
    /// measurement that was never taken.
    /// </summary>
    internal static class GcProbe
    {
        private static readonly Func<long> Accessor = CreateAccessor();

        public static bool Available
        {
            get { return Accessor != null; }
        }

        public static long TotalAllocated()
        {
            if (Accessor == null)
            {
                return -1;
            }

            try
            {
                return Accessor();
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static Func<long> CreateAccessor()
        {
            try
            {
                MethodInfo precise = typeof(GC).GetMethod(
                    "GetTotalAllocatedBytes", new[] { typeof(bool) });
                if (precise != null)
                {
                    return () => (long)precise.Invoke(null, new object[] { false });
                }

                MethodInfo simple = typeof(GC).GetMethod("GetTotalAllocatedBytes", Type.EmptyTypes);
                if (simple != null)
                {
                    return () => (long)simple.Invoke(null, null);
                }
            }
            catch (Exception)
            {
                // Fall through to "unavailable".
            }

            return null;
        }
    }
}
