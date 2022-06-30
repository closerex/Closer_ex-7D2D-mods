using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class ItemValueExtension
{
    public static string GetVehicleWeaponPropertyOverride(this ItemValue self, string partName, string _propertyName, string _originalValue)
    {
        if ((self.Modifications.Length == 0 && self.CosmeticMods.Length == 0))
            return _originalValue;

		string str = "";
		for (int i = 0; i < self.Modifications.Length; i++)
		{
			ItemValue itemValue = self.Modifications[i];
			if (itemValue != null)
			{
				ItemClassModifier itemClassModifier = itemValue.ItemClass as ItemClassModifier;
				if (itemClassModifier != null && itemClassModifier.GetPropertyOverride(_propertyName, partName, ref str))
					return str;
			}
		}

		str = "";
		for (int j = 0; j < self.CosmeticMods.Length; j++)
		{
			ItemValue itemValue2 = self.CosmeticMods[j];
			if (itemValue2 != null)
			{
				ItemClassModifier itemClassModifier2 = itemValue2.ItemClass as ItemClassModifier;
				if (itemClassModifier2 != null && itemClassModifier2.GetPropertyOverride(_propertyName, partName, ref str))
					return str;
			}
		}
		return _originalValue;
    }
}

