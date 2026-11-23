using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Players
{
	// Per-player Clawline state:
	//  - ForcedVelocity: the hook sets this every tick it wants control of the player's movement.
	//    When it stops setting it, the player moves normally and keeps their velocity (momentum).
	//  - Air charges: only MaxAirCharges uses while airborne; touching the ground refills them.
	//  - Jump can't cancel the Clawline; once fired it always plays out.
	public class ClawlinePlayer : ModPlayer
	{
		public const int MaxAirCharges = 1;

		public Vector2? ForcedVelocity;
		public int AirCharges = MaxAirCharges;

		int HookType => ModContent.ProjectileType<ClawlineHook>();

		bool HookOut => Player.ownedProjectileCounts[HookType] > 0;

		// Standing on solid ground or a platform (not just "vertical speed is 0", which is also
		// true while the Clawline holds the player still in midair)
		public bool OnGround => IsOnGround(Player);

		// Shared with other items (e.g. the Blast Pack)
		public static bool IsOnGround(Player player) => player.velocity.Y == 0f && StandingOnTile(player);

		// Checks the row of tiles just below the player's feet for anything standable:
		// solid blocks or solid-top tiles (platforms, tables, etc. of any style)
		static bool StandingOnTile(Player player)
		{
			int y = (int)((player.Bottom.Y + 2f) / 16f);
			int left = (int)(player.position.X / 16f);
			int right = (int)((player.position.X + player.width - 1f) / 16f);
			for (int x = left; x <= right; x++)
			{
				if (!WorldGen.InWorld(x, y))
					continue;
				Tile tile = Main.tile[x, y];
				if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
					return true;
			}
			return false;
		}

		// Checked by both the item (hotbar use) and the hook (Grapple key)
		public bool CanUseClawline() => !HookOut && (OnGround || AirCharges > 0);

		// Called by the hook when it spawns. Firing from the ground uses the charge too, so the
		// pull can't lift you off the ground with a free air use still in hand.
		public void ConsumeCharge()
		{
			if (AirCharges > 0)
				AirCharges--;
		}

		// Called by the hook after a pull that latched onto a tile or an enemy
		public void Recharge()
		{
			AirCharges = MaxAirCharges;
		}

		public override void PreUpdateMovement()
		{
			if (ForcedVelocity is Vector2 v)
			{
				// While held still, vanilla thinks you're on the ground (vertical speed is 0), so a
				// Jump press would start a jump that kicks in after the pull. Clear it.
				Player.jump = 0;
				Player.velocity = v;                                   // overrides gravity for this tick
				Player.fallStart = (int)(Player.position.Y / 16f);     // no fall damage from clawline movement
				ForcedVelocity = null;
			}
		}

		public override void PostUpdate()
		{
			// Touching the ground recharges the air uses (only once the hook is gone, i.e. after the pull)
			if (!HookOut && OnGround)
				AirCharges = MaxAirCharges;
		}
	}
}
