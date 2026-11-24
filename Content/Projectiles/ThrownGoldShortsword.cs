using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	public class ThrownGoldShortsword : ModProjectile
	{
		public override string Texture => $"Terraria/Images/Item_{ItemID.GoldShortsword}";

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 8;
			ProjectileID.Sets.TrailingMode[Type] = 0;
		}

		public override void SetDefaults()
		{
			Projectile.width = 28;
			Projectile.height = 28;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.Ranged;
			Projectile.penetrate = 5;
			Projectile.timeLeft = 600;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

			if (Main.netMode != NetmodeID.Server)
			{
				Lighting.AddLight(Projectile.Center, 1.4f, 1.05f, 0.3f);

				if (Main.rand.NextBool())
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
						1.6f);
					Main.dust[dustIndex].noGravity = true;
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Main.GetItemDrawFrame(ItemID.GoldShortsword, out Texture2D texture, out Rectangle frame);
			Vector2 origin = frame.Size() * 0.5f;
			Vector2 screenPosition = Main.screenPosition;

			for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
			{
				Vector2 oldPosition = Projectile.oldPos[i];
				if (oldPosition == Vector2.Zero)
				{
					continue;
				}

				float age = i / (float)Projectile.oldPos.Length;
				float opacity = (1f - age) * 0.55f;
				Color trailColor = Color.Lerp(Color.Gold, Color.OrangeRed, age) * opacity;
				Vector2 trailCenter = oldPosition + Projectile.Size * 0.5f - screenPosition;

				Main.EntitySpriteDraw(
					texture,
					trailCenter,
					frame,
					trailColor,
					Projectile.rotation,
					origin,
					Projectile.scale,
					SpriteEffects.None);
			}

			Vector2 center = Projectile.Center - screenPosition;
			Color glowColor = new Color(255, 190, 55, 0);
			Main.EntitySpriteDraw(
				texture,
				center,
				frame,
				glowColor * 0.75f,
				Projectile.rotation,
				origin,
				Projectile.scale * 1.5f,
				SpriteEffects.None);
			Main.EntitySpriteDraw(
				texture,
				center,
				frame,
				glowColor * 0.9f,
				Projectile.rotation,
				origin,
				Projectile.scale * 1.2f,
				SpriteEffects.None);
			Main.EntitySpriteDraw(
				texture,
				center,
				frame,
				Color.White,
				Projectile.rotation,
				origin,
				Projectile.scale,
				SpriteEffects.None);

			return false;
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			modifiers.DisableCrit();
		}
	}
}
