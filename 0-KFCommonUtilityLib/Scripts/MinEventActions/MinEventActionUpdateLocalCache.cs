using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using System.Xml.Linq;

public class MinEventActionUpdateLocalCache : MinEventActionBase
{
    private PassiveEffects[] passives;
    private int actionIndex = -1;
    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
        return !_params.Self.isEntityRemote && (actionIndex < 0 ? _params.ItemActionData : _params.ItemActionData.invData.actionData[actionIndex]) is IModuleContainerFor<ActionModuleLocalPassiveCache.LocalPassiveCacheData> && base.CanExecute(_eventType, _params);
    }

    public override void Execute(MinEventParams _params)
    {
        ActionModuleLocalPassiveCache.LocalPassiveCacheData _data = ((IModuleContainerFor<ActionModuleLocalPassiveCache.LocalPassiveCacheData>)(actionIndex < 0 ? _params.ItemActionData : _params.ItemActionData.invData.actionData[actionIndex])).Instance;
        foreach (var passive in passives)
        {
            _data.MarkForCache(passive);
        }
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if (base.ParseXmlAttribute(_attribute))
            return true;

        switch (_attribute.Name.LocalName)
        {
            case "passives":
                passives = Array.ConvertAll(_attribute.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), s => CustomEffectEnumManager.RegisterOrGetEnum<PassiveEffects>(s));
                return true;
            case "action_index":
                actionIndex = int.Parse(_attribute.Value);
                return true;
        }

        return false;
    }
}