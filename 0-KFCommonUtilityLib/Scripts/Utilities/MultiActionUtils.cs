namespace KFCommonUtilityLib.Scripts.Utilities
{
    public static class MultiActionUtils
    {
        public static readonly FastTags[] ActionIndexTags = new FastTags[]
        {
            FastTags.Parse("primary"),
            FastTags.Parse("secondary"),
            FastTags.Parse("tertiary"),
            FastTags.Parse("quaternary"),
            FastTags.Parse("quinary")
        };

        public static FastTags ActionIndexToTag(int index)
        {
            return ActionIndexTags[index];
        }

        public static string GetPropertyName(int index, string prop)
        {
            return $"Action{index}.{prop}";
        }
    }
}