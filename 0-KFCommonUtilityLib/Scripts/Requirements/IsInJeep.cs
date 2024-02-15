public class IsInJeep : IsAttachedToEntity
{
    public override bool IsValid(MinEventParams _params)
    {
        if (!base.IsValid(_params))
        {
            return false;
        }
        if (invert)
        {
            return !(target.AttachedToEntity is EntityVJeep);
        }
        return target.AttachedToEntity is EntityVJeep;
    }
}
