using System.Collections.Generic;

public class MetaInItem : TargetedCompareRequirementBase
{
    public override bool IsValid(MinEventParams _params)
    {
        if (!base.IsValid(_params))
        {
            return false;
        }
        if (_params.ItemValue.IsEmpty())
        {
            return false;
        }
        if (invert)
        {
            return !RequirementBase.compareValues((float)_params.ItemValue.Meta, this.operation, this.value);
        }
        return RequirementBase.compareValues((float)_params.ItemValue.Meta, this.operation, this.value);
    }

    public override void GetInfoStrings(ref List<string> list)
    {
        list.Add(string.Format("Meta in Item: {0}{1} {2}", this.invert ? "NOT " : "", this.operation.ToStringCached<RequirementBase.OperationTypes>(), this.value.ToCultureInvariantString()));
    }
}
