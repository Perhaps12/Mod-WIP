using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	public class NyrulnaConeProjectile : ModProjectile
	{
		private const float ConeLength = 8f * 16f;
		private const float ConeHalfAngle = MathHelper.Pi / 6f;
		private const float HeavyKnockback = 30f;
		private const int EffectTicks = 6;

		private float DirectionAngle => Projectile.ai[0];

		public override string Texture => $"Terraria/Images/Item_{ItemID.TitaniumTrident}";

		public override void SetDefaults()
		{
			Projectile.width = (int)(ConeLength * 2f);
			Projectile.height = (int)(ConeLength * 2f);
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = EffectTicks;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			Projectile.Center = owner.MountedCenter;
			Projectile.velocity = Vector2.Zero;

			if (Projectile.localAI[0] == 0f)
			{
				Projectile.localAI[0] = 1f;
				if (Main.netMode != NetmodeID.Server)
					SpawnConeParticles();
			}
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			return targetHitbox.IntersectsConeSlowMoreAccurate(
				Projectile.Center,
				ConeLength,
				DirectionAngle,
				ConeHalfAngle);
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			modifiers.DisableKnockback();
			modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			Vector2 away = (target.Center - Projectile.Center).SafeNormalize(DirectionAngle.ToRotationVector2());
			target.velocity = away * HeavyKnockback * target.knockBackResist;
			target.netUpdate = true;
		}

		public override bool PreDraw(ref Color lightColor) => false;

		private void SpawnConeParticles()
		{
			Vector2 aim = DirectionAngle.ToRotationVector2();
			for (int i = 0; i < 32; i++)
			{
				float angle = DirectionAngle + Main.rand.NextFloat(-ConeHalfAngle, ConeHalfAngle);
				Vector2 direction = angle.ToRotationVector2();
				float distance = Main.rand.NextFloat(20f, ConeLength);
				Dust dust = Dust.NewDustPerfect(
					Projectile.Center + direction * distance,
					DustID.Cloud,
					direction * Main.rand.NextFloat(2f, 6f) + aim * 2f,
					0,
					new Color(120, 230, 255),
					Main.rand.NextFloat(1.2f, 2f));
				dust.noGravity = true;
			}
		}
	}
}
