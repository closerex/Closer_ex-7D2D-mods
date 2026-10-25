using System.Globalization;

namespace LuaRuntimeProbe.Contracts
{
    /// <summary>Backend-neutral classification of a Lua value.</summary>
    public enum LuaValueKind
    {
        Nil = 0,
        Boolean = 1,
        Number = 2,
        String = 3,
        Table = 4,
        Function = 5,
        Userdata = 6,
        Thread = 7,
        Other = 8,
    }

    /// <summary>
    /// A normalised view of one value crossing the C#/Lua boundary.
    ///
    /// Backends disagree about integers (Lua 5.2 has no integer subtype),
    /// about CLR enums (userdata vs number) and about nulls, so results are
    /// always reported through this type instead of being assumed.
    /// </summary>
    public sealed class LuaValue
    {
        public LuaValueKind Kind = LuaValueKind.Nil;
        public bool Bool;
        public double Number;
        public bool IsInteger;
        public string Text;

        /// <summary>Backend-specific original value, for diagnostics only.</summary>
        public object Raw;

        public static LuaValue Nil()
        {
            return new LuaValue();
        }

        public static LuaValue FromBool(bool v)
        {
            return new LuaValue { Kind = LuaValueKind.Boolean, Bool = v };
        }

        public static LuaValue FromNumber(double v, bool isInteger)
        {
            return new LuaValue { Kind = LuaValueKind.Number, Number = v, IsInteger = isInteger };
        }

        public static LuaValue FromString(string v)
        {
            return new LuaValue { Kind = LuaValueKind.String, Text = v };
        }

        public static LuaValue FromOther(LuaValueKind kind, object raw)
        {
            return new LuaValue { Kind = kind, Raw = raw };
        }

        public bool IsNil
        {
            get { return Kind == LuaValueKind.Nil; }
        }

        public bool IsNumber
        {
            get { return Kind == LuaValueKind.Number; }
        }

        public bool IsString
        {
            get { return Kind == LuaValueKind.String; }
        }

        public bool IsBoolean
        {
            get { return Kind == LuaValueKind.Boolean; }
        }

        public string KindName
        {
            get { return Kind.ToString().ToLowerInvariant(); }
        }

        /// <summary>Human-readable one-line rendering used in console reports.</summary>
        public string Display
        {
            get
            {
                switch (Kind)
                {
                    case LuaValueKind.Nil:
                        return "nil";
                    case LuaValueKind.Boolean:
                        return Bool ? "true" : "false";
                    case LuaValueKind.Number:
                        return Number.ToString("R", CultureInfo.InvariantCulture);
                    case LuaValueKind.String:
                        return "\"" + (Text ?? string.Empty) + "\"";
                    default:
                        return "<" + KindName + ">";
                }
            }
        }

        public override string ToString()
        {
            return KindName + ":" + Display;
        }
    }
}
