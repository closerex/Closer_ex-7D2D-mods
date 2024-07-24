public class InventorySlotGurad
{
    private int slot = -1;

#if NotEditor
    public bool IsValid(EntityAlive entity)
    {
        if (entity && entity.inventory != null)
        {
            if (slot < 0)
            {
                slot = entity.inventory.holdingItemIdx;
                return true;
            }
            if (slot != entity.inventory.holdingItemIdx)
            {
                Log.Warning($"trying to set ammo for slot {slot} while holding slot {entity.inventory.holdingItemIdx} on entity {entity.entityId}!");
                return false;
            }
            return true;
        }
        return false;
    }
#endif
}
