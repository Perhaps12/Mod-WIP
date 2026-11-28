using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Projectiles
{
	/// <summary>
	/// Applies One Man Band's damage bonus only to sentry bodies and projectiles registered as sentry shots.
	/// </summary>
	public class OneManBandGlobalProjectile : GlobalProjectile
	{
		/// <summary>
		/// Checks whether this projectile is a sentry attack from a player wearing One Man Band,
		/// then multiplies its source damage by 1.08 (an 8% increase).
		/// </summary>
		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			// SentryShot also covers the separate projectiles fired by sentries.
			if (!projectile.sentry && !ProjectileID.Sets.SentryShot[projectile.type])
				return;

			// Some projectiles have no player owner; those cannot receive this player's accessory bonus.
			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;

			// The owner flag is set only while the accessory is equipped.
			Player owner = Main.player[projectile.owner];
			if (!owner.active || !owner.GetModPlayer<OneManBandPlayer>().OneManBandEquipped)
				return;

			modifiers.SourceDamage *= 1.08f;
		}
	}
}
