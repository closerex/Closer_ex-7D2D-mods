using System;
using System.Reflection;
using System.Reflection.Emit;
using KFAssemblyPatcher;

namespace KFCommonUtilityLib
{
    public static class InventoryModuleStorage
    {
        public static readonly Type BaseType;
        public static readonly FieldInfo DataField;
        private static readonly Func<ItemInventoryData, object> getter;
        private static readonly Action<ItemInventoryData, object> setter;
        private static readonly Func<object, ItemClass> ownerGetter;

        static InventoryModuleStorage()
        {
            var native = typeof(ItemInventoryData);
            if (!AssemblyVirtualizer.HasInventoryModuleLayout(native))
                throw new TypeLoadException("Inventory module storage is missing or incompatible; repair the game assembly and restart.");
            BaseType = native.Assembly.GetType(AssemblyVirtualizer.InventoryModuleTypeName, true);
            DataField = native.GetField(AssemblyVirtualizer.InventoryModuleFieldName);

            var get = new DynamicMethod("GetInventoryModules", typeof(object), new[] { native }, native, true);
            var il = get.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, DataField);
            il.Emit(OpCodes.Ret);
            getter = (Func<ItemInventoryData, object>)get.CreateDelegate(typeof(Func<ItemInventoryData, object>));

            var set = new DynamicMethod("SetInventoryModules", typeof(void), new[] { native, typeof(object) }, native, true);
            il = set.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, BaseType);
            il.Emit(OpCodes.Stfld, DataField);
            il.Emit(OpCodes.Ret);
            setter = (Action<ItemInventoryData, object>)set.CreateDelegate(typeof(Action<ItemInventoryData, object>));

            var owner = new DynamicMethod("GetInventoryModuleOwner", typeof(ItemClass), new[] { typeof(object) }, native, true);
            il = owner.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, BaseType);
            il.Emit(OpCodes.Ldfld, BaseType.GetField("Owner"));
            il.Emit(OpCodes.Ret);
            ownerGetter = (Func<object, ItemClass>)owner.CreateDelegate(typeof(Func<object, ItemClass>));
        }

        public static object Get(ItemInventoryData inventoryData) => getter(inventoryData);
        public static void Set(ItemInventoryData inventoryData, object modules) => setter(inventoryData, modules);

        // Keep the old owner available until native stop/cleanup has finished.
        public static void Ensure(ItemInventoryData inventoryData, bool building = false)
        {
            var item = inventoryData.item;
            var modules = Get(inventoryData);
            if (!building && modules != null)
            {
                var owner = ownerGetter(modules);
                if (ReferenceEquals(owner, item))
                    return;
                if (inventoryData.PhysicalItemValue != null &&
                    ReferenceEquals(owner, inventoryData.PhysicalItemValue.ItemClass))
                    return;
            }
            Set(inventoryData, item is ICustomInvData custom ? custom.createItemInventoryData(inventoryData) : null);
        }
    }
}
