using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Items;

namespace Wprint.Content.Projectiles
{
	public class EnergySwordCharge : ModProjectile
	{
		private const float ChargeTicksPerScale = 120f;

		public override string Texture => $"Terraria/Images/Item_{ItemID.Excalibur}";

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 2;
		}

		public override bool? CanDamage() => false;

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead || owner.HeldItem.type != ModContent.ItemType<EnergySword>())
			{
				Projectile.Kill();
				return;
			}

			if (owner.channel)
			{
				Projectile.timeLeft = 2;
				Projectile.ai[1]++;
				Projectile.Center = owner.MountedCenter;
				Projectile.rotation = owner.direction == 1 ? 0f : MathHelper.Pi;
				owner.heldProj = Projectile.whoAmI;
				owner.itemTime = 2;
				owner.itemAnimation = 2;
				owner.SetCompositeArmFront(
					true,
					Player.CompositeArmStretchAmount.Full,
					Projectile.rotation - MathHelper.PiOver2);

				if (!Main.dedServ)
				{
					float lightStrength = MathHelper.Clamp(0.2f + Projectile.ai[1] / 600f, 0.2f, 1.2f);
					Vector2 lightPosition = Projectile.Center + Projectile.rotation.ToRotationVector2() * 40f;
					Lighting.AddLight(lightPosition, 0.7f * lightStrength, 0.48f * lightStrength, 0.12f * lightStrength);
				}

				return;
			}

			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				float chargeScale = 1f + (float)Math.Sqrt(Projectile.ai[1] / ChargeTicksPerScale);
				float sweepDirection = owner.direction;
				Vector2 swingDirection = new Vector2(owner.direction, 0f);
				Projectile.NewProjectile(
					Projectile.GetSource_FromThis(),
					owner.MountedCenter,
					swingDirection,
					ModContent.ProjectileType<EnergySwordSwing>(),
					Projectile.damage,
					Projectile.knockBack,
					Projectile.owner,
					sweepDirection,
					Projectile.ai[0],
					chargeScale);
			}

			Projectile.Kill();
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Player owner = Main.player[Projectile.owner];
			Texture2D sword = TextureAssets.Item[ItemID.Excalibur].Value;
			Vector2 drawPosition = owner.MountedCenter - Main.screenPosition;
			Vector2 origin = new Vector2(0f, sword.Height);
			float swordAngle = Projectile.rotation + MathHelper.PiOver4;
			float glow = 0.2f + MathHelper.Clamp(Projectile.ai[1] / ChargeTicksPerScale, 0f, 1f) * 0.8f;
			Color glowColor = Color.Lerp(Color.Yellow, Color.Goldenrod, 0.25f) with { A = 0 };

			Main.EntitySpriteDraw(
				sword,
				drawPosition,
				null,
				glowColor * glow,
				swordAngle,
				origin,
				Projectile.ai[0] * 1.2f,
				SpriteEffects.None);
			Main.EntitySpriteDraw(
				sword,
				drawPosition,
				null,
				lightColor,
				swordAngle,
				origin,
				Projectile.ai[0],
				SpriteEffects.None);
			return false;
		}
	}
}
