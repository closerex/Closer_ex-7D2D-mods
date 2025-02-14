using KFCommonUtilityLib;
using System.Xml.Linq;

public class ActionHasTags : TargetedCompareRequirementBase
{
    private FastTags<TagGroup.Global> actionTags;

    private bool hasAllTags;

    public override bool IsValid(MinEventParams _params)
    {
        if (!base.IsValid(_params))
        {
            return false;
        }

        bool flag = false;
        if (_params.ItemActionData is IModuleContainerFor<ActionModuleTagged.TaggedData> tagged)
        {
            flag = (hasAllTags ? tagged.Instance.tags.Test_AllSet(actionTags) : tagged.Instance.tags.Test_AnySet(actionTags));
        }

        if (!invert)
        {
            return flag;
        }

        return !flag;
    }

    public override bool ParseXAttribute(XAttribute _attribute)
    {
        bool flag = base.ParseXAttribute(_attribute);
        if (!flag)
        {
            string localName = _attribute.Name.LocalName;
            if (localName == "tags")
            {
                actionTags = FastTags<TagGroup.Global>.Parse(_attribute.Value);
                return true;
            }

            if (localName == "has_all_tags")
            {
                hasAllTags = StringParsers.ParseBool(_attribute.Value);
                return true;
            }
        }

        return flag;
    }
}