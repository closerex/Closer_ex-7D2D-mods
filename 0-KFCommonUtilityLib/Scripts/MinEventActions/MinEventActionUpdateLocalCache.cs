using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using System.Xml.Linq;

public class MinEventActionUpdateLocalCache : MinEventActionBase
{
    private PassiveEffects passive;
    private FastTags tags;
    private int actionIndex = -1;
    private int saveAs;
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        return !_params.Self.isEntityRemote && (actionIndex < 0 ? _params.ItemActionData : _params.ItemActionData.invData.actionData[actionIndex]) is IModuleContainerFor<ActionModuleLocalPassiveCache.LocalPassiveCacheData> && base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        ActionModuleLocalPassiveCache.LocalPassiveCacheData _data = ((IModuleContainerFor<ActionModuleLocalPassiveCache.LocalPassiveCacheData>)(actionIndex < 0 ? _params.ItemActionData : _params.ItemActionData.invData.actionData[actionIndex])).Instance;

        _data.CachePassive(passive, saveAs, tags);
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if (base.ParseXmlAttribute(_attribute))
            return true;

        switch (_attribute.Name.LocalName)
        {
            case "passive":
                passive = CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>(_attribute.Value);
                return true;
            case "tags":
                tags = FastTags.Parse(_attribute.Value);
                return true;
            case "action_index":
                actionIndex = int.Parse(_attribute.Value);
                return true;
            case "as":
                saveAs = _attribute.Value.GetHashCode();
                return true;
        }

        return false;
    }
}