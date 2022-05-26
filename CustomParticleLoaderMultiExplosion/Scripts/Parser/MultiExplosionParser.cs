using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class MultiExplosionParser : IExplosionPropertyParser
{
    public static readonly string name = "MultiExplosion";
    public static readonly string str_sub_explosion = "Explosion.SubExplosion";
    public static readonly string str_sub_explosion_transform = "Explosion.SubExplosionTransform";
    public Type MatchScriptType()
    {
        return typeof(SubExplosionController);
    }

    public string Name()
    {
        return name;
    }

    public bool ParseProperty(DynamicProperties _props, out object property)
    {
        property = new MultiExplosionProperty();
        var _prop = property as MultiExplosionProperty;
        string sub_explosion = null;
        string[] arr_sub_explosions = null;
        List<int> list_sub_explosion_indice = new List<int>();
        _props.ParseString(str_sub_explosion, ref sub_explosion);
        if (sub_explosion != null)
        {
            arr_sub_explosions = sub_explosion.Split(',');
            foreach (string str in arr_sub_explosions)
            {
                if (str.StartsWith("#"))
                {
                    int index = CustomExplosionManager.getHashCode(str.Trim());
                    if (index >= WorldStaticData.prefabExplosions.Length)
                        list_sub_explosion_indice.Add(index);
                }
            }
        }

        if (list_sub_explosion_indice.Count > 0)
        {
            _prop.list_sub_explosion_indice = list_sub_explosion_indice;
            Log.Out("Adding subexplosion data: " + sub_explosion);
        }
        else
            return false;

        string sub_transform = null;
        string[] arr_transforms = null;
        _props.ParseString(str_sub_explosion_transform, ref sub_transform);
        arr_transforms = sub_transform.Split(',');
        _prop.arr_transforms = arr_transforms;
        Log.Out("Adding transform: " + string.Join(" ", arr_transforms));
        property = _prop;
        return true;
    }
}

public class MultiExplosionProperty
{
    public string[] arr_transforms { get; internal set; } = null;
    public List<int> list_sub_explosion_indice { get; internal set; } = new List<int>();
}

