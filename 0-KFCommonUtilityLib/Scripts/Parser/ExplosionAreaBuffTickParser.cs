using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ExplosionAreaBuffTickParser : IExplosionPropertyParser
{
    public static readonly string name = "ExplosionAreaBuffTick";
    public static readonly string str_tick_interval = "TickInterval";
    public Type MatchScriptType()
    {
        return typeof(ExplosionAreaBuffTick);
    }

    public string Name()
    {
        return name;
    }

    public bool ParseProperty(DynamicProperties _props, out object property)
    {
        float interval = 0.5f;
        _props.ParseFloat(str_tick_interval, ref interval);
        property = interval;
        return true;
    }
}

