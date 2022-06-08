public interface IExplosionPropertyParser
{
    bool ParseProperty(DynamicProperties _props, out object property);
    System.Type MatchScriptType();
    string Name();
}
