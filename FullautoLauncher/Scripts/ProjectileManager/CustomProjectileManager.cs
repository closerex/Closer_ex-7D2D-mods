using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FullautoLauncher.Scripts.ProjectileManager
{
    public static class CustomProjectileManager
    {
        private static readonly Dictionary<string, IProjectileItemGroup> dict_item_groups = new Dictionary<string, IProjectileItemGroup>();

        public static IProjectileItemGroup Get(string name) => dict_item_groups[name];

        public static void InitClass(ItemClass item, string typename)
        {
            if (dict_item_groups.ContainsKey(item.Name))
            {
                return;
            }
            Type type = ReflectionHelpers.GetTypeWithPrefix("PIG", typename);
            IProjectileItemGroup group = (IProjectileItemGroup)Activator.CreateInstance(type, new object[] {item});
            dict_item_groups.Add(item.Name, group);
        }

        public static void Update()
        {
            foreach (var group in dict_item_groups.Values)
            {
                group.Update();
            }
        }

        public static void FixedUpdate()
        {
            foreach (var group in dict_item_groups.Values)
            {
                group.FixedUpdate();
            }
        }

        public static void Cleanup()
        {
            foreach (var group in dict_item_groups.Values)
            {
                group.Cleanup();
            }
            dict_item_groups.Clear();
        }
    }
}
