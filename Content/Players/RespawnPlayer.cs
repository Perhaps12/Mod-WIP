using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Wprint.Content.Players
{
	// Shortens the respawn timer after dying.
	public class RespawnPlayer : ModPlayer
	{
		const int RespawnSeconds = 3;

		public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
		{
			// respawnTimer is in ticks (60 per second). Vanilla sets it before this hook runs,
			// so overriding it here replaces the normal timer (including the longer one during boss fights).
			Player.respawnTimer = RespawnSeconds * 60;
		}
	}
}
