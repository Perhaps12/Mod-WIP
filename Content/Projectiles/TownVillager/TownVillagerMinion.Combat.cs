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
	//   Ranged:   keeps RangedMin-RangedMax away, like midrange: outside that band (too close or too far) it walks
	//             to a random distance within it, then holds.
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

		private const float MidrangeMin = 6 * 16f;
		private const float MidrangeMax = 10 * 16f;
		private const float RangedMin = 15 * 16f;
		private const float RangedMax = 30 * 16f;
		private const float MeleeLeadTicks = 8f;       // melee villagers aim this many ticks ahead of a moving enemy...
		private const float MeleeLeadFullDistance = 6 * 16f; // ...when at least this far from it (less lead closer in)
		private const float ArriveDistance = 16f;      // close enough to the attack spot to stop repositioning

		// The vanilla Resonance Scepter is not exposed as an ItemID constant in this tModLoader build.
		private const int ResonanceScepterItem = 5065;

		// How lobbed shots fly, used to aim their arc (the aim simulates the flight tick by tick):
		//   gravity      - added to the fall speed each tick. Raise it if they land short, lower if they overshoot.
		//   gravityDelay - ticks after being thrown before gravity kicks in (vanilla grenades fly straight briefly)
		//   drag         - sideways speed is multiplied by this each tick (1 = no slowdown)
		//   maxFall      - fastest fall speed
		private readonly struct LobPhysics
		{
			public readonly float Gravity, Drag, MaxFall;
			public readonly int GravityDelay;
			public LobPhysics(float gravity, int gravityDelay, float drag, float maxFall)
			{
				Gravity = gravity; GravityDelay = gravityDelay; Drag = drag; MaxFall = maxFall;
			}
		}
		private static readonly LobPhysics GrenadePhysics = new(0.2f, 5, 0.99f, 16f);      // Demolitionist's grenades
		private static readonly LobPhysics PartyGrenadePhysics = new(0.2f, 5, 0.99f, 16f); // Party Girl's party grenades
		private static readonly LobPhysics FireballPhysics = new(0.2f, 0, 1f, 16f);        // Wizard's bouncing fireballs

		private const int LobSimulateTicks = 150; // longest flight the aim simulates
		private const int LobAngleSteps = 80;     // throw angles tried when aiming a lob (more = finer aim)

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
				{
					// Straight onto the enemy's center (not beside it), so their hitboxes overlap. While still
					// far away it aims ahead of a moving enemy to catch up; the lead shrinks to nothing as it
					// gets close, so it ends up on the enemy rather than in front of it.
					float leadFraction = MathHelper.Clamp((distance - target.width / 2f) / MeleeLeadFullDistance, 0f, 1f);
					return target.Center.X + target.velocity.X * MeleeLeadTicks * leadFraction;
				}

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
			LobPhysics physics = GrenadePhysics; // how a lobbed shot flies
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
					physics = PartyGrenadePhysics;
					break;
				case NPCID.ArmsDealer:
					projectileType = ProjectileID.Bullet;
					speed = 14f;
					spread = 0.07f;
					break;
				case NPCID.Clothier: // Clothier's Curse skull that homes in on enemies
					projectileType = ProjectileID.ClothiersCurse; // the real vanilla skull, for its visuals
					speed = VillagerSkull.Speed; // homing, constant speed and piercing come from VillagerSkull.cs
					break;
				case NPCID.Wizard: // bouncing fireballs (Flower of Fire), thrown in an arc since they fall
					projectileType = ProjectileID.BallofFire;
					speed = 10f;
					lob = true;
					physics = FireballPhysics;
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
				Vector2 aim = lob
					? AimLob(target, speed, physics)
					: PredictTargetOffset(target, projectileType, speed).SafeNormalize(Vector2.UnitX);
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

		// Where the target will be (relative to this villager) when a straight shot fired now reaches it.
		// Estimates the travel time, moves the target along its velocity by that much, and repeats
		// a few times so the estimate settles.
		private Vector2 PredictTargetOffset(NPC target, int projectileType, float speed)
		{
			Vector2 offset = target.Center - Projectile.Center;

			// Some vanilla projectiles (e.g. bullets) move several steps per tick: count their real speed
			int extraUpdates = ContentSamples.ProjectilesByType.TryGetValue(projectileType, out Projectile sample)
				? sample.extraUpdates : 0;
			float speedPerTick = speed * (extraUpdates + 1);

			Vector2 predicted = offset;
			for (int i = 0; i < 3; i++)
			{
				float travelTicks = predicted.Length() / speedPerTick;
				predicted = offset + target.velocity * System.Math.Min(travelTicks, MaxLeadTicks);
			}
			return predicted;
		}

		// Direction to throw a lobbed shot so it lands on the enemy, leading it. Each guess of where the enemy
		// will be gives an arc and a flight time (from simulating the shot's real flight), which gives a
		// better guess of where the enemy will be; repeated a few times so it settles.
		private Vector2 AimLob(NPC target, float speed, LobPhysics physics)
		{
			Vector2 offset = target.Center - Projectile.Center;
			Vector2 predicted = offset;
			Vector2 direction = Vector2.UnitX;
			for (int i = 0; i < 4; i++)
			{
				direction = LobDirection(predicted, speed, physics, out float travelTicks);
				predicted = offset + target.velocity * System.Math.Min(travelTicks, MaxLeadTicks);
			}
			return direction;
		}

		// Direction to throw at `speed` so a shot flying with `physics` passes through `offset` (relative to
		// the thrower), and how many ticks it takes to get there. Tries throw angles from low to high and
		// picks the flattest arc that gets there. Out of reach: the throw that gets closest.
		private static Vector2 LobDirection(Vector2 offset, float speed, LobPhysics physics, out float travelTicks)
		{
			int side = offset.X >= 0f ? 1 : -1;
			float x = System.Math.Abs(offset.X);
			if (x < 8f)
			{
				// Right above or below: throw straight up or down
				travelTicks = System.Math.Abs(offset.Y) / System.Math.Max(speed, 1f);
				return new Vector2(0f, offset.Y < 0f ? -1f : 1f);
			}

			const float minAngle = -1.2f; // about -70 degrees (throwing downward)
			const float maxAngle = 1.4f;  // about 80 degrees (throwing upward)
			float bestError = float.MaxValue;
			float bestAngle = MathHelper.PiOver4;
			float bestTicks = 0f;
			float prevAngle = minAngle;
			float prevMiss = float.NaN;
			for (int step = 0; step <= LobAngleSteps; step++)
			{
				float angle = MathHelper.Lerp(minAngle, maxAngle, step / (float)LobAngleSteps);
				float miss = SimulateLob(angle, speed, physics, x, offset.Y, out float ticks);
				if (float.IsNaN(miss))
				{
					prevMiss = float.NaN;
					continue; // doesn't get that far
				}

				// Going from low to high, the first angle where the shot goes from passing below the enemy
				// to passing above it is the flattest arc that hits: narrow it down between the two angles
				if (!float.IsNaN(prevMiss) && prevMiss > 0f && miss <= 0f)
				{
					float low = prevAngle, high = angle;
					for (int i = 0; i < 10; i++)
					{
						float mid = (low + high) / 2f;
						float midMiss = SimulateLob(mid, speed, physics, x, offset.Y, out _);
						if (!float.IsNaN(midMiss) && midMiss > 0f)
							low = mid;
						else
							high = mid;
					}
					bestAngle = (low + high) / 2f;
					SimulateLob(bestAngle, speed, physics, x, offset.Y, out bestTicks);
					bestError = 0f;
					break;
				}

				// Out of reach: remember the throw that comes closest
				if (System.Math.Abs(miss) < bestError)
				{
					bestError = System.Math.Abs(miss);
					bestAngle = angle;
					bestTicks = ticks;
				}
				prevAngle = angle;
				prevMiss = miss;
			}

			travelTicks = bestTicks;
			// angle is measured upward from horizontal; screen Y points down
			return new Vector2(side * (float)System.Math.Cos(bestAngle), -(float)System.Math.Sin(bestAngle));
		}

		// Flies a shot thrown at `angle` (up from horizontal) until it's `targetX` px away sideways. Returns how
		// far below `targetY` it is at that point (px; negative = above), and the ticks it took. If it never
		// gets that far (drag stops it, or it takes too long), returns NaN.
		private static float SimulateLob(float angle, float speed, LobPhysics physics, float targetX, float targetY,
			out float ticks)
		{
			float vx = (float)System.Math.Cos(angle) * speed;
			float vy = -(float)System.Math.Sin(angle) * speed;
			float px = 0f, py = 0f;
			for (int t = 1; t <= LobSimulateTicks; t++)
			{
				float prevX = px, prevY = py;
				px += vx;
				py += vy;
				if (px >= targetX)
				{
					// Interpolate to exactly targetX for a precise height and time
					float f = vx > 0f ? (targetX - prevX) / vx : 1f;
					float yAtTarget = prevY + (py - prevY) * f;
					ticks = t - 1 + f;
					return yAtTarget - targetY; // screen Y points down, so positive = below the target
				}
				if (t > physics.GravityDelay)
					vy = System.Math.Min(vy + physics.Gravity, physics.MaxFall);
				vx *= physics.Drag;
			}
			ticks = LobSimulateTicks;
			return float.NaN;
		}
	}
}
