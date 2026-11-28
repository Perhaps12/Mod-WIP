using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Projectiles
{
	public class EnergySwordExplosion : ModProjectile
	{
		public const int DamageMultiplier = 6;
		public const int DetonationDuration = 30;
		public const float BlastRadius = 320f;

		public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Excalibur}";

		public override void SetDefaults()
		{
			Projectile.width = (int)(BlastRadius * 2f);
			Projectile.height = (int)(BlastRadius * 2f);
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = DetonationDuration;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
		}

		public override void AI()
		{
			if (Projectile.localAI[0] != 0f)
			{
				return;
			}

			Player owner = Main.player[Projectile.owner];
			Projectile.Center = owner.Center;
			Projectile.localAI[0] = 1f;
			if (!Main.dedServ)
			{
				SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
				Lighting.AddLight(Projectile.Center, 1.4f, 0.9f, 0.25f);
				for (int i = 0; i < 48; i++)
				{
					Vector2 direction = (MathHelper.TwoPi * i / 48f).ToRotationVector2();
					Dust dust = Dust.NewDustPerfect(
						Projectile.Center + direction * (BlastRadius * 0.35f),
						DustID.Torch,
						direction * Main.rand.NextFloat(3f, 8f),
						0,
						new Color(255, 190, 65),
						Main.rand.NextFloat(1.7f, 2.6f));
					dust.noGravity = true;
				}
			}

			Projectile.Damage();
			Projectile.localAI[1] = 1f;
			owner.GetModPlayer<EnergySwordPlayer>().BeginDetonation();
		}

		public override bool? CanDamage()
		{
			return Projectile.localAI[0] != 0f && Projectile.localAI[1] == 0f;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Vector2 closestPoint = Vector2.Clamp(
				Projectile.Center,
				targetHitbox.TopLeft(),
				targetHitbox.BottomRight());
			return Vector2.DistanceSquared(Projectile.Center, closestPoint) <= BlastRadius * BlastRadius;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			return false;
		}
	}
}
