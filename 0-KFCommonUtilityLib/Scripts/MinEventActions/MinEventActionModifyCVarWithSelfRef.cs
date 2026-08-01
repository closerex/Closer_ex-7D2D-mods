using System.Xml.Linq;

class MinEventActionModifyCVarWithSelfRef : MinEventActionModifyCVar
{
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        return base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        if (cvarRef)
        {
            if (_params.Self.isEntityRemote && !_params.IsLocal)
            {
                return;
            }
            value = _params.Self.Buffs.GetCustomVar(refCvarName);
            for (int i = 0; i < targets.Count; i++)
            {
                EntityAlive target = targets[i];
                target.Buffs.SetCustomVar(cvarName, value, (target.isEntityRemote && !_params.Self.isEntityRemote) || (!target.isEntityRemote && !_params.Self.isEntityRemote && target != _params.Self) || _params.IsLocal, operation);
            }
        }
        else
            base.Execute(_params);
    }
}

