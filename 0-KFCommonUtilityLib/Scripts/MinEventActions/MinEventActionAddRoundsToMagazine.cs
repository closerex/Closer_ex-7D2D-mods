public class MinEventActionAddRoundsToMagazine : MinEventActionAmmoAccessBase
{
    public override void Execute(MinEventParams _params)
    {
        _params.ItemValue.Meta += GetCount(_params);
    }
}

