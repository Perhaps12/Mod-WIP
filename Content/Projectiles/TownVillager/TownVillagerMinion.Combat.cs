using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	// ---- Combat: ranges, attack positioning, aiming and shooting ----
	//   Melee:    walks right up to the enemy (and jumps at it); hits by touch (MinionContactDamage).
	//   Midrange: keeps MidrangeMin-MidrangeMax away. Outside that band it walks to a random distance within it, then holds.
	//   Ranged:   if farther than RangedMax, walks in to a random distance of RangedMin-RangedMax; otherwise stands still.
	// Distances are horizontal, since villagers walk on the ground.
	public partial class TownVillagerMinion
	{
		private enum CombatRange { Melee, Midrange, Ranged }

		// Combat range of each villager, in the same order as Villagers (TownVillagerMinion.cs)
		private static readonly CombatRange[] Ranges =
		{
			CombatRange.Midrange, // Guide
			CombatRange.Midrange, // Merchant
			CombatRange.Midrange, // Demolitionist
			CombatRange.Melee,    // Zoologist
			CombatRange.Midrange, // Dryad
			CombatRange.Ranged,   // Arms Dealer
			CombatRange.Midrange, // Party Girl
			CombatRange.Ranged, // Clothier
			CombatRange.Midrange,   // Wizard
			CombatRange.Ranged,   // Steampunker
			CombatRange.Ranged,   // Princess
		};

		private const float MidrangeMin = 8 * 16f;
		private const float MidrangeMax = 20 * 16f;
		private const float RangedMin = 15 * 16f;
		private const float RangedMax = 30 * 16f;
		private const float MeleeStandOffset = 12f;    // melee villagers get right up to the enemy (px from its center)
		private const float ArriveDistance = 16f;      // close enough to the attack spot to stop repositioning

		// The vanilla Resonance Scepter is not exposed as an ItemID constant in this tModLoader build.
		private const int ResonanceScepterItem = 5065;

		// How fast lobbed shots fall (px/tick^2), used to aim their arc. Raise it if they land short,
		// lower it if they overshoot.
		private const float GrenadeGravity = 0.2f;   // Demolitionist's grenades
		private const float PartyGrenadeGravity = 0.2f; // Party Girl's party grenades (tune separately if she misses)
		private const float FireballGravity = 0.2f;  // Wizard's bouncing fireballs

		// Longest lead it will aim with (ticks of travel), so it doesn't aim wildly at far, fast enemies
		private const float MaxLeadTicks = 90f;

		// Distance from the enemy it's walking to, picked at random within its band each time it repositions.
		// Kept in ai[2] so every player sees the same spot in multiplayer (only the owner picks it).
		private ref float StandDistance => ref Projectile.ai[2];

		private CombatRange Range => Ranges[System.Math.Clamp(VillagerIndex, 0, VillagerCount - 1)];

		// Walking to a better distance from the enemy (midrange/ranged); while false it holds its position
		private bool repositioning;

		// ---- Midrange climbing ----
		// When the enemy is more than ClimbTriggerHeight above it, a midrange villager looks for a spot to stand
		// at about the enemy's height (MidrangeMin-MidrangeMax from it, its own side first), walks there and
		// jumps up onto it (see WalkMovement), then attacks from up there.
		private const float ClimbTriggerHeight = 5 * 16f;  // enemy this much higher = out of reach, climb up
		private const int ClimbSearchUpTiles = 3;          // stand spots can be up to this many tiles above the enemy's feet...
		private const int ClimbSearchDownTiles = 4;        // ...or this many below
		private const int ClimbHeadroomTiles = 3;          // empty tiles needed above a stand spot
		private const int ClimbRecheckTicks = 60;          // looks for a new spot this often (follows a moving enemy)

		private Vector2? climbSpot;      // where it's climbing to (bottom-center of the stand spot), or null
		private int climbRecheckTimer;
		private bool Climbing => climbSpot.HasValue;

		// Returns the spot to climb to while the enemy is out of reach above, or null when it isn't
		private Vector2? UpdateClimbSpot(NPC target)
		{
			if (Projectile.Bottom.Y - target.Bottom.Y <= ClimbTriggerHeight)
			{
				climbSpot = null;
				return null;
			}

			if (--climbRecheckTimer <= 0 || !climbSpot.HasValue)
			{
				climbSpot = FindClimbSpot(target);
				climbRecheckTimer = ClimbRecheckTicks;
			}
			return climbSpot;
		}

		// Closest standable spot (to this villager) at about the enemy's height, MidrangeMin-MidrangeMax
		// from it sideways. Tries its own side of the enemy first, then the other side.
		private Vector2? FindClimbSpot(NPC target)
		{
			int ownSide = Projectile.Center.X >= target.Center.X ? 1 : -1;
			foreach (int side in new[] { ownSide, -ownSide })
			{
				Vector2? best = null;
				float bestDistance = float.MaxValue;
				for (float dx = MidrangeMin; dx <= MidrangeMax; dx += 16f)
				{
					float x = target.Center.X + side * dx;
					float? ground = FindStandableGround(x, target.Bottom.Y);
					if (!ground.HasValue)
						continue;
					float distance = System.Math.Abs(x - Projectile.Center.X);
					if (distance < bestDistance)
					{
						bestDistance = distance;
						best = new Vector2(x, ground.Value);
					}
				}
				if (best.HasValue)
					return best;
			}
			return null;
		}

		// Top Y of a standable tile (solid or platform) in column x near feetY, with room to stand on it
		private static float? FindStandableGround(float x, float feetY)
		{
			int tileX = (int)(x / 16f);
			int feetTileY = (int)(feetY / 16f);
			for (int y = feetTileY - ClimbSearchUpTiles; y <= feetTileY + ClimbSearchDownTiles; y++)
			{
				if (!WorldGen.InWorld(tileX, y - ClimbHeadroomTiles))
					continue;
				Tile ground = Main.tile[tileX, y];
				if (!(ground.HasUnactuatedTile && (Main.tileSolid[ground.TileType] || Main.tileSolidTop[ground.TileType])))
					continue;

				bool roomAbove = true;
				for (int h = 1; h <= ClimbHeadroomTiles; h++)
				{
					Tile above = Main.tile[tileX, y - h];
					if (above.HasUnactuatedTile && Main.tileSolid[above.TileType] && !Main.tileSolidTop[above.TileType])
					{
						roomAbove = false;
						break;
					}
				}
				if (roomAbove)
					return y * 16f;
			}
			return null;
		}

		// ---- Attack positioning ----

		private float AttackSpotX(NPC target)
		{
			float dx = Projectile.Center.X - target.Center.X;
			int side = dx >= 0f ? 1 : -1;  // stay on the side of the enemy it's already on
			float distance = System.Math.Abs(dx);

			switch (Range)
			{
				case CombatRange.Melee:
					return target.Center.X + side * MeleeStandOffset; // right up to the enemy

				case CombatRange.Midrange:
					// Enemy too high above to reach: climb up to a spot at about its height instead
					if (UpdateClimbSpot(target) is Vector2 climb)
						return climb.X;

					if (!repositioning && (distance < MidrangeMin || distance > MidrangeMax))
						StartRepositioning(MidrangeMin, MidrangeMax);
					return Reposition(target.Center.X + side * StandDistanceOr((MidrangeMin + MidrangeMax) / 2f));

				default: // Ranged: backs off when the enemy is too close, moves in when it's too far
					if (!repositioning && (distance < RangedMin || distance > RangedMax))
						StartRepositioning(RangedMin, RangedMax);
					return Reposition(target.Center.X + side * StandDistanceOr((RangedMin + RangedMax) / 2f));
			}
		}

		// Pick a new random distance within [min, max] to walk to
		private void StartRepositioning(float min, float max)
		{
			repositioning = true;
			if (Main.myPlayer == Projectile.owner)
			{
				StandDistance = Main.rand.NextFloat(min, max);
				Projectile.netUpdate = true;
			}
		}

		// The picked stand distance, or a fallback before one has been picked/synced
		private float StandDistanceOr(float fallback) => StandDistance > 0f ? StandDistance : fallback;

		// Walk to the ideal spot while repositioning; once there, stop and hold position
		private float Reposition(float idealX)
		{
			if (repositioning && System.Math.Abs(idealX - Projectile.Center.X) < ArriveDistance)
				repositioning = false;
			return repositioning ? idealX : Projectile.Center.X; // destination = where it is = stand still
		}

		// ---- Attacking ----

		private void UpdateAttack(Player owner, NPC target)
		{
			AttackTimer = System.Math.Min(AttackTimer + 1f, AttackIntervals[VillagerIndex]);

			// The Dryad doesn't attack: she keeps up her ward aura instead (TownVillagerMinion.Dryad.cs)
			if (VillagerType == NPCID.Dryad)
			{
				UpdateDryadAura();
				return;
			}

			// Melee villagers hit by touching enemies (MinionContactDamage), not with projectiles
			if (Range == CombatRange.Melee)
				return;

			// In the middle of a burst: fire the next shot every BurstShotDelay ticks
			if (burstShotsLeft > 0)
			{
				if (--burstTimer > 0)
					return;
				if (target == null || Main.myPlayer != Projectile.owner
					|| !Collision.CanHitLine(Projectile.Center, 1, 1, target.Center, 1, 1))
				{
					burstShotsLeft = 0; // lost the target: end the burst
					return;
				}
				FireAt(target);
				burstShotsLeft--;
				burstTimer = BurstShotDelay(VillagerType);
				if (burstShotsLeft == 0)
					AttackTimer = 0f; // the attack interval is the pause after the burst ends
				return;
			}

			if (target == null || AttackTimer < AttackIntervals[VillagerIndex]
				|| Main.myPlayer != Projectile.owner
				|| !Collision.CanHitLine(Projectile.Center, 1, 1, target.Center, 1, 1))
				return;

			AttackTimer = 0f;
			FireAt(target);

			// Villagers that fire in bursts queue up the rest of the burst
			burstShotsLeft = BurstShots(VillagerType) - 1;
			burstTimer = BurstShotDelay(VillagerType);
		}

		// ---- Bursts ----
		// Villagers listed here fire several shots in quick succession per attack; AttackIntervals is then
		// the pause between bursts. Everyone else fires one shot per attack.
		private int burstShotsLeft;  // shots still to fire in the current burst
		private int burstTimer;      // ticks until the next shot in the burst

		private static int BurstShots(int npcType) => npcType switch
		{
			NPCID.Steampunker => 3,
			_ => 1,
		};

		private static int BurstShotDelay(int npcType) => npcType switch
		{
			NPCID.Steampunker => 10, // ticks between shots in a burst
			_ => 0,
		};

		private void FireAt(NPC target)
		{
			int projectileType;
			float speed;
			float spread = 0f;
			bool lob = false;               // thrown in an arc (affected by gravity)
			float gravity = GrenadeGravity; // how fast a lobbed shot falls
			bool placeOnTarget = false;     // created right on the enemy, standing still, instead of being fired

			// Picked by NPC type, so it doesn't depend on the order of the Villagers list
			switch (VillagerType)
			{
				case NPCID.Guide:
					projectileType = ProjectileID.FireArrow;
					speed = 11f;
					break;
				case NPCID.Merchant:
					projectileType = ProjectileID.ThrowingKnife;
					speed = 11f;
					break;
				case NPCID.Demolitionist:
					projectileType = ProjectileID.Grenade;
					speed = 9f;
					lob = true;
					break;
				case NPCID.PartyGirl:
					projectileType = ProjectileID.PartyGirlGrenade;
					speed = 9f;
					lob = true; // same arc + lead-aiming as the Demolitionist
					gravity = PartyGrenadeGravity;
					break;
				case NPCID.ArmsDealer:
					projectileType = ProjectileID.Bullet;
					speed = 14f;
					spread = 0.07f;
					break;
				case NPCID.Clothier: // Clothier's Curse skull that homes in on enemies
					projectileType = ModContent.ProjectileType<VillagerHomingSkull>();
					speed = VillagerHomingSkull.Speed; // flies at a constant speed, set in VillagerHomingSkull.cs
					break;
				case NPCID.Wizard: // bouncing fireballs (Flower of Fire), thrown in an arc since they fall
					projectileType = ProjectileID.BallofFire;
					speed = 10f;
					lob = true;
					gravity = FireballGravity;
					break;
				case NPCID.Steampunker:
					projectileType = ProjectileID.BulletHighVelocity;
					speed = 16f;
					spread = 0.02f;
					break;
				default: // Princess: Resonance Scepter energy created on the enemy, lingering in place
					projectileType = ContentSamples.ItemsByType[ResonanceScepterItem].shoot;
					speed = 0f;
					placeOnTarget = true;
					break;
			}

			Vector2 spawnPosition;
			Vector2 velocity;
			if (placeOnTarget)
			{
				// Created right on the enemy and doesn't move
				spawnPosition = target.Center;
				velocity = Vector2.Zero;
			}
			else
			{
				// Lead the target: aim where it will be when the shot arrives (based on its current velocity).
				// Shots that fall under gravity are aimed in an arc that lands there; everything else flies
				// straight at it.
				Vector2 predicted = PredictTargetOffset(target, projectileType, speed, lob, gravity);
				Vector2 aim = lob
					? LobDirection(predicted, speed, gravity)
					: predicted.SafeNormalize(Vector2.UnitX);
				if (spread > 0f)
					aim = aim.RotatedBy(Main.rand.NextFloat(-spread, spread));
				spawnPosition = Projectile.Center;
				velocity = aim * speed;
			}

			int projectileIndex = Projectile.NewProjectile(
				Projectile.GetSource_FromAI(),
				spawnPosition,
				velocity,
				projectileType,
				Projectile.damage,
				Projectile.knockBack,
				Projectile.owner);

			if (projectileIndex >= 0 && projectileIndex < Main.maxProjectiles)
			{
				Projectile shot = Main.projectile[projectileIndex];
				shot.friendly = true;
				shot.hostile = false;
				shot.DamageType = DamageClass.Summon;
				shot.netUpdate = true;
			}

			SoundEngine.PlaySound(VillagerType == NPCID.Wizard ? SoundID.Item20 : SoundID.Item1, Projectile.Center);
		}

		// ---- Aiming ----

		// Where the target will be (relative to this villager) when a shot fired now reaches it.
		// Estimates the travel time, moves the target along its velocity by that much, and repeats
		// a few times so the estimate settles.
		private Vector2 PredictTargetOffset(NPC target, int projectileType, float speed, bool lob, float gravity)
		{
			Vector2 offset = target.Center - Projectile.Center;

			// Some vanilla projectiles (e.g. bullets) move several steps per tick: count their real speed
			int extraUpdates = ContentSamples.ProjectilesByType.TryGetValue(projectileType, out Projectile sample)
				? sample.extraUpdates : 0;
			float speedPerTick = speed * (extraUpdates + 1);

			Vector2 predicted = offset;
			for (int i = 0; i < 3; i++)
			{
				float travelTicks;
				if (lob)
				{
					// Grenades: time = sideways distance / sideways speed of the arc
					float sidewaysSpeed = System.Math.Abs(LobDirection(predicted, speed, gravity).X) * speedPerTick;
					travelTicks = sidewaysSpeed > 0.1f ? System.Math.Abs(predicted.X) / sidewaysSpeed : 0f;
				}
				else
				{
					travelTicks = predicted.Length() / speedPerTick;
				}

				predicted = offset + target.velocity * System.Math.Min(travelTicks, MaxLeadTicks);
			}
			return predicted;
		}

		// Direction to throw at `speed` so a projectile falling with `gravity` lands on `offset`
		// (target position relative to the thrower). Uses the flatter of the two possible arcs.
		// If the target is out of reach, throws at 45 degrees (the longest throw) toward it.
		private static Vector2 LobDirection(Vector2 offset, float speed, float gravity)
		{
			float x = System.Math.Abs(offset.X);
			float up = -offset.Y;                 // height of the target above the thrower
			int side = offset.X >= 0f ? 1 : -1;
			if (x < 1f)
				return new Vector2(0f, up >= 0f ? -1f : 1f);

			float s2 = speed * speed;
			float discriminant = s2 * s2 - gravity * (gravity * x * x + 2f * up * s2);
			float angle = discriminant < 0f
				? MathHelper.PiOver4
				: (float)System.Math.Atan((s2 - (float)System.Math.Sqrt(discriminant)) / (gravity * x));

			// angle is measured upward from horizontal; screen Y points down
			return new Vector2(side * (float)System.Math.Cos(angle), -(float)System.Math.Sin(angle));
		}
	}
}
