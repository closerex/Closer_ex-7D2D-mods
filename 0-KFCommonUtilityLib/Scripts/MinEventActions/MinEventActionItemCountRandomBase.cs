using System.Xml.Linq;
using UnityEngine;
public class MinEventActionItemCountRandomBase : MinEventActionBase
{
    private bool useRange = false;
    private bool useRandom = false;
    private bool useCvar = false;
    private int[] random;
    private Vector2i range;
    private string cvarRef;
    private int constant;

    protected virtual int GetCount(MinEventParams _params)
    {
        if (useRandom)
            return random[Random.Range(0, random.Length)];
        else if (useRange)
            return Random.Range(range.x, range.y + 1);
        else if (useCvar)
            return (int)_params.Self.GetCVar(cvarRef);
        else
            return constant;
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
    {
        if (base.ParseXmlAttribute(_attribute))
            return true;

        switch (_attribute.Name.LocalName)
        {
            case "count":
                string str = _attribute.Value;
                if (str.StartsWith("random"))
                {
                    useRandom = true;
                    string[] values = str.Substring(str.IndexOf('(') + 1, str.IndexOf(')') - str.IndexOf('(') - 1).Split(',');
                    random = new int[values.Length];
                    for (int i = 0; i < values.Length; i++)
                        random[i] = int.Parse(values[i]);
                }
                else if (str.StartsWith("range"))
                {
                    useRange = true;
                    range = StringParsers.ParseVector2i(str.Substring(str.IndexOf('(') + 1, str.IndexOf(')') - str.IndexOf('(') - 1));
                }
                else if (str.StartsWith("@"))
                {
                    useCvar = true;
                    cvarRef = str.Substring(1);
                }
                else
                    return int.TryParse(str, out constant);

                return true;
            default:
                return false;
        }
    }
}
