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
                float num = targets[i].Buffs.GetCustomVar(cvarName);
                switch (operation)
                {
                    case OperationTypes.set:
                    case OperationTypes.setvalue:
                        num = value;
                        break;
                    case OperationTypes.add:
                        num += value;
                        break;
                    case OperationTypes.subtract:
                        num -= value;
                        break;
                    case OperationTypes.multiply:
                        num *= value;
                        break;
                    case OperationTypes.divide:
                        num /= ((value == 0f) ? 0.0001f : value);
                        break;
                }
                targets[i].Buffs.SetCustomVar(cvarName, num, (targets[i].isEntityRemote && !_params.Self.isEntityRemote) || _params.IsLocal);
            }
        }
        else
            base.Execute(_params);
    }
}

