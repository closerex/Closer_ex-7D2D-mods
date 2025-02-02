public class IsLocal : RequirementBase
{
    public override bool IsValid(MinEventParams _params)
    {
        return base.IsValid(_params) && ((_params.IsLocal || (_params.Self && !_params.Self.isEntityRemote)) ^ invert);
    }
}