using System.IO;
using UnityEngine;
using XMLData.Item;

public class NetPackageExplosionSyncOnConnect : NetPackage
{
	public NetPackageExplosionSyncOnConnect Setup(byte[] data)
	{
		this.data = data != null ? data : new byte[0];
		return this;
	}

	public override void read(PooledBinaryReader _br)
	{
		int bytes = _br.ReadInt32();
		data = _br.ReadBytes(bytes);
	}

	public override void write(PooledBinaryWriter _bw)
	{
		base.write(_bw);
		_bw.Write(data.Length);
		_bw.Write(data);
	}

	public override void ProcessPackage(World _world, GameManager _callbacks)
	{
		if (_world == null)
		{
			return;
		}

		using (PooledBinaryReader _br = MemoryPools.poolBinaryReader.AllocSync(false))
		{
			_br.SetBaseStream(new MemoryStream(data));
			uint count = _br.ReadUInt32();
			for(int i = 0; i < count; ++i)
			{
				int bytes = (int)_br.ReadUInt16();
				ExplosionParams explParams = new ExplosionParams(_br.ReadBytes(bytes));
				ItemValue explValue = null;
				if (_br.ReadBoolean())
				{
					explValue = new ItemValue();
					explValue.Read(_br);
				}
				CustomExplosionManager.GetCustomParticleComponents(explParams._explosionData.ParticleIndex, out ExplosionComponent component);

                ExplosionValue value = new ExplosionValue()
                {
                    Component = component,
                    CurrentExplosionParams = explParams,
                    CurrentItemValue = explValue?.Clone()
                };
                CustomExplosionManager.PushLastInitComponent(value);
				GameObject obj = CustomExplosionManager.InitializeParticle(component, explParams._worldPos - Origin.position, explParams._rotation);
				obj.GetComponent<NetSyncHelper>().OnConnectedToServer(_br);
			}
			CustomExplosionManager.PopLastInitComponent();
		}
	}

	public override NetPackageDirection PackageDirection
	{
		get
		{
			return NetPackageDirection.ToClient;
		}
	}

	public override int GetLength()
	{
		return 8 + data.Length;
	}

	private byte[] data;
}
