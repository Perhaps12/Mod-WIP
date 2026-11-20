using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Buffs;
using Wprint.Content.NPCs;
using Wprint.Content.Players;

namespace Wprint.Content.Projectiles
{
	public class LaevateinnSwingProjectile : ModProjectile
	{
		private const float SwordReach = 122f;
		private const float BladeWidth = 30f;
		private const float SwingArc = MathHelper.Pi * 2f / 3f;
		private const int BurnDuration = 3 * 60;

		public override string Texture => $"Terraria/Images/Item_{ItemID.WoodenSword}";

		public override void SetDefaults()
		{
			Projectile.width = 24;
			Projectile.height = 24;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = LaevateinnPlayer.ChargeTicksPerSwing;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = LaevateinnPlayer.ChargeTicksPerSwing;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead)
			{
				Projectile.Kill();
				return;
			}

			bool isAuthoritative = Main.netMode != NetmodeID.MultiplayerClient;
			bool isOwnerClient = Main.myPlayer == owner.whoAmI;
			if (isAuthoritative || isOwnerClient)
			{
				if (Projectile.localAI[0] == 0f)
				{
					Projectile.localAI[0] = 1f;
					Projectile.ai[0] = owner.GetModPlayer<LaevateinnPlayer>().RegisterSwing();
					Projectile.netUpdate = true;
				}
			}

			float progress = 1f - Projectile.timeLeft / (float)LaevateinnPlayer.ChargeTicksPerSwing;
			float swingRotation = Projectile.ai[1] - SwingArc * 0.5f + SwingArc * progress;
			Vector2 direction = swingRotation.ToRotationVector2();
			Projectile.direction = direction.X >= 0f ? 1 : -1;
			Projectile.rotation = swingRotation + MathHelper.PiOver4;
			Projectile.Center = owner.MountedCenter + direction * (SwordReach * 0.5f);
		}

		public override bool? CanDamage()
		{
			return Projectile.ai[0] > 0f;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Player owner = Main.player[Projectile.owner];
			float progress = 1f - Projectile.timeLeft / (float)LaevateinnPlayer.ChargeTicksPerSwing;
			float swingRotation = Projectile.ai[1] - SwingArc * 0.5f + SwingArc * progress;
			Vector2 direction = swingRotation.ToRotationVector2();
			Vector2 start = owner.MountedCenter + direction * 16f;
			Vector2 end = owner.MountedCenter + direction * SwordReach;
			float collisionPoint = 0f;
			return Collision.CheckAABBvLineCollision(
				new Vector2(targetHitbox.X, targetHitbox.Y),
				new Vector2(targetHitbox.Width, targetHitbox.Height),
				start,
				end,
				BladeWidth,
				ref collisionPoint);
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			modifiers.SourceDamage *= Math.Max(1f, Math.Min(Projectile.ai[0], 1_000_000f));
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				return;
			}

			target.AddBuff(ModContent.BuffType<LaevateinnBurnBuff>(), BurnDuration);
			target.GetGlobalNPC<LaevateinnBurnGlobalNPC>().ApplyBurn(Projectile.ai[0]);
			target.netUpdate = true;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = TextureAssets.Item[ItemID.WoodenSword].Value;
			SpriteEffects effects = Projectile.direction < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
			Main.EntitySpriteDraw(
				texture,
				Projectile.Center - Main.screenPosition,
				null,
				lightColor,
				Projectile.rotation,
				new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
				2.7f,
				effects,
				0);
			return false;
		}
	}
}
