using System;
using LuaRuntimeProbe.Contracts;
using UnityEngine;

namespace LuaRuntimeProbe.Game
{
    /// <summary>
    /// Reads a small set of local-player and world fields.
    ///
    /// STRICT RULES:
    ///   * read-only: nothing in this file assigns to any game object;
    ///   * no world loaded => Available == false with an explicit reason string,
    ///     never fabricated zeroes;
    ///   * any exception is caught and surfaced as an unavailable reason so the
    ///     console command can never take the game down.
    /// </summary>
    public static class GameContextProvider
    {
        public static PlayerSnapshot Capture()
        {
            var snap = new PlayerSnapshot();

            try
            {
                snap.CapturedOnFrame = Time.frameCount;

                GameManager gm = GameManager.Instance;
                if (gm == null)
                {
                    return PlayerSnapshot.Unavailable("world_unavailable: GameManager.Instance is null (game not started)");
                }

                World world = gm.World;
                if (world == null)
                {
                    return PlayerSnapshot.Unavailable("world_unavailable: no world loaded (main menu or still loading)");
                }

                snap.WorldGuid = SafeWorldGuid(world);
                snap.WorldDay = world.WorldDay;
                snap.WorldHour = world.WorldHour;

                EntityPlayerLocal player = world.GetPrimaryPlayer();
                if (player == null)
                {
                    return PlayerSnapshot.Unavailable("player_unavailable: world is loaded but has no primary local player yet");
                }

                snap.Available = true;
                snap.Name = player.EntityName;
                snap.DisplayName = player.PlayerDisplayName;
                snap.Health = player.Health;
                snap.MaxHealth = player.GetMaxHealth();
                snap.Stamina = player.Stamina;
                snap.GameStage = player.gameStage;
                snap.IsDead = player.IsDead();
                snap.IsAdmin = player.IsAdmin;

                Vector3 pos = player.position;
                snap.X = pos.x;
                snap.Y = pos.y;
                snap.Z = pos.z;

                snap.CapturedOnFrame = Time.frameCount;
                return snap;
            }
            catch (Exception e)
            {
                return PlayerSnapshot.Unavailable(
                    "context_error: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static string SafeWorldGuid(World world)
        {
            try
            {
                return world.Guid;
            }
            catch (Exception e)
            {
                return "<guid unavailable: " + e.GetType().Name + ">";
            }
        }
    }
}
