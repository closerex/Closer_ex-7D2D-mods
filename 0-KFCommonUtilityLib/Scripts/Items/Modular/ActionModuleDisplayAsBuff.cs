﻿using KFCommonUtilityLib.Scripts.Attributes;

public class DisplayAsBuffEntityUINotification : BuffEntityUINotification
{
    public ActionModuleDisplayAsBuff.DisplayValueType displayType = ActionModuleDisplayAsBuff.DisplayValueType.Meta;
    public string displayData = string.Empty;

    public override float CurrentValue
    {
        get
        {
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null)
                return 0;
            switch (displayType)
            {
                case ActionModuleDisplayAsBuff.DisplayValueType.Meta:
                    return player.inventory.holdingItemItemValue.Meta;
                case ActionModuleDisplayAsBuff.DisplayValueType.MetaData:
                    return (float)player.inventory.holdingItemItemValue.GetMetadata(displayData);
                default:
                    return 0;
            }
        }
    }

    public override bool Visible => true;

    public override EnumEntityUINotificationDisplayMode DisplayMode => EnumEntityUINotificationDisplayMode.IconPlusCurrentValue;
}

[TypeTarget(typeof(ItemActionRanged))]
public class ActionModuleDisplayAsBuff
{
    public enum DisplayValueType
    {
        Meta,
        MetaData
    }

    private DisplayAsBuffEntityUINotification notification;
    private BuffClass buffClass;

    [MethodTargetPostfix(nameof(ItemActionRanged.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        notification = new DisplayAsBuffEntityUINotification();
        _props.Values.TryGetValue("DisplayType", out string str);
        EnumUtils.TryParse(str, out notification.displayType, true);
        _props.Values.TryGetValue("DisplayData", out notification.displayData);
        _props.Values.TryGetValue("DisplayBuff", out str);
        BuffClass buffClass = BuffManager.GetBuff(str);
        BuffValue buff = new BuffValue(buffClass.Name, Vector3i.zero, -1, buffClass);
        notification.SetBuff(buff);
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.StartHolding))]
    private void Postfix_StartHolding(ItemActionData _data)
    {
        EntityPlayerLocal player = _data.invData.holdingEntity as EntityPlayerLocal;
        if (player != null && notification != null)
        {
            notification.SetStats(player.Stats);
            player.Stats.NotificationAdded(notification);
        }
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.StopHolding))]
    private void Postfix_StopHolding(ItemActionData _data)
    {
        EntityPlayerLocal player = _data.invData.holdingEntity as EntityPlayerLocal;
        if (player != null && notification != null)
        {
            player.Stats.NotificationRemoved(notification);
        }
    }
}
