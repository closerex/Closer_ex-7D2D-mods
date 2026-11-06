namespace KFCommonUtilityLib
{
    public interface ICustomInvData
    {
        ItemInventoryData createItemInventoryData(EntityAlive _entity, ItemStack _stack, int _toolbeltSlotIdx);
    }
}