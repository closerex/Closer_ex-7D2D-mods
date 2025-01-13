public class TypeBasedUID<T>
{
    private static int uid = 0;
    public static int UID { get => uid++; }
}