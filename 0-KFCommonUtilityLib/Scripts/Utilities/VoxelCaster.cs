using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public static class VoxelCaster
    {
        public readonly struct HitInfo
        {
            public readonly int hitIndex;
            public readonly bool isBlock;

            public HitInfo(int hitIndex, bool isBlock)
            {
                this.hitIndex = hitIndex;
                this.isBlock = isBlock;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private readonly struct HitMaskFlags
        {
            public const int Hit_Voxels = 1;
            public const int Hit_MovementCollide = 0x40;
            public const int Hit_NonMovementCollide = 4;
            public const int Hit_Water = 2;
            public const int Hit_MeleeCollide = 0x80;
            public const int Hit_RocketCollide = 0x10;
            public const int Hit_ArrowCollide = 0x20;
            public const int Hit_BulletCollide = 8;

            
            public readonly bool hitVoxels;
            public readonly bool hitMovementCollide;
            public readonly bool hitNonMovementCollide;
            public readonly bool hitWater;
            public readonly bool hitMeleeCollide;
            public readonly bool hitRocketCollide;
            public readonly bool hitArrowCollide;
            public readonly bool hitBulletCollide;
            public HitMaskFlags(int hitMask)
            {
                hitVoxels = (hitMask & Hit_Voxels) != 0;
                hitMovementCollide = (hitMask & Hit_MovementCollide) != 0;
                hitNonMovementCollide = (hitMask & Hit_NonMovementCollide) != 0;
                hitWater = (hitMask & Hit_Water) != 0;
                hitMeleeCollide = (hitMask & Hit_MeleeCollide) != 0;
                hitRocketCollide = (hitMask & Hit_RocketCollide) != 0;
                hitArrowCollide = (hitMask & Hit_ArrowCollide) != 0;
                hitBulletCollide = (hitMask & Hit_BulletCollide) != 0;
            }

            public readonly bool CanCollideWithBlock(Block block, bool blockIsSeeThrough, bool isOnlyWater)
            {
                return (hitMovementCollide && block.IsCollideMovement && (hitVoxels || !blockIsSeeThrough))
                    || (hitNonMovementCollide && !block.IsCollideMovement && !isOnlyWater)
                    || (hitBulletCollide && block.IsCollideBullets)
                    || (hitRocketCollide && block.IsCollideRockets)
                    || (hitArrowCollide && block.IsCollideArrows)
                    || (hitMeleeCollide && block.IsCollideMelee)
                    || (hitWater && isOnlyWater)
                    || (hitVoxels && blockIsSeeThrough);
            }
        }

        public readonly struct CastBound
        {
            public readonly Vector3i min;
            public readonly Vector3i max;

            public CastBound(Vector3i min, Vector3i max)
            {
                this.min = min;
                this.max = max;
            }
            public readonly bool Contains(Vector3i p)
            {
                return p.x >= min.x && p.x <= max.x &&
                       p.y >= min.y && p.y <= max.y &&
                       p.z >= min.z && p.z <= max.z;
            }

        }

        private static Vector3 RealOrigin;
        private static Vector3 CastDirection;
        private static readonly RaycastHit[] raycastHitsCache = new RaycastHit[100];

        public static IEnumerable<HitInfo> BoxCastAll(World _world, Vector3 origin, Quaternion orientation, Vector3 halfExtents, float distance, int _layerMask, int _hitMask)
        {
            HitMaskFlags hitMaskFlags = new HitMaskFlags(_hitMask);

            CastDirection = orientation * Vector3.forward;
            RealOrigin = origin - Origin.position - CastDirection * halfExtents.z;
            Voxel.voxelRayHitInfo.Clear();
            int hitCount = Physics.BoxCastNonAlloc(RealOrigin, halfExtents, CastDirection, raycastHitsCache, orientation, distance, _layerMask);

            return CheckHits(_world, _layerMask, _hitMask, hitMaskFlags, hitCount);
        }

        private static IEnumerable<HitInfo> CheckHits(World _world, int _layerMask, int _hitMask, HitMaskFlags hitMaskFlags, int hitCount)
        {
            HitInfoDetails.VoxelData lastHitData = default(HitInfoDetails.VoxelData);
            Array.Sort(raycastHitsCache, 0, hitCount, new RaycastHitComparer());
            for (int i = 0; i < hitCount; i++)
            {
                Voxel.voxelRayHitInfo.Clear();
                Voxel.phyxRaycastHit = raycastHitsCache[i];
                Ray hitRay = new Ray(Voxel.phyxRaycastHit.point + Origin.position - CastDirection * Voxel.phyxRaycastHit.distance, CastDirection);
                Transform hitTransform = Voxel.phyxRaycastHit.collider.transform;
                Vector3 hitOrigin = Voxel.phyxRaycastHit.point + Origin.position;
                string hitTag = Voxel.phyxRaycastHit.collider.transform.tag;

                Voxel.voxelRayHitInfo.ray = hitRay;
                Voxel.voxelRayHitInfo.hitCollider = Voxel.phyxRaycastHit.collider;
                Voxel.voxelRayHitInfo.hitTriangleIdx = Voxel.phyxRaycastHit.triangleIndex;
                if (hitTag == "T_Block")
                {
                    GameUtils.FindMasterBlockForEntityModelBlock(_world, CastDirection, hitTag, hitOrigin, hitTransform, Voxel.voxelRayHitInfo);
                    hitTag = "B_Mesh";
                    if (Voxel.voxelRayHitInfo.fmcHit.voxelData.IsOnlyAir())
                    {
                        Voxel.voxelRayHitInfo.fmcHit = Voxel.voxelRayHitInfo.hit;
                        Voxel.voxelRayHitInfo.fmcHit.pos = hitOrigin - CastDirection * 0.01f;
                    }
                }
                else if (hitTag == "T_Deco")
                {
                    if (DecoManager.Instance.GetParentBlockOfDecoration(hitTransform, out Voxel.voxelRayHitInfo.hit.blockPos, out var _decoObject))
                    {
                        Voxel.voxelRayHitInfo.hit.voxelData = HitInfoDetails.VoxelData.GetFrom(_world, Voxel.voxelRayHitInfo.hit.blockPos);
                        Voxel.voxelRayHitInfo.hit.pos = hitOrigin - hitRay.direction * 0.1f;
                        Voxel.voxelRayHitInfo.hit.distanceSq = ((hitOrigin != Vector3.zero) ? (hitRay.origin - hitOrigin).sqrMagnitude : float.MaxValue);
                        if (Voxel.voxelRayHitInfo.hit.voxelData.IsOnlyAir())
                        {
                            BlockValue bv = _decoObject.bv;
                            bv.damage = _decoObject.bv.Block.MaxDamage - 1;
                            Voxel.voxelRayHitInfo.hit.voxelData.Set(bv, Voxel.voxelRayHitInfo.hit.voxelData.WaterValue);
                        }

                        Voxel.voxelRayHitInfo.fmcHit = Voxel.voxelRayHitInfo.hit;
                    }
                }
                else
                {
                    if (!GameUtils.IsBlockOrTerrain(hitTag))
                    {
                        Voxel.voxelRayHitInfo.transform = hitTransform;
                        Voxel.voxelRayHitInfo.tag = hitTag;
                        Voxel.voxelRayHitInfo.bHitValid = true;
                        Voxel.voxelRayHitInfo.hit.pos = hitOrigin;
                        Voxel.voxelRayHitInfo.hit.distanceSq = ((hitOrigin != Vector3.zero) ? (hitRay.origin - hitOrigin).sqrMagnitude : float.MaxValue);
                        Voxel.voxelRayHitInfo.fmcHit = Voxel.voxelRayHitInfo.hit;
                        yield return new HitInfo(i, false);
                    }

                    Voxel.terrainMeshHit(_world, CastDirection, hitTag, hitOrigin, lastHitData, _layerMask, _hitMask);
                    if (Voxel.voxelRayHitInfo.fmcHit.voxelData.IsOnlyAir())
                    {
                        Voxel.voxelRayHitInfo.fmcHit = Voxel.voxelRayHitInfo.hit;
                        Voxel.voxelRayHitInfo.fmcHit.blockPos = Voxel.voxelRayHitInfo.lastBlockPos;
                        Voxel.voxelRayHitInfo.fmcHit.pos = hitOrigin - CastDirection * 0.01f;
                    }
                }

                lastHitData = Voxel.voxelRayHitInfo.hit.voxelData;
                Block block = Voxel.voxelRayHitInfo.hit.blockValue.Block;
                bool isOnlyWater = Voxel.voxelRayHitInfo.hit.voxelData.IsOnlyWater();
                if (isOnlyWater && Voxel.voxelRayHitInfo.fmcHit.voxelData.IsOnlyAir())
                {
                    Voxel.voxelRayHitInfo.fmcHit.blockPos = Voxel.voxelRayHitInfo.hit.blockPos;
                    Voxel.voxelRayHitInfo.fmcHit.voxelData = Voxel.voxelRayHitInfo.hit.voxelData;
                    Voxel.voxelRayHitInfo.fmcHit.blockFace = BlockFace.Top;
                    Voxel.voxelRayHitInfo.fmcHit.pos = hitOrigin;
                }

                bool blockIsSeeThrough = block.IsSeeThrough(_world, Voxel.voxelRayHitInfo.hit.clrIdx, Voxel.voxelRayHitInfo.hit.blockPos, Voxel.voxelRayHitInfo.hit.blockValue);
                // add a bound check to filter the mysterious underground hits
                if (hitMaskFlags.CanCollideWithBlock(block, blockIsSeeThrough, isOnlyWater))
                {
                    //if (!block.isMultiBlock && !blockBoundCheck.Contains(Voxel.voxelRayHitInfo.hit.blockPos))
                    if (Voxel.phyxRaycastHit.point == Vector3.zero)
                    {
                        //Log.Out($"VoxelCaster: Skipping block {block.GetBlockName()} hit outside of cast bound at {Voxel.voxelRayHitInfo.hit.blockPos} world pos {Voxel.voxelRayHitInfo.hit.pos} physics pos {raycastHitsCache[i].point}");
                        continue;
                    }

                    Voxel.voxelRayHitInfo.tag = hitTag;
                    Voxel.voxelRayHitInfo.bHitValid = true;
                    Voxel.voxelRayHitInfo.hit.pos = hitOrigin;
                    Voxel.voxelRayHitInfo.hit.distanceSq = ((hitOrigin != Vector3.zero) ? (Voxel.voxelRayHitInfo.ray.origin - hitOrigin).sqrMagnitude : float.MaxValue);
                    yield return new HitInfo(i, true);
                }
                lastHitData.Clear();
            }
        }

        public static bool SelectEntityHitAsCurrent(int hitIndex)
        {
            if (hitIndex < 0 || hitIndex >= raycastHitsCache.Length)
            {
                return false;
            }
            RaycastHit raycastHit = raycastHitsCache[hitIndex];
            if (raycastHit.collider.transform.tag.StartsWith("E_"))
            {
                Voxel.voxelRayHitInfo.Clear();
                Voxel.phyxRaycastHit = raycastHit;
                Ray hitRay = new Ray(Voxel.phyxRaycastHit.point + Origin.position - CastDirection * Voxel.phyxRaycastHit.distance, CastDirection);
                Transform hitTransform = Voxel.phyxRaycastHit.collider.transform;
                Vector3 hitOrigin = Voxel.phyxRaycastHit.point + Origin.position;
                string hitTag = Voxel.phyxRaycastHit.collider.transform.tag;

                Voxel.voxelRayHitInfo.ray = hitRay;
                Voxel.voxelRayHitInfo.hitCollider = Voxel.phyxRaycastHit.collider;
                Voxel.voxelRayHitInfo.hitTriangleIdx = Voxel.phyxRaycastHit.triangleIndex;
                Voxel.voxelRayHitInfo.transform = hitTransform;
                Voxel.voxelRayHitInfo.tag = hitTag;
                Voxel.voxelRayHitInfo.bHitValid = true;
                Voxel.voxelRayHitInfo.hit.pos = hitOrigin;
                Voxel.voxelRayHitInfo.hit.distanceSq = ((hitOrigin != Vector3.zero) ? (hitRay.origin - hitOrigin).sqrMagnitude : float.MaxValue);
                Voxel.voxelRayHitInfo.fmcHit = Voxel.voxelRayHitInfo.hit;
                return true;
            }

            return false;
        }

        public static CastBound GetCastBound(Vector3 center, Vector3 halfExtent, Quaternion rotation)
        {
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;

            Vector3 aabbHalf = new Vector3(
                Mathf.Abs(right.x) * halfExtent.x +
                Mathf.Abs(up.x) * halfExtent.y +
                Mathf.Abs(forward.x) * halfExtent.z,

                Mathf.Abs(right.y) * halfExtent.x +
                Mathf.Abs(up.y) * halfExtent.y +
                Mathf.Abs(forward.y) * halfExtent.z,

                Mathf.Abs(right.z) * halfExtent.x +
                Mathf.Abs(up.z) * halfExtent.y +
                Mathf.Abs(forward.z) * halfExtent.z
            );

            Vector3 minF = center - aabbHalf;
            Vector3 maxF = center + aabbHalf;

            return new CastBound
            (
                new Vector3i(
                    Mathf.FloorToInt(minF.x),
                    Mathf.FloorToInt(minF.y),
                    Mathf.FloorToInt(minF.z)
                ),
                new Vector3i(
                    Mathf.CeilToInt(maxF.x),
                    Mathf.CeilToInt(maxF.y),
                    Mathf.CeilToInt(maxF.z)
                )
            );
        }

        private struct RaycastHitComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit x, RaycastHit y)
            {
                return Vector3.Dot(x.point - RealOrigin, CastDirection).CompareTo(Vector3.Dot(y.point - RealOrigin, CastDirection));
            }
        }
    }
}
