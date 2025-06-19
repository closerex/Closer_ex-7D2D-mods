using KFCommonUtilityLib;
using System.Xml.Linq;

public class MinEventActionModifyCVarWithLocalCache : MinEventActionModifyCVar
{
    int targetHash;
    private int actionIndex = -1;
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        bool flag = !_params.Self.isEntityRemote && (actionIndex < 0 ? _params.ItemActionData : _params.ItemActionData.invData.actionData[actionIndex]) is IModuleContainerFor<ActionModuleLocalPassiveCache.LocalPassiveCacheData> && base.CanExecute(_eventType, _params);
        //Log.Out($"can execute {flag} is remote {_params.Self.isEntityRemote} action index {actionIndex} cache {targetHash.ToString()} action {(actionIndex < 0 ? _params.ItemActionData : _params.ItemActionData.invData.actionData[actionIndex]).GetType().Name}");
        return flag;
    }

    public override void Execute(MinEventParams _params)
    {
        if (_params.Self.isEntityRemote && !_params.IsLocal)
        {
            return;
        }
        ActionModuleLocalPassiveCache.LocalPassiveCacheData _data = ((IModuleContainerFor<ActionModuleLocalPassiveCache.LocalPassiveCacheData>)(actionIndex < 0 ? _params.ItemActionData : _params.ItemActionData.invData.actionData[actionIndex])).Instance;
        float value = _data.GetCachedValue(targetHash);
        //Log.Out($"cache {targetHash.ToString()} value {value}");
        for (int i = 0; i < targets.Count; i++)
        {
            float num = targets[i].Buffs.GetCustomVar(cvarName);
            switch (operation)
            {
                case CVarOperation.set:
                case CVarOperation.setvalue:
                    num = value;
                    break;
                case CVarOperation.add:
                    num += value;
                    break;
                case CVarOperation.subtract:
                    num -= value;
                    break;
                case CVarOperation.multiply:
                    num *= value;
                    break;
                case CVarOperation.divide:
                    num /= ((value == 0f) ? 0.0001f : value);
                    break;
            }
            targets[i].Buffs.SetCustomVar(cvarName, num, (targets[i].isEntityRemote && !_params.Self.isEntityRemote) || _params.IsLocal);
        }
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        bool flag = false;
        string name = _attribute.Name.LocalName;
        if (name != null)
        {
            if (name == "cache")
            {
                targetHash = _attribute.Value.GetHashCode();
                flag = true;
            }
            else if (name == "action_index")
            {
                actionIndex = int.Parse(_attribute.Value);
                flag = true;
            }
        }

        if (!flag)
            flag = base.ParseXmlAttribute(_attribute);
        return flag;
    }
}