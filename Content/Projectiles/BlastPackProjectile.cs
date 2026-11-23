using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Projectiles
{
	// The thrown Blast Pack. Spawned by Content/Items/BlastPack.cs.
	//
	//   Flying   - arcs with gravity
	//   Stuck    - sticks to the first tile it touches (falls again if that tile is removed)
	//   Exploding- after the fuse, on enemy contact, or on right click: the hitbox grows for
	//              3 ticks to damage enemies, then it dies and launches its owner away from it.
	public class BlastPackProjectile : ModProjectile
	{
		const int FuseTicks = 180;             // 3 seconds, same as a Sticky Grenade
		const int BlowTicks = 3;               // last ticks of life where the explosion does damage
		const int ExplosionSize = 200;         // px, width/height of the damaging blast (21 tiles)
		const float LaunchRampRadius = 8 * 16f; // launch strength ramps from Near to Far over the first 8 tiles
		const float LaunchRadius = 12 * 16f;   // ...then stays at Far out to 12 tiles; beyond that, no launch
		const float LaunchSpeedNear = 2f;      // px per tick at point-blank
		const float LaunchMaxUpFraction = 0.7f; // upward launch speed is capped at this fraction of LaunchSpeedFar
		const float LaunchUpBonus = 3f;        // px per tick of extra upward speed added to every launch
		const float LaunchSpeedFar = 8f;      // px per tick from LaunchRampRadius out to LaunchRadius (farther = stronger)

		// Placed sprite of tile 428 (Weighted Pressure Plate), style 0 (orange)
		const int TileType = 428;
		static readonly Rectangle TileFrame = new Rectangle(0, 0, 16, 16);

		bool Stuck
		{
			get => Projectile.ai[0] == 1;
			set => Projectile.ai[0] = value ? 1 : 0;
		}

		// Set by right click. Lives in ai[] so it syncs in multiplayer.
		bool DetonateRequested
		{
			get => Projectile.ai[1] == 1;
			set => Projectile.ai[1] = value ? 1 : 0;
		}

		bool Blown
		{
			get => Projectile.localAI[0] == 1;
			set => Projectile.localAI[0] = value ? 1 : 0;
		}

		public override string Texture => "Terraria/Images/Tiles_" + TileType;

		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Ranged;
			Projectile.penetrate = -1;
			Projectile.tileCollide = true;
			Projectile.timeLeft = FuseTicks;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;   // each enemy is hit once per Blast Pack
		}

		public static bool HasActive(Player player)
		{
			return player.ownedProjectileCounts[ModContent.ProjectileType<BlastPackProjectile>()] > 0;
		}

		// Right click: blow up every Blast Pack this player has out
		public static void DetonateAll(Player player)
		{
			int type = ModContent.ProjectileType<BlastPackProjectile>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == player.whoAmI && p.type == type
					&& p.ModProjectile is BlastPackProjectile pack)
					pack.Detonate();
			}
		}

		public void Detonate()
		{
			if (DetonateRequested || Blown)
				return;
			DetonateRequested = true;
			Projectile.netUpdate = true;
		}

		public override void AI()
		{
			if (DetonateRequested && Projectile.timeLeft > BlowTicks)
				Projectile.timeLeft = BlowTicks;

			if (Projectile.timeLeft <= BlowTicks)
			{
				Blow();
				return;
			}

			if (Stuck)
			{
				Projectile.velocity = Vector2.Zero;

				// The tile it was stuck to got mined: fall again
				if (!Collision.SolidCollision(Projectile.position - new Vector2(2), Projectile.width + 4, Projectile.height + 4))
					Stuck = false;
			}
			else
			{
				Projectile.velocity.Y += 0.2f;             // gravity
				if (Projectile.velocity.Y > 16f)
					Projectile.velocity.Y = 16f;
				Projectile.rotation += Projectile.velocity.X * 0.05f;
			}
		}

		// Grow into the explosion hitbox. Damage is dealt for the remaining BlowTicks.
		void Blow()
		{
			if (Blown)
				return;
			Blown = true;
			Projectile.tileCollide = false;
			Projectile.velocity = Vector2.Zero;
			Projectile.alpha = 255;
			Projectile.Resize(ExplosionSize, ExplosionSize);   // keeps the center in place
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (!Stuck)
			{
				Stuck = true;
				Projectile.velocity = Vector2.Zero;
				Projectile.netUpdate = true;
			}
			return false; // don't die on tile hit
		}

		// Like a grenade, touching an enemy sets it off
		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			Detonate();
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
			SpawnExplosionEffects();

			if (Projectile.owner == Main.myPlayer)
				LaunchOwner();
		}

		// Push the owner directly away from the blast
		void LaunchOwner()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead)
				return;

			Vector2 away = owner.Center - Projectile.Center;
			float dist = away.Length();
			if (dist > LaunchRadius)
				return;

			Vector2 dir = dist > 1f ? away / dist : new Vector2(0f, -owner.gravDir); // right on top of it: straight up
			float strength = MathHelper.Lerp(LaunchSpeedNear, LaunchSpeedFar, MathHelper.Clamp(dist / LaunchRampRadius, 0f, 1f));

			// Cancel any speed toward the blast first (e.g. falling onto it), so the launch always feels full
			float towardBlast = Vector2.Dot(owner.velocity, dir);
			if (towardBlast < 0f)
				owner.velocity -= dir * towardBlast;

			Vector2 launch = dir * strength;
			launch.Y -= LaunchUpBonus * owner.gravDir;   // every launch also pops you up a bit
			// Cap the upward part so launching straight up is weaker than launching forwards
			float maxUp = LaunchSpeedFar * LaunchMaxUpFraction;
			if (-launch.Y * owner.gravDir > maxUp)
				launch.Y = -maxUp * owner.gravDir;
			owner.velocity += launch;
			owner.jump = 0;
			owner.fallStart = (int)(owner.position.Y / 16f);   // reset fall damage at the launch point
			owner.GetModPlayer<BlastPackPlayer>().StartLowGravity();
		}

		void SpawnExplosionEffects()
		{
			Vector2 corner = Projectile.Center - new Vector2(ExplosionSize / 4);
			for (int i = 0; i < 30; i++)
			{
				Dust smoke = Dust.NewDustDirect(corner, ExplosionSize / 2, ExplosionSize / 2, DustID.Smoke, 0f, 0f, 100, default, 2f);
				smoke.velocity *= 1.4f;
			}
			for (int i = 0; i < 20; i++)
			{
				Dust fire = Dust.NewDustDirect(corner, ExplosionSize / 2, ExplosionSize / 2, DustID.Torch, 0f, 0f, 100, default, 3f);
				fire.noGravity = true;
				fire.velocity *= 5f;
			}
			if (Main.netMode != NetmodeID.Server)
			{
				for (int i = 0; i < 3; i++)
				{
					Gore smokeGore = Gore.NewGoreDirect(Projectile.GetSource_Death(), Projectile.Center - new Vector2(24), default, Main.rand.Next(61, 64));
					smokeGore.velocity *= 0.4f;
					smokeGore.velocity += Main.rand.NextVector2Circular(1.5f, 1.5f);
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Blown)
				return false;

			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, TileFrame, lightColor,
				Projectile.rotation, TileFrame.Size() / 2f, Projectile.scale, SpriteEffects.None);
			return false;
		}
	}
}
