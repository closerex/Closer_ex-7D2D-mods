public interface IExplosionPropertyParser
{
    bool ParseProperty(DynamicProperties _props, ExplosionComponent component, out object property);
    System.Type MatchScriptType();
    string Name();
}
