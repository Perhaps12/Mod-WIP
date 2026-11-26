using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	// A healing syringe that homes in on its owner and heals them on contact.
	// Was used by the Towncall Staff's Nurse; currently unused (kept for later).
	public class TrackingSyringe : ModProjectile
	{
		public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.PoisonDart}";

		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 120;
		}

		public override void AI()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
			{
				Projectile.Kill();
				return;
			}

			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead)
			{
				Projectile.Kill();
				return;
			}

			Vector2 toOwner = owner.Center - Projectile.Center;
			if (toOwner.LengthSquared() <= 20f * 20f)
			{
				if (Main.myPlayer == Projectile.owner)
					owner.Heal(40);
				Projectile.Kill();
				return;
			}

			Vector2 desiredVelocity = toOwner.SafeNormalize(Vector2.UnitY) * 13f;
			Projectile.velocity = (Projectile.velocity * 12f + desiredVelocity) / 13f;
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
		}
	}
}
