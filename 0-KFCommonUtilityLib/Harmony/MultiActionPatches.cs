using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KFCommonUtilityLib.Harmony
{
    //todo: patch all accesses to ItemClass.Actions so that they process all actions
    [HarmonyPatch]
    public static class MultiActionPatches
    {
        //maybe use TriggerHasTags instead?
        public struct TagsForAll
        {
            public FastTags tags;
            public bool matchAllTags;
            public bool invertTagCheck;

            public bool IsValid()
            {
                return !tags.IsEmpty || matchAllTags || invertTagCheck;
            }
        }

        [HarmonyPatch(typeof(MinEffectGroup), nameof(MinEffectGroup.ParseXml))]
        [HarmonyPrefix]
        private static bool Prefix_ParseXml_MinEffectGroup(XElement _element, out TagsForAll __state)
        {
            __state = new TagsForAll()
            {
                tags = FastTags.none,
                matchAllTags = false,
                invertTagCheck = false
            };
            string tags = _element.GetAttribute("tags");
            __state.tags = tags != null ? FastTags.Parse(tags) : FastTags.none;
            if (_element.HasAttribute("match_all_tags"))
            {
                __state.matchAllTags = true;
            }
            if (_element.HasAttribute("invert_tag_check"))
            {
                __state.invertTagCheck = true;
            }

            return true;
        }


        [HarmonyPatch(typeof(MinEffectGroup), nameof(MinEffectGroup.ParseXml))]
        [HarmonyPostfix]
        private static void Postfix_ParseXml_MinEffectGroup(MinEffectGroup __instance, TagsForAll __state)
        {
            if (!__state.IsValid())
                return;

            foreach (var passive in __instance.PassiveEffects)
            {
                if (!__state.tags.IsEmpty)
                {
                    passive.Tags |= __state.tags;
                }

                if (__state.matchAllTags)
                {
                    passive.MatchAnyTags = false;
                }

                if (__state.invertTagCheck)
                {
                    passive.InvertTagCheck = true;
                }
            }
        }
    }
}
