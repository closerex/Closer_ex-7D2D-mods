using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using UniLinq;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleTagged
{
    [MethodTargetPostfix(nameof(ItemAction.OnModificationsChanged))]
    private void Postfix_OnModificationChanged(ItemAction __instance, ItemActionData _data, TaggedData __customData)
    {
        var tags = __instance.Properties.GetString("ActionTags").Split(',');
        var tags_to_add = _data.invData.itemValue.GetAllPropertyOverridesForAction("ActionTagsAppend", __instance.ActionIndex);
        var tags_to_remove = _data.invData.itemValue.GetAllPropertyOverridesForAction("ActionTagsRemove", __instance.ActionIndex);
        var tags_result = tags.Union(tags_to_add);
        tags_result = tags_result.Union(tags_result.Except(tags_to_remove));

        __customData.tags = tags_result.Any() ? FastTags.Parse(string.Join(",", tags_result)) : FastTags.none;
    }

    public class TaggedData
    {
        public FastTags tags;
    }
}
