using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Projectiles
{
	public class OneManBandGlobalProjectile : GlobalProjectile
	{
		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (!projectile.sentry && !ProjectileID.Sets.SentryShot[projectile.type])
				return;

			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;

			Player owner = Main.player[projectile.owner];
			if (!owner.active || !owner.GetModPlayer<OneManBandPlayer>().OneManBandEquipped)
				return;

			modifiers.SourceDamage *= 1.08f;
		}
	}
}
