using System;
using System.Collections.Generic;
using UniLinq;

namespace KFCommonUtilityLib
{
    public static class CustomEffectEnumManager
    {
        private static event Action OnInitDefault;
        private static event Action OnInitFinal;
        private static event Action OnPrintResult;

        //call this in InitMod
        public static void RegisterEnumType<T>(bool requestMin = false, int requestedMin = 0, bool requestMax = false, int requestedMax = int.MaxValue) where T : struct, Enum
        {
            if (EnumHolder<T>.Registered)
                return;
            EnumHolder<T>.Registered = true;
            EnumHolder<T>.RequestMinMax(requestMin, requestedMin, requestMax, requestedMax);
            OnInitDefault += EnumHolder<T>.InitDefault;
            OnInitFinal += EnumHolder<T>.InitFinal;
            OnPrintResult += EnumHolder<T>.PrintResult;
        }

        //hooked to GameAwake
        public static void InitDefault(ref ModEvents.SGameAwakeData _)
        {
            OnInitDefault?.Invoke();
        }

        //patched to GameManager.StartGame prefix
        public static void InitFinal()
        {
            OnInitFinal?.Invoke();
        }

        public static void PrintResults()
        {
            OnPrintResult?.Invoke();
        }

        //only call these from callbacks hooked to ModEvents.GameStartDone and cache the results for future usage
        //patched to PassiveEffect.ParsePassiveEffect and MinEventActionBase.ParseXmlAttribute
        public static T RegisterOrGetEnum<T>(string name, bool ignoreCase = false) where T : struct, Enum
        {
            if (!EnumHolder<T>.Registered)
                throw new Exception($"Enum not registered: {typeof(T).Name}");
            return EnumHolder<T>.RegisterOrGetEnum(name, ignoreCase);
        }

        public static T GetEnumOrThrow<T>(string name, bool ignoreCase = false) where T : struct, Enum
        {
            if (!EnumHolder<T>.Registered)
                throw new Exception($"Enum not registered: {typeof(T).Name}");
            return EnumHolder<T>.GetEnumOrThrow(name, ignoreCase);
        }

        //public static PassiveEffects RegisterOrGetPassive(string passive)
        //{
        //    if (!dict_final_passive.TryGetValue(passive, out var value))
        //    {
        //        if (dict_final_passive.Count >= byte.MaxValue)
        //            throw new OverflowException("Passive effect count exceeds limit 255!");
        //        value = (PassiveEffects)(byte)dict_final_passive.Count;
        //        dict_final_passive.Add(passive, value);
        //    }
        //    return value;
        //}

        ////patched to MinEventActionBase.ParseXmlAttribute
        //public static MinEventTypes RegisterOrGetTrigger(string trigger)
        //{
        //    if (!dict_final_trigger.TryGetValue(trigger, out var value))
        //    {
        //        value = (MinEventTypes)dict_final_trigger.Count;
        //        dict_final_trigger.Add(trigger, value);
        //    }
        //    return value;
        //}

        private static class EnumHolder<T> where T : struct, Enum
        {
            private static int max, min;
            private static readonly TypeCode typecode;
            private static readonly Dictionary<string, T> dict_default_enums = new Dictionary<string, T>();
            private static readonly Dictionary<string, T> dict_default_enums_lower = new Dictionary<string, T>();
            private static readonly LinkedList<(int start, int end)> link_default_holes = new LinkedList<(int start, int end)>();
            private static Dictionary<string, T> dict_final_enums = new Dictionary<string, T>();
            private static Dictionary<string, T> dict_final_enums_lower = new Dictionary<string, T>();
            private static LinkedList<(int start, int end)> link_final_holes = new LinkedList<(int start, int end)>();
            public static bool Registered { get; set; } = false;
            private static bool DefaultInited { get; set; } = false;
            static EnumHolder()
            {
                Type underlying = Enum.GetUnderlyingType(typeof(T));
                typecode = Type.GetTypeCode(underlying);
                switch (typecode)
                {
                    case TypeCode.Byte:
                        min = 0;
                        max = byte.MaxValue;
                        break;
                    case TypeCode.SByte:
                        min = sbyte.MinValue;
                        max = sbyte.MaxValue;
                        break;
                    case TypeCode.Int16:
                        min = short.MinValue;
                        max = short.MaxValue;
                        break;
                    case TypeCode.UInt16:
                        min = 0;
                        max = ushort.MaxValue;
                        break;
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                        min = int.MinValue;
                        max = int.MaxValue;
                        break;
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                        min = 0;
                        max = int.MaxValue;
                        break;
                    default:
                        throw new Exception($"Invalid underlying type for enum {typeof(T).Name}");
                }
            }

            public static void RequestMinMax(bool requestMin, int requestedMin, bool requestMax, int requestedMax)
            {
                if (requestMin && requestedMin >= min)
                    min = requestedMin;
                if (requestMax && requestedMax <= max)
                    max = requestedMax;
            }

            public static void InitDefault()
            {
                if (DefaultInited)
                    return;
                dict_default_enums.Clear();
                dict_default_enums_lower.Clear();
                link_default_holes.Clear();
                var total = Enum.GetNames(typeof(T)).Length;
                var enums = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
                for (int i = 0; i < total; i++)
                {
                    string name = enums[i].ToString();
                    dict_default_enums.Add(name, enums[i]);
                    dict_default_enums_lower.Add(name.ToLower(), enums[i]);
                }
                var values = enums.Select(e => Convert.ToInt32(e)).OrderBy(i => i).ToArray();
                int nextHole = min;
                foreach (var value in values)
                    if (nextHole < value)
                    {
                        link_default_holes.AddLast((nextHole, Math.Min(value - 1, max)));
                        if (value >= max)
                        {
                            nextHole = max;
                            break;
                        }
                        nextHole = value + 1;
                    }
                    else if (nextHole == value)
                    {
                        if (value >= max)
                            break;
                        nextHole++;
                    }
                if (nextHole <= max && values[values.Length - 1] < max)
                    link_default_holes.AddLast((nextHole, max));
                DefaultInited = true;
            }

            public static void InitFinal()
            {
                dict_final_enums = new Dictionary<string, T>(dict_default_enums);
                dict_final_enums_lower = new Dictionary<string, T>(dict_default_enums_lower);
                link_final_holes = new LinkedList<(int start, int end)>(link_default_holes);
            }

            public static void PrintResult()
            {
                //Log.Out($"{typeof(T).Name}:\n" + string.Join("\n", dict_final_enums.Select(p => $"name: {p.Key} value: {p.Value}")));
            }

            public static T RegisterOrGetEnum(string name, bool ignoreCase = false)
            {
                if (!(ignoreCase ? dict_final_enums_lower : dict_final_enums).TryGetValue(ignoreCase ? name.ToLower() : name, out var value))
                {
                    if (link_final_holes.Count == 0)
                        throw new OverflowException($"Enum count exceeds limit {max}!");
                    (int start, int end) = link_final_holes.First.Value;
                    link_final_holes.RemoveFirst();
                    value = (T)Enum.ToObject(typeof(T), Convert.ChangeType(start, typecode));
                    dict_final_enums.Add(name, value);
                    dict_final_enums_lower.Add(name.ToLower(), value);
                    if (start < end)
                    {
                        start++;
                        link_final_holes.AddFirst((start, end));
                    }
                }
                return value;
            }

            public static T GetEnumOrThrow(string name, bool ignoreCase = false)
            {
                if ((ignoreCase ? dict_final_enums_lower : dict_final_enums).TryGetValue(ignoreCase ? name.ToLower() : name, out var value))
                    return value;
                throw new Exception($"Enum not registered: {name} type: {typeof(T).ToString()}");
            }
        }
    }
}
