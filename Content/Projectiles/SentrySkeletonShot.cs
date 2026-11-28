using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	/// <summary>
	/// Basic fast-moving projectile spawned by SentrySkeletonProjectile.
	/// </summary>
	public class SentrySkeletonShot : ModProjectile
	{
		// Use a vanilla bullet graphic until this example has a custom texture.
		public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Bullet}";

		// Mark this type so sentry-specific effects, such as One Man Band, recognize its hits.
		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.SentryShot[Type] = true; // Enables sentry-specific hit effects.
		}

		public override void SetDefaults()
		{
			// Configure a friendly summon projectile that hits once, dies after three seconds,
			// and collides with tiles.
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 180;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
		}
	}
}
