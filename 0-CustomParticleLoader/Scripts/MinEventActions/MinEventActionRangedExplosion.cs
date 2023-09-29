using System.Xml;
using System.Xml.Linq;
using UnityEngine;

public class MinEventActionRangedExplosion : MinEventActionBase
{
	private ExplosionData _explosionData;
	private int itemType = -1;
	private ExplosionComponent _explosionComponent;
	//private float delay = 0;
	private bool _initialized = false;
	private bool _useCustomParticle = false;
	private int customParticleIndex;
	private ItemValue ammoItem;

	public override void Execute(MinEventParams _params)
	{
		bool hasEntity = _params.Self != null;
		//int layer = 0;
  //      if (hasEntity)
  //      {
		//	layer = _params.Self.GetModelLayer();
		//	_params.Self.SetModelLayer(24, false);
  //      }
        GameManager.Instance.ExplosionServer(0, _params.Position, World.worldToBlockPos(_params.Position), hasEntity ? _params.Self.qrotation : Quaternion.identity, _useCustomParticle ? _explosionComponent.BoundExplosionData : _explosionData, hasEntity ? _params.Self.entityId : -1, Delay, false, ammoItem ?? _params.ItemValue);
		//if (hasEntity)
		//{
		//	_params.Self.SetModelLayer(layer, false);
		//}
	}

    public override bool CanExecute(MinEventTypes _eventType, MinEventParams _params)
    {
		if (!base.CanExecute(_eventType, _params))
			return false;

		if(!_initialized || !_useCustomParticle)
        {
			if (_useCustomParticle)
			{
				if (!CustomExplosionManager.GetCustomParticleComponents(customParticleIndex, out _explosionComponent))
					return false;
				if(_explosionComponent.BoundItemClass != null)
					ammoItem = new ItemValue(_explosionComponent.BoundItemClass.Id, false);
			}
			else
			{
				if (_params.ItemValue == null)
					return false;
				if (_params.ItemValue.type == itemType)
					return true;
				ItemClass itemClass = _params.ItemValue.ItemClass;
				string particleIndex = null;
				itemClass.Properties.ParseString("Explosion.ParticleIndex", ref particleIndex);
				if (string.IsNullOrEmpty(particleIndex))
					return false;

				if (int.TryParse(particleIndex, out int index))
				{
					_explosionData = new ExplosionData(itemClass.Properties);
					itemType = itemClass.Id;
					ammoItem = new ItemValue(itemType, false);
				}
				else if (CustomExplosionManager.GetCustomParticleComponents(CustomExplosionManager.getHashCode(particleIndex), out _explosionComponent))
				{
					itemType = itemClass.Id;
					if(_explosionComponent.BoundItemClass != null)
						ammoItem = new ItemValue(_explosionComponent.BoundItemClass.Id, false);
				}
				else
				{
					return false;
				}
			}

			_initialized = true;
        }

        return true;
    }

    public override bool ParseXmlAttribute(XAttribute _attribute)
	{
		bool flag = base.ParseXmlAttribute(_attribute);
		if (!flag)
		{
			string name = _attribute.Name.LocalName;
			switch(name)
            {
				case "particle_index":
					customParticleIndex = CustomExplosionManager.getHashCode(_attribute.Value);
					_useCustomParticle = true;
					flag = true;
					break;
				//case "delay":
				//	float.TryParse(_attribute.Value, out delay);
				//	flag = true;
				//	break;
            }
		}
		return flag;
	}
}
