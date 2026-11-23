using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Buffs;
using Wprint.Content.NPCs;

namespace Wprint.Content.Projectiles
{
	public class LaevateinnStabProjectile : ModProjectile
	{
		private const float SwordReach = 96f;
		private const float BladeWidth = 24f;
		private const int StabDuration = 18;

		public override string Texture => $"Terraria/Images/Item_{ItemID.WoodenSword}";

		public override void SetDefaults()
		{
			Projectile.width = 24;
			Projectile.height = 24;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = 1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = StabDuration;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead)
			{
				Projectile.Kill();
				return;
			}

			Vector2 direction = Projectile.ai[1].ToRotationVector2();
			float progress = 1f - Projectile.timeLeft / (float)StabDuration;
			float extension = (float)Math.Sin(progress * Math.PI);
			Projectile.direction = direction.X >= 0f ? 1 : -1;
			Projectile.rotation = Projectile.ai[1] + MathHelper.PiOver4;
			Projectile.Center = owner.MountedCenter + direction * (SwordReach * extension * 0.5f);
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Player owner = Main.player[Projectile.owner];
			Vector2 direction = Projectile.ai[1].ToRotationVector2();
			float progress = 1f - Projectile.timeLeft / (float)StabDuration;
			float extension = (float)Math.Sin(progress * Math.PI);
			if (extension <= 0.05f)
			{
				return false;
			}

			Vector2 start = owner.MountedCenter + direction * 14f;
			Vector2 end = owner.MountedCenter + direction * (14f + SwordReach * extension);
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

			int burnDamage = target.GetGlobalNPC<LaevateinnBurnGlobalNPC>().GetRemainingDamage(target);
			modifiers.FlatBonusDamage += burnDamage;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				return;
			}

			LaevateinnBurnGlobalNPC burn = target.GetGlobalNPC<LaevateinnBurnGlobalNPC>();
			int remainingBurnDamage = burn.GetRemainingDamage(target);
			if (remainingBurnDamage > 0)
			{
				int burnBuffIndex = target.FindBuffIndex(ModContent.BuffType<LaevateinnBurnBuff>());
				if (burnBuffIndex >= 0)
				{
					target.DelBuff(burnBuffIndex);
				}
				burn.ClearBurn();
				target.netUpdate = true;

				Player owner = Main.player[Projectile.owner];
				int healAmount = Math.Min(remainingBurnDamage, damageDone);
				healAmount = Math.Min(healAmount, Math.Max(0, owner.statLifeMax2 - owner.statLife));
				if (healAmount > 0)
				{
					owner.Heal(healAmount);
				}
			}

			if (Main.netMode != NetmodeID.Server)
			{
				SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
			}
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
				2.35f,
				effects,
				0);
			return false;
		}
	}
}
