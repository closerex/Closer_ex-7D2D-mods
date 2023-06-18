using System.Xml;
using UnityEngine;

public class MinEventActionRangedExplosion : MinEventActionBase
{
	private ExplosionData _explosionData;
	private int itemType = -1;
	private ExplosionComponent _explosionComponent;
	private float delay = 0;
	private bool _initialized = false;
	private bool _useCustomParticle = false;

	public override void Execute(MinEventParams _params)
	{
		GameManager.Instance.ExplosionServer(0, _params.Position, World.worldToBlockPos(_params.Position), Quaternion.identity, _useCustomParticle ? _explosionComponent.BoundExplosionData : _explosionData, _params.Self != null ? _params.Self.entityId : -1, delay, false, _params.ItemValue);
	}

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
		if (!base.CanExecute(_eventType, _params))
			return false;

		if(!_initialized || (!_useCustomParticle && _params.ItemValue != null && _params.ItemValue.type != itemType))
        {
			ItemClass itemClass = _params.ItemValue.ItemClass;
			string particleIndex = null;
			itemClass.Properties.ParseString("Explosion.ParticleIndex", ref particleIndex);
			if (string.IsNullOrEmpty(particleIndex))
				return false;

			if (int.TryParse(particleIndex, out int index))
            {
				_explosionData = new ExplosionData(itemClass.Properties);
				itemType = itemClass.Id;
            }
			else if(!CustomExplosionManager.GetCustomParticleComponents(CustomExplosionManager.getHashCode(particleIndex), out _explosionComponent))
				return false;

			_initialized = true;
			if (_explosionComponent != null)
				_useCustomParticle = true;
        }

        return true;
    }

    public override bool ParseXmlAttribute(XmlAttribute _attribute)
	{
		bool flag = base.ParseXmlAttribute(_attribute);
		if (!flag)
		{
			string name = _attribute.Name;
			switch(name)
            {
				case "particle_index":
					if(CustomExplosionManager.GetCustomParticleComponents(CustomExplosionManager.getHashCode(_attribute.Value), out _explosionComponent))
					{
						_useCustomParticle = true;
						_initialized = true;
						flag = true;
					}
					break;
				case "delay":
					float.TryParse(_attribute.Value, out delay);
					flag = true;
					break;
            }
		}
		return flag;
	}
}
