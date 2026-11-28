using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Projectiles
{
	public class EnergySwordSwing : ModProjectile
	{
		private const int SwingTicks = 22;
		private const float SwingCone = MathHelper.PiOver4;
		private const float SwingReach = 112f;

		public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Excalibur}";

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = SwingTicks;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.ownerHitCheck = true;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead)
			{
				Projectile.Kill();
				return;
			}

			Projectile.Center = owner.MountedCenter;
			Projectile.localAI[0]++;
			float progress = Projectile.localAI[0] / SwingTicks;
			float sweepDirection = Projectile.ai[0];
			float chargeScale = Projectile.ai[2];
			Projectile.rotation = Projectile.velocity.ToRotation()
				- sweepDirection * MathHelper.PiOver2
				+ sweepDirection * MathHelper.Pi * progress
				+ owner.fullRotation;

			if (Math.Abs(Projectile.velocity.X) > 0.01f)
			{
				owner.ChangeDir(Projectile.velocity.X > 0f ? 1 : -1);
			}

			Vector2 swordDirection = (Projectile.rotation + sweepDirection * SwingCone).ToRotationVector2();
			if (!Main.dedServ)
			{
				Color gaussColor = Color.Lerp(Color.Yellow, Color.Goldenrod, 0.25f);
				float lightPulse = 0.55f + 0.45f * (float)System.Math.Sin(MathHelper.Pi * progress);
				Vector2 lightPosition = Projectile.Center
					+ swordDirection * (SwingReach * Projectile.ai[1] * chargeScale * 0.55f);
				Vector3 light = Color.Lerp(gaussColor, Color.White, 0.3f).ToVector3()
					* 0.7f * Projectile.ai[1] * chargeScale * lightPulse;
				Lighting.AddLight(lightPosition, light);
			}

		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Projectile.ai[2] >= EnergySwordCharge.MaxSwingScale
				&& Main.netMode != NetmodeID.MultiplayerClient)
			{
				Main.player[Projectile.owner].GetModPlayer<EnergySwordPlayer>().StoreCharge();
			}
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			return targetHitbox.IntersectsConeSlowMoreAccurate(
				Projectile.Center,
				SwingReach * Projectile.ai[1] * Projectile.ai[2],
				Projectile.rotation,
				SwingCone);
		}

		public override void PostAI()
		{
			Player owner = Main.player[Projectile.owner];
			owner.heldProj = Projectile.whoAmI;
			owner.SetCompositeArmFront(
				true,
				Player.CompositeArmStretchAmount.Full,
				Projectile.rotation + Projectile.ai[0] * SwingCone - MathHelper.PiOver2);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			float progress = Projectile.localAI[0] / SwingTicks;
			float fade = Utils.Remap(progress, 0f, 0.55f, 0f, 1f)
				* Utils.Remap(progress, 0.55f, 1f, 1f, 0f);
			Vector2 drawPosition = Projectile.Center - Main.screenPosition;
			float chargeScale = Projectile.ai[2];
			float scale = Projectile.ai[1] * chargeScale * (112f / (110f / 1.4f));
			float sweepDirection = Projectile.ai[0];
			Texture2D slash = TextureAssets.Projectile[Type].Value;
			Rectangle body = slash.Frame(1, 4, 0, 0);
			Rectangle edge = slash.Frame(1, 4, 0, 3);
			Vector2 origin = body.Size() * 0.5f;
			SpriteEffects effects = sweepDirection >= 0f
				? SpriteEffects.None
				: SpriteEffects.FlipVertically;

			Color gaussGlow = Color.Lerp(Color.Yellow, Color.Goldenrod, 0.25f) with { A = 0 };
			Color whiteGlow = Color.White with { A = 0 };
			Main.EntitySpriteDraw(
				slash,
				drawPosition,
				body,
				gaussGlow * fade,
				Projectile.rotation,
				origin,
				scale * 1.08f,
				effects);
			Main.EntitySpriteDraw(
				slash,
				drawPosition,
				body,
				whiteGlow * fade * 0.7f,
				Projectile.rotation,
				origin,
				scale * 0.88f,
				effects);
			Main.EntitySpriteDraw(
				slash,
				drawPosition,
				edge,
				whiteGlow * fade,
				Projectile.rotation + sweepDirection * 0.02f,
				origin,
				scale,
				effects);

			Texture2D sword = TextureAssets.Item[ItemID.Excalibur].Value;
			float swordAngle = Projectile.rotation + sweepDirection * SwingCone + MathHelper.PiOver4;
			Main.EntitySpriteDraw(
				sword,
				drawPosition,
				null,
				Color.White,
				swordAngle,
				new Vector2(0f, sword.Height),
				Projectile.ai[1],
				SpriteEffects.None);

			return false;
		}
	}
}
