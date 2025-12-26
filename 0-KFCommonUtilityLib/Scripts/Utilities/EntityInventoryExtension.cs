public static class EntityInventoryExtension
{
    public static void TryStackItem(this EntityAlive self, ItemStack stack)
    {
        if (self.bag.TryStackItem(stack))
            return;
        self.inventory.TryStackItem(stack);
    }

    public static int TryRemoveItem(this EntityAlive self, int count, ItemValue value)
    {
        int countLeft = count - self.bag.DecItem(value, count);
        if (countLeft > 0)
            countLeft = self.inventory.DecItem(value, countLeft);
        return countLeft;
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

        if (self.itemValue.type != other.itemValue.type || self.itemValue.IsShapeHelperBlock || self.itemValue.TextureFullArray != other.itemValue.TextureFullArray || self.count >= maxStackCount)
            return;

        int add = Utils.FastMin(maxStackCount - self.count, other.count);
        self.count += add;
        other.count -= add;
    }

    public static bool ShouldUpdateItem(this ItemStack self, ItemStack other)
    {
        if (self.count != other.count || self.itemValue.type != other.itemValue.type)
        {
            return true;
        }

        return ShouldUpdateItem(self.itemValue, other.itemValue);
    }

    public static bool ShouldUpdateItem(this ItemValue self, ItemValue other)
    {
        if (self.type != other.type)
        {
            return true;
        }
        bool isHoldingGun = self.ItemClass != null && self.ItemClass.Actions != null && self.ItemClass.Actions.Length != 0 && self.ItemClass.Actions[0] is ItemActionRanged;
        if (!isHoldingGun && self.Meta != other.Meta)
        {
            return true;
        }

        return ShouldUpdateItem(other.CosmeticMods, self.CosmeticMods) || ShouldUpdateItem(other.Modifications, self.Modifications);
    }

    public static bool ShouldUpdateItem(ItemValue[] self, ItemValue[] other)
    {
        if (self == null || other == null)
        {
            return self != other;
        }

        if (self.Length != other.Length)
        {
            return true;
        }

        for (int i = 0; i < self.Length; i++)
        {
            if (ShouldUpdateItem(self[i], other[i]))
            {
                return true;
            }
        }

        return false;
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

