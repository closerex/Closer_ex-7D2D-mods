using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class EntityInventoryExtension
{
    public static void TryStackItem(this EntityAlive self, ItemStack stack)
    {
        if (self.bag.TryStackItem(0, stack))
            return;
        self.inventory.TryStackItem(0, stack);
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
}

