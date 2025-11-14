using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public enum ValueRefStatType
    {
        Value,
        Cvar,
        Metadata
    }

    public static class MiscExtensions
    {
        public static EntityPlayerLocal GetLocalPlayerInParent(this Component self)
        {
            if (!self)
            {
                return null;
            }
            var player = self.GetComponentInParent<EntityPlayerLocal>();
            if (player)
            {
                return player;
            }

            var graphBuilder = self.GetComponentInParent<AnimationGraphBuilder>();
            if (graphBuilder)
            {
                return graphBuilder.Player as EntityPlayerLocal;
            }

            var vp_camera = self.GetComponentInParent<vp_FPCamera>();
            if (vp_camera)
            {
                return vp_camera.FPController.localPlayer;
            }

            return null;
        }
    }
}

