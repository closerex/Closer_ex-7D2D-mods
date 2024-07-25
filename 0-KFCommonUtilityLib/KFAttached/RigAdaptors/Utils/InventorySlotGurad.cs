public class InventorySlotGurad
{
    public int Slot { get; private set; } = -1;

#if NotEditor
    public bool IsValid(EntityAlive entity)
    {
        if (entity && entity.inventory != null)
        {
            if (Slot < 0)
            {
                Slot = entity.inventory.holdingItemIdx;
                return true;
            }
            if (Slot != entity.inventory.holdingItemIdx)
            {
                Log.Warning($"trying to set ammo for slot {Slot} while holding slot {entity.inventory.holdingItemIdx} on entity {entity.entityId}!");
                return false;
            }
            return true;
        }
        return false;
    }
#endif
}
