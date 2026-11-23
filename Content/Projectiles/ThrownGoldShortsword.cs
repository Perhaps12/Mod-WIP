using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	public class ThrownGoldShortsword : ModProjectile
	{
		public override string Texture => $"Terraria/Images/Item_{ItemID.GoldShortsword}";

		public override void SetDefaults()
		{
			Projectile.width = 28;
			Projectile.height = 28;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.Ranged;
			Projectile.penetrate = 3;
			Projectile.timeLeft = 600;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

			if (Main.netMode != NetmodeID.Server)
			{
				Lighting.AddLight(Projectile.Center, 0.7f, 0.5f, 0.1f);

				if (Main.rand.NextBool(2))
				{
					int dustIndex = Dust.NewDust(
						Projectile.position,
						Projectile.width,
						Projectile.height,
						DustID.GoldFlame,
						-Projectile.velocity.X * 0.1f,
						-Projectile.velocity.Y * 0.1f,
						100,
						Color.Gold,
						1.2f);
					Main.dust[dustIndex].noGravity = true;
				}
			}
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			modifiers.DisableCrit();
		}
	}
}
