using System.Xml;
using System.Xml.Linq;

class MinEventActionModifyCVarWithSelfRef : MinEventActionModifyCVar
{
    private enum OperationTypes
    {
        set,
        setvalue,
        add,
        subtract,
        multiply,
        divide
    }
    bool cvarRef = false;
    string refCvarName;
    float value;
    OperationTypes operation;

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        bool flag = false;
        string name = _attribute.Name.LocalName;
        if (name != null)
        {
            if(name == "value" && _attribute.Value.StartsWith("@"))
            {
                this.cvarRef = true;
                this.refCvarName = _attribute.Value.Substring(1);
                flag = true;
            }else if (name == "operation")
            {
                this.operation = EnumUtils.Parse<MinEventActionModifyCVarWithSelfRef.OperationTypes>(_attribute.Value, true);
            }
        }
        
        if (!flag)
            flag = base.ParseXmlAttribute(_attribute);
        return flag;
    }

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        return base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        if(this.cvarRef)
        {
            if (_params.Self.isEntityRemote && !_params.IsLocal)
            {
                return;
            }
            this.value = _params.Self.Buffs.GetCustomVar(this.refCvarName);
            for (int i = 0; i < this.targets.Count; i++)
            {
                float num = this.targets[i].Buffs.GetCustomVar(this.cvarName);
                switch (this.operation)
                {
                    case MinEventActionModifyCVarWithSelfRef.OperationTypes.set:
                    case MinEventActionModifyCVarWithSelfRef.OperationTypes.setvalue:
                        num = this.value;
                        break;
                    case MinEventActionModifyCVarWithSelfRef.OperationTypes.add:
                        num += this.value;
                        break;
                    case MinEventActionModifyCVarWithSelfRef.OperationTypes.subtract:
                        num -= this.value;
                        break;
                    case MinEventActionModifyCVarWithSelfRef.OperationTypes.multiply:
                        num *= this.value;
                        break;
                    case MinEventActionModifyCVarWithSelfRef.OperationTypes.divide:
                        num /= ((this.value == 0f) ? 0.0001f : this.value);
                        break;
                }
                this.targets[i].Buffs.SetCustomVar(this.cvarName, num, (this.targets[i].isEntityRemote && !_params.Self.isEntityRemote) || _params.IsLocal);
            }
        }else
            base.Execute(_params);
    }
}

