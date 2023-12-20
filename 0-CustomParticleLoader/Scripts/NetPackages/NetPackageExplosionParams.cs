using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class NetPackageExplosionParams : NetPackage
{
	public NetPackageExplosionParams Setup(int _clrIdx, Vector3 _worldPos, Vector3i _blockPos, Quaternion _rotation, ExplosionData _explosionData, int _entityId, uint _explId, ItemValue _itemValueExplosive, List<BlockChangeInfo> explosionChanges, GameObject particle)
	{
		clrIdx = _clrIdx;
		worldPos = _worldPos;
		blockPos = _blockPos;
		rotation = _rotation;
		explosionData = _explosionData;
		entityId = _entityId;
		explosionId = _explId;
		itemValueExplosive = null;
		if(_itemValueExplosive != null)
			itemValueExplosive = _itemValueExplosive.Clone();
		this.explosionChanges.Clear();
        this.explosionChanges.AddRange(explosionChanges);
		if(particle != null)
        {
			if(particle.TryGetComponent<NetSyncHelper>(out var helper))
			{
				MemoryStream memoryStream = new MemoryStream();
				using (PooledBinaryWriter _bw = MemoryPools.poolBinaryWriter.AllocSync(false))
				{
					_bw.SetBaseStream(memoryStream);
					helper.OnExplosionServerInit(_bw);
				}
				dataToSync = memoryStream.ToArray();
			}
        }
		return this;
	}

	public override void read(PooledBinaryReader _br)
	{
		clrIdx = (int)_br.ReadUInt16();
		worldPos = StreamUtils.ReadVector3(_br);
		blockPos = StreamUtils.ReadVector3i(_br);
		rotation = StreamUtils.ReadQuaterion(_br);
		int count = (int)_br.ReadUInt16();
		explosionData = new ExplosionData(_br.ReadBytes(count));
		entityId = _br.ReadInt32();
		explosionId = _br.ReadUInt32();
		int num = (int)_br.ReadUInt16();
		explosionChanges = new List<BlockChangeInfo>(num);
		for (int i = 0; i < num; i++)
		{
			BlockChangeInfo blockChangeInfo = new BlockChangeInfo();
			blockChangeInfo.Read(_br);
			explosionChanges.Add(blockChangeInfo);
		}
		if (_br.ReadBoolean())
		{
			itemValueExplosive = new ItemValue();
			itemValueExplosive.Read(_br);
		}
		ushort bytes = _br.ReadUInt16();
		if (bytes > 0)
			dataToSync = _br.ReadBytes(bytes);
	}

	public override void write(PooledBinaryWriter _bw)
	{
		base.write(_bw);
		_bw.Write((ushort)clrIdx);
		StreamUtils.Write(_bw, worldPos);
		StreamUtils.Write(_bw, blockPos);
		StreamUtils.Write(_bw, rotation);
		byte[] array = explosionData.ToByteArray();
		_bw.Write((ushort)array.Length);
		_bw.Write(array);
		_bw.Write(entityId);
		_bw.Write(explosionId);
		_bw.Write((ushort)explosionChanges.Count);
		for (int i = 0; i < explosionChanges.Count; i++)
		{
			explosionChanges[i].Write(_bw);
		}
		_bw.Write(itemValueExplosive != null);
		if (itemValueExplosive != null)
		{
			itemValueExplosive.Write(_bw);
		}
		if (dataToSync != null && dataToSync.Length > 0)
		{
			_bw.Write((ushort)dataToSync.Length);
			_bw.Write(dataToSync);
		}
		else
			_bw.Write((ushort)0);
	}

	public override void ProcessPackage(World _world, GameManager _callbacks)
	{
		if (_world == null)
		{
			return;
		}
		bool isCustom = false;
		if(explosionData.ParticleIndex >= WorldStaticData.prefabExplosions.Length)
        {
			isCustom = CustomExplosionManager.GetCustomParticleComponents(explosionData.ParticleIndex, out ExplosionComponent component) && component != null;
			if (isCustom)
			{
				ExplosionValue value = new ExplosionValue()
				{
					Component = component,
					CurrentExplosionParams = new ExplosionParams(clrIdx, worldPos, blockPos, rotation, explosionData, entityId, explosionId),
					CurrentItemValue = itemValueExplosive?.Clone()
				};
				CustomExplosionManager.PushLastInitComponent(value);
			}
        }

		GameObject result = _callbacks.ExplosionClient(clrIdx, worldPos, rotation, explosionData.ParticleIndex, explosionData.BlastPower, (float)explosionData.EntityRadius, (float)explosionData.BlockDamage, entityId, explosionChanges);
		if (isCustom)
		{
			NetSyncHelper helper = result?.GetComponent<NetSyncHelper>();
			if (helper != null && dataToSync != null)
			{
				using (PooledBinaryReader _br = MemoryPools.poolBinaryReader.AllocSync(false))
				{
					_br.SetBaseStream(new MemoryStream(dataToSync));
					helper.OnExplosionClientInit(_br);
				}
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
		return 80 + explosionChanges.Count * 30;
	}

	private int clrIdx;
	private Vector3 worldPos;
	private Vector3i blockPos;
	private Quaternion rotation;
	private ExplosionData explosionData;
	private int entityId;
	private uint explosionId;
	private ItemValue itemValueExplosive;
	private List<BlockChangeInfo> explosionChanges = new List<BlockChangeInfo>();
	private byte[] dataToSync = null;
}

