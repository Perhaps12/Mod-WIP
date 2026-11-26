using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	public class EokaBlastProjectile : ModProjectile
	{
		private const float Range = 360f;
		private const float DamageFalloffStart = 9f * 16f;
		private const float HalfAngle = MathHelper.Pi / 6f;
		public const float MuzzleOffset = 20f;
		private static readonly SoundStyle KillSound = new SoundStyle("Wprint/Sounds/EokaKill")
		{
			Volume = 0.7f,
			Pitch = -0.15f
		};
		private bool killSoundPlayed;

		public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Bullet}";

		public override void SetDefaults()
		{
			Projectile.width = 1;
			Projectile.height = 1;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Ranged;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 2;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
		}

		public override bool ShouldUpdatePosition() => false;

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			Vector2 direction = Projectile.velocity;
			if (direction.LengthSquared() <= 0.001f)
			{
				direction = Vector2.UnitX;
			}
			else
			{
				direction.Normalize();
			}

			Vector2 playerOrigin = Projectile.Center - direction * MuzzleOffset;
			float distance = Vector2.Distance(playerOrigin, target.Center);
			if (distance <= DamageFalloffStart)
			{
				return;
			}

			float falloffProgress = MathHelper.Clamp(
				(distance - DamageFalloffStart) / (Range - DamageFalloffStart),
				0f,
				1f);
			modifiers.SourceDamage *= 1f - falloffProgress;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (killSoundPlayed || target.life > 0)
			{
				return;
			}

			killSoundPlayed = true;
			if (Projectile.owner == Main.myPlayer && Main.netMode != NetmodeID.Server)
			{
				SoundEngine.PlaySound(KillSound, target.Center);
			}
		}

		public override void AI()
		{
			if (Projectile.localAI[0] != 0f || Main.dedServ)
			{
				return;
			}

			Projectile.localAI[0] = 1f;
			Vector2 direction = Projectile.velocity;
			if (direction.LengthSquared() <= 0.001f)
			{
				direction = Vector2.UnitX;
			}
			else
			{
				direction.Normalize();
			}

			for (int i = 0; i < 80; i++)
			{
				float angle = direction.ToRotation() + Main.rand.NextFloat(-HalfAngle, HalfAngle);
				Vector2 dustPosition = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(12f, Range);
				Dust dust = Dust.NewDustDirect(
					dustPosition - new Vector2(4f),
					8,
					8,
					DustID.Smoke,
					0f,
					0f,
					100,
					Color.Black,
					Main.rand.NextFloat(1f, 1.7f));
				dust.velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);
				dust.noGravity = true;
			}
			for (int i = 0; i < 40; i++)
			{
				float angle = direction.ToRotation() + Main.rand.NextFloat(-HalfAngle, HalfAngle);
				Vector2 dustPosition = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(12f, Range);
				Dust dust = Dust.NewDustDirect(
					dustPosition - new Vector2(4f),
					8,
					8,
					DustID.FlameBurst,
					0f,
					0f,
					100,
					default,
					Main.rand.NextFloat(1f, 1.7f));
				dust.velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);
				dust.noGravity = true;
			}
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Vector2 direction = Projectile.velocity;
			if (direction.LengthSquared() <= 0.001f)
			{
				direction = Vector2.UnitX;
			}
			else
			{
				direction.Normalize();
			}

			Vector2 targetCenter = new Vector2(
				targetHitbox.X + targetHitbox.Width * 0.5f,
				targetHitbox.Y + targetHitbox.Height * 0.5f);
			Vector2 toTarget = targetCenter - Projectile.Center;
			float distance = toTarget.Length();
			float targetRadius = Math.Max(targetHitbox.Width, targetHitbox.Height) * 0.5f;
			if (distance > Range + targetRadius)
			{
				return false;
			}

			if (distance <= targetRadius)
			{
				return true;
			}

			float targetAngle = toTarget.ToRotation();
			float directionAngle = direction.ToRotation();
			float angleDifference = Math.Abs(MathHelper.WrapAngle(targetAngle - directionAngle));
			float targetAngleAllowance = (float)Math.Asin(Math.Min(1f, targetRadius / distance));
			return angleDifference <= HalfAngle + targetAngleAllowance;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			return false;
		}
	}
}
