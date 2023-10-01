using System;
using System.Collections.Generic;
using UniLinq;

namespace KFCommonUtilityLib.Scripts.Singletons
{
    public static class CustomEffectEnumManager
    {
        private static readonly Dictionary<string, PassiveEffects> dict_default_passive = new Dictionary<string, PassiveEffects>();
        private static readonly Dictionary<string, MinEventTypes> dict_default_trigger = new Dictionary<string, MinEventTypes>();
        private static Dictionary<string, PassiveEffects> dict_final_passive = new Dictionary<string, PassiveEffects>();
        private static Dictionary<string, MinEventTypes> dict_final_trigger = new Dictionary<string, MinEventTypes>();

        //hooked to GameAwake
        public static void InitDefault()
        {
            for (PassiveEffects i = 0; i <= PassiveEffects.Count; i++)
                dict_default_passive.Add(i.ToString(), i);
            for (MinEventTypes i = 0; i <= MinEventTypes.COUNT; i++)
                dict_default_trigger.Add(i.ToString(), i);
        }

        //patched to GameManager.StartGame
        public static void InitFinal()
        {
            dict_final_passive = new Dictionary<string, PassiveEffects>(dict_default_passive);
            dict_final_trigger = new Dictionary<string, MinEventTypes>(dict_default_trigger);
        }

        public static void PrintResults()
        {
            Log.Out("Passive Effects:\n" + string.Join("\n", dict_final_passive.Select(p => $"passive: {p.Key} value: {p.Value}")));
            Log.Out("Trigger Effects:\n" + string.Join("\n", dict_final_trigger.Select(p => $"trigger: {p.Key} value: {p.Value}")));
        }

        //only call these from callbacks hooked to ModEvents.GameStartDone and cache the results for future usage
        //patched to PassiveEffect.ParsePassiveEffect
        public static PassiveEffects RegisterOrGetPassive(string passive)
        {
            if (!dict_final_passive.TryGetValue(passive, out var value))
            {
                if (dict_final_passive.Count >= byte.MaxValue)
                    throw new OverflowException("Passive effect count exceeds limit 255!");
                value = (PassiveEffects)(byte)dict_final_passive.Count;
                dict_final_passive.Add(passive, value);
            }
            return value;
        }

        //patched to MinEventActionBase.ParseXmlAttribute
        public static MinEventTypes RegisterOrGetTrigger(string trigger)
        {
            if (!dict_final_trigger.TryGetValue(trigger, out var value))
            {
                value = (MinEventTypes)dict_final_trigger.Count;
                dict_final_trigger.Add(trigger, value);
            }
            return value;
        }

    }
}
