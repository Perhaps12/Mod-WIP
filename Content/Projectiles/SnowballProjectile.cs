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
	public class SnowballProjectile : ModProjectile
	{
		public const float MaxSnowballSize = 3f;
		public const float ThrowDamageMultiplier = 1.5f;
		public const int HeldMode = 0;
		public const int ThrownMode = 1;
		private const int FragmentMode = 2;
		private const int BaseHitboxSize = 14;
		private const float BaseMovementDistancePerSize = 160f;
		private const float Gravity = 0.35f;
		private const float MaximumFallSpeed = 16f;
		private static readonly SoundStyle SnowballHitSound = SoundID.Item51;
		private static readonly SoundStyle SnowballBreakSound = SoundID.NPCHit11;


		public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.SnowBallFriendly}";

		public override void SetDefaults()
		{
			Projectile.width = BaseHitboxSize;
			Projectile.height = BaseHitboxSize;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.timeLeft = 300;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
		}

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			float size = MathHelper.Clamp(Projectile.ai[0], 1f, MaximumSnowballSize);
			SetSize(size);
			bool held = Projectile.ai[1] == HeldMode;
			Projectile.tileCollide = !held;
			Projectile.penetrate = held ? -1 : 1;
		}

		public override void AI()
		{
			if (Projectile.ai[1] == HeldMode)
			{
				FollowOwnerAndGrow();
				return;
			}

			Projectile.velocity.Y = MathHelper.Min(Projectile.velocity.Y + Gravity, MaximumFallSpeed);
			Projectile.rotation += Projectile.velocity.X * 0.04f;
		}

		private void FollowOwnerAndGrow()
		{
			Player owner = Main.player[Projectile.owner];
			Snowballer heldItem = owner.HeldItem.ModItem as Snowballer;
			if (!owner.active
				|| owner.dead
				|| heldItem == null
				|| System.Math.Abs(heldItem.SnowballSizeMultiplier - SizeMultiplier) >= 0.001f)
			{
				Projectile.Kill();
				return;
			}

			Projectile.velocity = Vector2.Zero;
			Projectile.tileCollide = false;

			if (Projectile.owner == Main.myPlayer)
			{
				float oldSize = Projectile.ai[0];
				float newSize = MathHelper.Min(
					MaximumSnowballSize,
					oldSize + System.Math.Abs(owner.velocity.X) / MovementDistancePerSize);
				if (newSize != oldSize)
				{
					SetSize(newSize);
					if (oldSize < MaximumSnowballSize && newSize >= MaximumSnowballSize)
					{
						SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = 0.25f }, owner.Center);
					}

					if (System.Math.Abs(newSize - Projectile.localAI[0]) >= 0.05f)
					{
						Projectile.localAI[0] = newSize;
						Projectile.netUpdate = true;
					}
				}
			}
			else
			{
				SetSize(Projectile.ai[0]);
			}

			SetHeldPosition(owner);
			Projectile.rotation += owner.velocity.X / (Projectile.width * 0.5f);
			Projectile.timeLeft = 2;
		}

		private void SetHeldPosition(Player owner)
		{
			Vector2 anchor = GetHeldAnchor(owner);
			Projectile.position = new Vector2(
				owner.direction > 0 ? anchor.X : anchor.X - Projectile.width,
				anchor.Y - Projectile.height);
		}

		private static Vector2 GetHeldAnchor(Player owner)
		{
			float x = owner.direction > 0
				? owner.position.X + owner.width + 2f
				: owner.position.X - 2f;
			return new Vector2(x, owner.position.Y + owner.height);
		}

		private float SizeMultiplier => Projectile.ai[2] >= 1f ? Projectile.ai[2] : 1f;
		private float MaximumSnowballSize => MaxSnowballSize * SizeMultiplier;
		private float MovementDistancePerSize =>
			BaseMovementDistancePerSize * (MaxSnowballSize - 1f) / (MaximumSnowballSize - 1f);

		private void SetSize(float size)
		{
			Projectile.ai[0] = size;
			Projectile.scale = size;
			int hitboxSize = (int)(BaseHitboxSize * size);
			if (Projectile.width != hitboxSize || Projectile.height != hitboxSize)
			{
				Projectile.Resize(hitboxSize, hitboxSize);
			}
		}

		public void Throw(Vector2 direction)
		{
			float size = Projectile.ai[0];
			Projectile.ai[1] = ThrownMode;
			Projectile.velocity = direction * ThrowSpeed(size);
			Projectile.tileCollide = true;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 300;
			Projectile.netUpdate = true;
		}

		public static Projectile FindHeldSnowball(Player player, float sizeMultiplier)
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile projectile = Main.projectile[i];
				float projectileSizeMultiplier = projectile.ai[2] >= 1f ? projectile.ai[2] : 1f;
				if (projectile.active
					&& projectile.owner == player.whoAmI
					&& projectile.type == ModContent.ProjectileType<SnowballProjectile>()
					&& projectile.ai[1] == HeldMode
					&& System.Math.Abs(projectileSizeMultiplier - sizeMultiplier) < 0.001f)
				{
					return projectile;
				}
			}

			return null;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			SpawnSnowImpactEffect(Projectile.Center);

			if (Projectile.ai[1] == FragmentMode)
			{
				return true;
			}

			if (Projectile.ai[1] == ThrownMode)
			{
				Split();
				Projectile.Kill();
				return false;
			}

			if (Projectile.velocity.Y != oldVelocity.Y)
			{
				Projectile.velocity.Y = 0f;
				return false;
			}

			return true;
		}

		public override bool? CanDamage()
		{
			if (Projectile.ai[1] != HeldMode)
			{
				return null;
			}

			Player owner = Main.player[Projectile.owner];
			return owner.active && owner.velocity.LengthSquared() > 0.01f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			Vector2 impactPosition = GetNPCImpactPosition(target);
			SpawnSnowImpactEffect(impactPosition);
			if (!Main.dedServ)
			{
				SoundEngine.PlaySound(SnowballHitSound, impactPosition);
			}

			if (Projectile.ai[1] == ThrownMode)
			{
				Split();
				Projectile.Kill();
			}
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			modifiers.SourceDamage *= Projectile.ai[0];
			if (Projectile.ai[1] == ThrownMode)
			{
				modifiers.SourceDamage *= ThrowDamageMultiplier;
			}
		}

		public override void OnKill(int timeLeft)
		{
			if (Projectile.ai[1] != HeldMode && !Main.dedServ)
			{
				SoundEngine.PlaySound(SnowballBreakSound, Projectile.Center);
			}
		}

		private Vector2 GetNPCImpactPosition(NPC target)
		{
			return new Vector2(
				MathHelper.Clamp(Projectile.Center.X, target.position.X, target.position.X + target.width),
				MathHelper.Clamp(Projectile.Center.Y, target.position.Y, target.position.Y + target.height));
		}

		private void SpawnSnowImpactEffect(Vector2 impactPosition)
		{
			if (Main.dedServ)
			{
				return;
			}

			Vector2 dustPosition = impactPosition - new Vector2(4f);
			int particleCount = 6 + (int)((Projectile.ai[0] - 1f) * 2f);
			for (int i = 0; i < particleCount; i++)
			{
				float dustScale = Main.rand.NextFloat(0.7f, 1.1f) * (0.8f + Projectile.ai[0] * 0.02f);
				Dust dust = Dust.NewDustDirect(
					dustPosition,
					8,
					8,
					DustID.Snow,
					0f,
					0f,
					0,
					default,
					dustScale);
				dust.velocity = Main.rand.NextVector2Circular(2.5f + Projectile.ai[0] * 0.2f, 2.5f + Projectile.ai[0] * 0.2f);
				dust.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = SizeMultiplier > 1f
				? TextureAssets.Projectile[ProjectileID.SlushBall].Value
				: TextureAssets.Projectile[Type].Value;
			Vector2 drawPosition;
			if (Projectile.ai[1] == HeldMode)
			{
				Player owner = Main.player[Projectile.owner];
				drawPosition = GetHeldAnchor(owner)
					+ new Vector2(owner.direction * texture.Width * Projectile.scale * 0.5f, -texture.Height * Projectile.scale * 0.5f);
			}
			else
			{
				drawPosition = Projectile.Center;
			}

			Main.EntitySpriteDraw(
				texture,
				drawPosition - Main.screenPosition,
				null,
				lightColor,
				Projectile.rotation,
				new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
				Projectile.scale,
				SpriteEffects.None,
				0);
			return false;
		}

		private void Split()
		{
			if (Projectile.owner != Main.myPlayer)
			{
				return;
			}

			int fragmentCount = GetFragmentCount(Projectile.ai[0]);
			if (fragmentCount == 0)
			{
				return;
			}

			float fragmentSize = System.Math.Max(0.65f, Projectile.ai[0] * 0.4f);
			int fragmentDamage = System.Math.Max(1, (int)(Projectile.damage * 0.4f));
			for (int i = 0; i < fragmentCount; i++)
			{
				float spread = 4f + Projectile.ai[0];
				Vector2 velocity = new Vector2(
					Main.rand.NextFloat(-spread, spread),
					Main.rand.NextFloat(-spread, spread));
				if (velocity.LengthSquared() < 1f)
				{
					velocity.Y = -spread;
				}

				Projectile.NewProjectile(
					Projectile.GetSource_FromThis(),
					Projectile.Center,
					velocity,
					Type,
					fragmentDamage,
					Projectile.knockBack * 0.5f,
					Projectile.owner,
					fragmentSize,
					FragmentMode,
					Projectile.ai[2]);
			}
		}

		private static int GetFragmentCount(float size)
		{
			if (size >= 2.5f)
			{
				return 4;
			}

			if (size >= 2f)
			{
				return 3;
			}

			return size >= 1.5f ? 2 : 0;
		}

		public static float ThrowSpeed(float size)
		{
			return 4f + (size - 1f) * 3f;
		}
	}
}
