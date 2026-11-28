using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Players
{
	/// <summary>
	/// Tracks the shared whistle cooldown and performs the owner-side sentry teleport.
	/// </summary>
	public class SentryWhistlePlayer : ModPlayer
	{
		// Remaining cooldown in ticks; both whistle items read and update this same value.
		private int _cooldownTicks;

		// Items can only be used when the timer is clear and this player owns an active sentry.
		public bool CanUseWhistle => _cooldownTicks <= 0 && HasOwnedSentries();

		// Decrement once per game tick and notify the local owner exactly when the cooldown ends.
		public override void PostUpdate()
		{
			if (_cooldownTicks <= 0)
				return;

			_cooldownTicks--;
			if (_cooldownTicks == 0 && Player.whoAmI == Main.myPlayer)
				SoundEngine.PlaySound(SoundID.Item4, Player.Center);
		}

		/// <summary>
		/// Moves all active sentry projectiles owned by this player to the cursor and starts the cooldown.
		/// Returns false when called by a non-owner or when there are no sentries to move.
		/// </summary>
		public bool TeleportSentries(int cooldownTicks)
		{
			if (Player.whoAmI != Main.myPlayer || !HasOwnedSentries())
				return false;

			// MouseWorld belongs to the local player; clamp it so sentries cannot be placed out of reach.
			Vector2 destination = Main.MouseWorld;
			Player.LimitPointToPlayerReachableArea(ref destination);

			// The owning client updates its projectiles and asks tModLoader to sync their new positions.
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile projectile = Main.projectile[i];
				if (!projectile.active || !projectile.sentry || projectile.owner != Player.whoAmI)
					continue;

				projectile.Center = destination;
				projectile.velocity = Vector2.Zero;
				projectile.netUpdate = true;
			}

			// Start the shared cooldown only after a teleport can actually occur.
			_cooldownTicks = cooldownTicks;
			return true;
		}

		// Search the projectile pool for a live sentry owned by this player.
		private bool HasOwnedSentries()
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile projectile = Main.projectile[i];
				if (projectile.active && projectile.sentry && projectile.owner == Player.whoAmI)
					return true;
			}

			return false;
		}
	}
}
