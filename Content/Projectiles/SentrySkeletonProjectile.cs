using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	/// <summary>
	/// Stationary example turret: falls onto solid tiles or platforms, finds a nearby enemy,
	/// and periodically spawns a SentrySkeletonShot toward it.
	/// </summary>
	public class SentrySkeletonProjectile : ModProjectile
	{
		// Easily adjustable behavior values. Distances and speeds are in pixels; intervals are ticks.
		private const float TargetingRange = 600f; // Pixels
		private const int ShootInterval = 60; // Ticks
		private const float ShotSpeed = 10f; // Pixels per tick
		private const float Gravity = 0.2f;
		private const float MaxFallSpeed = 16f;

		// ai[0] is automatically synchronized projectile state and stores the time until the next shot.
		private ref float ShootTimer => ref Projectile.ai[0];

		// Reuse a vanilla projectile graphic until this sentry has a custom texture.
		public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Boulder}";

		// Allow the player to designate targets using the normal summon targeting controls.
		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.MinionTargettingFeature[Type] = true;
		}

		public override void SetDefaults()
		{
			// Configure a harmless sentry body; only its spawned shot deals damage.
			Projectile.width = 32;
			Projectile.height = 32;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.sentry = true; // Counts against the sentry limit.
			Projectile.friendly = false;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = Projectile.SentryLifeTime; // Default sentry lifetime.
			Projectile.netImportant = true; // Syncs the sentry to players who join later.
		}

		// Make platforms solid for this projectile so the falling turret can land on them.
		public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac)
		{
			fallThrough = false;
			return true;
		}

		// Stop downward movement on landing and keep the sentry alive instead of destroying it.
		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (oldVelocity.Y != Projectile.velocity.Y)
				Projectile.velocity.Y = 0f;

			return false;
		}

		/// <summary>
		/// Applies gravity, selects a target, counts down the firing interval, and creates a shot.
		/// Only the owning client creates the shot to avoid duplicate projectiles in multiplayer.
		/// </summary>
		public override void AI()
		{
			// Keep the sentry fixed horizontally and accelerate it downward until it reaches terminal speed.
			Projectile.velocity.X = 0f;
			if (Projectile.velocity.Y < MaxFallSpeed)
			{
				Projectile.velocity.Y += Gravity;
				if (Projectile.velocity.Y > MaxFallSpeed)
					Projectile.velocity.Y = MaxFallSpeed;
			}

			NPC target = FindTarget();
			if (target == null)
				return;

			if (ShootTimer > 0f)
			{
				ShootTimer--;
				return;
			}

			ShootTimer = ShootInterval;
			Projectile.netUpdate = true;

			// Only the owning client should create the shot in multiplayer.
			if (Main.myPlayer != Projectile.owner)
				return;

			// Aim directly at the selected target and pass this sentry's damage and owner to the shot.
			Vector2 velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * ShotSpeed;
			Projectile.NewProjectile(
				Projectile.GetSource_FromThis(),
				Projectile.Center,
				velocity,
				ModContent.ProjectileType<SentrySkeletonShot>(),
				Projectile.damage,
				Projectile.knockBack,
				Projectile.owner);
		}

		/// <summary>
		/// Returns the nearest chaseable NPC within range that has a clear line of sight.
		/// Returns null if no valid target is found.
		/// </summary>
		private NPC FindTarget()
		{
			NPC target = null;
			float closestDistanceSquared = TargetingRange * TargetingRange;

			// Searching by squared distance avoids square roots for each NPC candidate.
			foreach (NPC npc in Main.ActiveNPCs)
			{
				if (!npc.CanBeChasedBy(this))
					continue;

				float distanceSquared = Vector2.DistanceSquared(Projectile.Center, npc.Center);
				if (distanceSquared >= closestDistanceSquared)
					continue;

				if (!Collision.CanHit(Projectile.position, Projectile.width, Projectile.height, npc.position, npc.width, npc.height))
					continue;

				closestDistanceSquared = distanceSquared;
				target = npc;
			}

			return target;
		}
	}
}
