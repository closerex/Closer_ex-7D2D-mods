public static class EntityInventoryExtension
{
    public static void TryStackItem(this EntityAlive self, ItemStack stack)
    {
        if (self.bag.TryStackItem(stack))
            return;
        self.inventory.TryStackItem(stack);
    }

    public static void TryRemoveItem(this EntityAlive self, int count, ItemValue value)
    {
        int decFromInv = self.bag.DecItem(value, count) - count;
        if (decFromInv > 0)
            self.inventory.DecItem(value, decFromInv);
    }

    public static int GetItemCount(this EntityAlive self, ItemValue value)
    {
        return self.inventory.GetItemCount(value) + self.bag.GetItemCount(value);
    }

    public static bool TryStackItem(this Bag self, ItemStack stack)
    {
        ItemStack[] slots = self.GetSlots();
        TryStackItem(slots, stack);
        self.SetSlots(slots);
        return stack.count == 0;
    }

    public static bool TryStackItem(this Inventory self, ItemStack stack)
    {
        ItemStack[] slots = self.GetSlots();
        TryStackItem(slots, stack);
        self.CallOnToolbeltChangedInternal();
        return stack.count == 0;
    }

    public static void TryStackWith(this ItemStack self, ItemStack other)
    {
        int maxStackCount = other.itemValue.ItemClass.Stacknumber.Value;
        if (self.IsEmpty())
        {
            self.itemValue = other.itemValue.Clone();
            self.count = Utils.FastMin(maxStackCount, other.count);
            other.count -= self.count;
            return;
        }

        if (self.itemValue.type != other.itemValue.type || self.itemValue.Texture != other.itemValue.Texture || self.count >= maxStackCount)
            return;

        int add = Utils.FastMin(maxStackCount - self.count, other.count);
        self.count += add;
        other.count -= add;
    }

    private static void TryStackItem(ItemStack[] slots, ItemStack stack)
    {
        foreach (var slot in slots)
        {
            slot.TryStackWith(stack);
            if (stack.count == 0)
                return;
        }
    }
}

