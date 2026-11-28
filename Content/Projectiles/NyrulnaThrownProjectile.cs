using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Items;

namespace Wprint.Content.Projectiles
{
	public class NyrulnaThrownProjectile : ModProjectile
	{
		private const float MaximumRange = 30f * 16f;
		private const float CatchDistance = 25f;
		private const float SpriteForwardOffset = MathHelper.PiOver4;
		// The Resonance Scepter has no ItemID constant in this tModLoader version.
		private const int ResonanceScepterItemType = 5065;
		private static readonly SoundStyle ImpactSound = new SoundStyle("Wprint/Sounds/NyrulnaImpact");

		private bool Returning
		{
			get => Projectile.ai[0] == 1f;
			set => Projectile.ai[0] = value ? 1f : 0f;
		}

		private bool ImpactHandled
		{
			get => Projectile.ai[2] == 1f;
			set => Projectile.ai[2] = value ? 1f : 0f;
		}

		public override string Texture => $"Terraria/Images/Item_{ItemID.TitaniumTrident}";

		public override void SetDefaults()
		{
			Projectile.width = 30;
			Projectile.height = 30;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 600;
		}

		public override void AI()
		{
			Projectile.localAI[0] = Projectile.Center.X;
			Projectile.localAI[1] = Projectile.Center.Y;
			Projectile.rotation = Projectile.velocity.ToRotation() + SpriteForwardOffset;

			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead)
			{
				Projectile.Kill();
				return;
			}

			if (Returning)
			{
				Projectile.tileCollide = false;
				Vector2 toOwner = owner.MountedCenter - Projectile.Center;
				if (toOwner.LengthSquared() <= CatchDistance * CatchDistance)
				{
					Projectile.Kill();
					return;
				}

				Projectile.velocity = toOwner.SafeNormalize(Vector2.UnitX) * Nyrulna.ThrowSpeed;
				return;
			}

			Projectile.ai[1] += Projectile.velocity.Length();
			if (Projectile.ai[1] > MaximumRange)
				BeginReturn();
		}

		public override bool? CanDamage() => !Returning && !ImpactHandled;

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (Returning || ImpactHandled)
				return false;

			Vector2 previousCenter = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
			float collisionPoint = 0f;
			return Collision.CheckAABBvLineCollision(
				new Vector2(targetHitbox.X, targetHitbox.Y),
				new Vector2(targetHitbox.Width, targetHitbox.Height),
				previousCenter,
				Projectile.Center,
				System.Math.Max(Projectile.width, Projectile.height),
				ref collisionPoint);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			SpawnResonanceImpact();
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			SpawnResonanceImpact();
			return false;
		}

		private void SpawnResonanceImpact()
		{
			if (ImpactHandled)
				return;

			ImpactHandled = true;
			Vector2 impactCenter = Projectile.Center;
			if (Main.netMode != NetmodeID.Server)
				SoundEngine.PlaySound(ImpactSound, impactCenter);

			if (Projectile.owner == Main.myPlayer)
			{
				int projectileType = ContentSamples.ItemsByType[ResonanceScepterItemType].shoot;
				int index = Projectile.NewProjectile(
					Projectile.GetSource_FromThis(),
					impactCenter,
					Vector2.Zero,
					projectileType,
					Projectile.damage,
					Projectile.knockBack,
					Projectile.owner);

				if (index >= 0 && index < Main.maxProjectiles)
				{
					Projectile resonance = Main.projectile[index];
					resonance.friendly = true;
					resonance.hostile = false;
					resonance.DamageType = DamageClass.Melee;
					resonance.netUpdate = true;
				}
			}

			BeginReturn();
		}

		private void BeginReturn()
		{
			if (Returning)
				return;

			Returning = true;
			Projectile.tileCollide = false;
			Vector2 toOwner = Main.player[Projectile.owner].MountedCenter - Projectile.Center;
			Projectile.velocity = toOwner.SafeNormalize(Vector2.UnitX) * Nyrulna.ThrowSpeed;
			Projectile.netUpdate = true;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = TextureAssets.Item[ItemID.TitaniumTrident].Value;
			Main.EntitySpriteDraw(
				texture,
				Projectile.Center - Main.screenPosition,
				null,
				lightColor,
				Projectile.rotation,
				texture.Size() / 2f,
				Projectile.scale,
				SpriteEffects.None);
			return false;
		}
	}
}
