using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using UniLinq;

[TypeTarget(typeof(ItemAction)), TypeDataTarget(typeof(TaggedData))]
public class ActionModuleTagged
{
    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    private void Postfix_OnModificationChanged(ItemAction __instance, ItemActionData _data, TaggedData __customData)
    {
        var tags = __instance.Properties.GetString("ActionTags").Split(',', System.StringSplitOptions.RemoveEmptyEntries);
        var tags_to_add = _data.invData.itemValue.GetAllPropertyOverridesForAction("ActionTagsAppend", __instance.ActionIndex).SelectMany(s => s.Split(',', System.StringSplitOptions.RemoveEmptyEntries));
        var tags_to_remove = _data.invData.itemValue.GetAllPropertyOverridesForAction("ActionTagsRemove", __instance.ActionIndex).SelectMany(s => s.Split(',', System.StringSplitOptions.RemoveEmptyEntries));
        var tags_result = tags.Union(tags_to_add);
        tags_result = tags_result.Except(tags_to_remove);

        __customData.tags = tags_result.Any() ? FastTags<TagGroup.Global>.Parse(string.Join(",", tags_result)) : FastTags<TagGroup.Global>.none;
        //Log.Out($"tags: {string.Join(",", tags_result)}");
    }

    public class TaggedData
    {
        public FastTags<TagGroup.Global> tags;
    }
}
