using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	// ---- Movement: Pygmy-style (vanilla aiStyle 26) ground minion ----
	//   Walking: walks on the ground with gravity, steps up 1-tile ledges, jumps walls (higher walls =
	//            bigger jump). Idle: lines up behind the player. Attacking: walks to its attack spot
	//            (see TownVillagerMinion.Combat.cs).
	//   Flying:  when the villagers fall too far behind sideways, or the player stands somewhere too high to
	//            jump to, they ignore tiles and fly back toward the floor below their line-up spot, landing
	//            as soon as they can. They prefer the ground: the player flying above them doesn't make them fly.
	//   Very far away: teleports to the player.
	public partial class TownVillagerMinion
	{
		private const float WalkSpeed = 6f;            // max walk speed (px/tick)
		private const float WalkSpeedFar = 9f;         // max walk speed when far from where it wants to be
		private const float WalkAcceleration = 0.25f;
		private const float Gravity = 0.4f;
		private const float MaxFallSpeed = 10f;
		private const float FlyStartDistance = 500f;   // starts flying back when this far from the player
		private const float AttackFlyStartDistance = 1400f; // ...or this far while attacking an enemy (lets them roam farther)
		private const float FlyLandDistance = 160f;    // fallback landing range around the player (when its fly spot is inside blocks)
		private const float FlyArriveDistance = 24f;   // lands once this close to its fly spot
		private const float FlySpeed = 10f;            // base fly speed; goes faster the farther away it is
		private const float FlyInertia = 20f;
		private const float FlyAboveHeight = 3 * 16f;  // flies to this many px above the player's center first (phase 1)
		private const float FlyAboveGround = 8f;       // then flies to just above the ground below its spot (phase 2)
		private const int GroundSearchTiles = 30;      // how far below the player's level it looks for ground to land on
		private const float FlyLevelArriveDistance = 32f; // phase 1 is done once this close to its spot at the player's level
		private const float TeleportDistance = 2200f;
		private const float LineUpStartDistance = 64f; // px from the player to the first villager in the line-up
		private const float LineUpSpacing = 48f;       // px between villagers lined up behind the player
		private const float MaxJumpSpeed = 10f;        // strongest jump up to the player/enemy (about 7.5 tiles high)
		private const float MaxJumpHeight = MaxJumpSpeed * MaxJumpSpeed / (2f * Gravity) - 12f; // highest it can jump up to (px)
		private const float FlyUpSidewaysRange = 10 * 16f; // fly up to a higher player only when the nearest villager is this close sideways
		private const float JumpUpMinHeight = 40f;     // only jumps up to the player if they're at least this much higher
		private const int JumpUpCooldownTicks = 30;    // wait between jumps up to the player/enemy

		// Melee villagers (chasing an enemy)
		private const float MeleeChaseSpeed = 9f;      // max walk speed while chasing an enemy...
		private const float MeleeChaseSpeedFar = 12f;  // ...and when the enemy is far away
		private const float MeleeChaseAcceleration = 0.6f; // speeds up and turns around faster than walking
		private const float MeleeStickDistance = 24f;  // this close to the enemy: matches its movement to stay on it
		private const float MeleeMaxJumpSpeed = 18f;   // jump at enemies much higher (about 25 tiles)
		private const float MeleeJumpMinHeight = 16f;  // jumps at enemies at least this much higher...
		private const float MeleeJumpSidewaysRange = 25 * 16f; // ...and this close sideways
		private const int MeleeJumpCooldownTicks = 6;
		private const float MeleeLungeRange = 6 * 16f;  // lunges at enemies this close sideways (at about the same level)
		private const float MeleeLungeJumpSpeed = 7f;   // upward speed of the lunge hop
		private const float MeleeLungeSpeed = 11f;      // sideways speed of the lunge hop
		private const int MeleeLungeCooldownTicks = 30;

		// Midrange villagers climbing up toward an enemy above (see TownVillagerMinion.Combat.cs)
		private const float ClimbMaxJumpSpeed = 12f;          // stronger jump while climbing (about 11 tiles)
		private const float ClimbJumpSidewaysRange = 6 * 16f; // jumps up once this close sideways to the spot
		private const int ClimbJumpCooldownTicks = 20;

		private int jumpCooldown;
		private bool onGround;        // standing on something this tick (set by WalkMovement, used by Animate)
		private bool reachedLevel;    // flying: finished phase 1 (got to the player's level), now seeking the floor
		private bool dropToTarget;    // melee chasing an enemy below: drop through platforms
		private bool chasingAsMelee;  // melee with a target (platform dropping follows the enemy, not the player)

		// Flying back to the player (ai[1], so it syncs in multiplayer)
		private bool Flying
		{
			get => Projectile.ai[1] == 1f;
			set => Projectile.ai[1] = value ? 1f : 0f;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (oldVelocity.X != Projectile.velocity.X)
				Projectile.velocity.X = 0f;
			if (oldVelocity.Y != Projectile.velocity.Y)
				Projectile.velocity.Y = 0f;
			return false;
		}

		public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac)
		{
			// Drop through platforms toward whatever it's heading for: a melee villager chasing an enemy
			// follows the enemy; everyone else follows the player
			if (Climbing)
				fallThrough = false; // climbing up toward an enemy: stand on platforms, don't drop through
			else if (chasingAsMelee)
				fallThrough = dropToTarget;
			else if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
				fallThrough = Main.player[Projectile.owner].Bottom.Y > Projectile.Bottom.Y + 12f;
			return true;
		}

		private void Move(Player owner, NPC target)
		{
			float ownerDistance = Vector2.Distance(Projectile.Center, owner.Center);

			if (ownerDistance > TeleportDistance)
			{
				Projectile.Center = new Vector2(IdleSpotX(owner), owner.Bottom.Y - Projectile.height / 2f);
				Projectile.velocity = Vector2.Zero;
				Flying = false;
				Projectile.netUpdate = true;
				return;
			}

			// Melee villagers fighting an enemy stay on it instead of following the group back to the player:
			// they only fly back if they themselves get too far away from the player, and if they were flying
			// back when an enemy showed up, they drop out of the air (once clear of blocks) to go after it
			bool meleeFighting = target != null && Range == CombatRange.Melee;
			if (meleeFighting)
			{
				bool tooFar = System.Math.Abs(Projectile.Center.X - owner.Center.X) > AttackFlyStartDistance;
				if (!Flying && tooFar)
				{
					Flying = true;
					reachedLevel = false;
					Projectile.netUpdate = true;
				}
				else if (Flying && !tooFar
					&& !Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height))
				{
					Flying = false;
					reachedLevel = false;
					Projectile.tileCollide = true;
					Projectile.netUpdate = true;
				}
			}
			// Start flying: one shared decision for all of the player's villagers, so they take off together
			else if (!Flying && GroupShouldFly(owner, target))
			{
				Flying = true;
				reachedLevel = false; // start with phase 1: get to the player's level
				Projectile.netUpdate = true;
			}

			if (Flying)
				FlyMovement(owner, ownerDistance);
			else
				WalkMovement(owner, target);
		}

		// Whether the villagers should fly, decided the same way for all of them from the villager nearest
		// to the player (so they don't take off at different times depending on their place in the line-up).
		// They prefer the ground: the player flying around above them is not a reason to fly.
		//   - even the nearest villager is more than FlyStartDistance from the player sideways
		//     (AttackFlyStartDistance while there's an enemy to fight, so they can go farther to attack)
		//   - the player is standing on something higher up than the nearest villager can jump to
		private bool GroupShouldFly(Player owner, NPC target)
		{
			Projectile nearest = NearestVillager(owner) ?? Projectile;
			float startDistance = target != null ? AttackFlyStartDistance : FlyStartDistance;
			if (System.Math.Abs(nearest.Center.X - owner.Center.X) > startDistance)
				return true;

			float heightAbove = nearest.Bottom.Y - owner.Bottom.Y;
			bool ownerStanding = owner.velocity.Y == 0f;
			if (!ownerStanding)
				return false;
			bool closeSideways = System.Math.Abs(owner.Center.X - nearest.Center.X) < FlyUpSidewaysRange;
			return heightAbove > FlyStartDistance || (closeSideways && heightAbove > MaxJumpHeight);
		}

		// The player's villager closest to them
		private static Projectile NearestVillager(Player owner)
		{
			int type = ModContent.ProjectileType<TownVillagerMinion>();
			Projectile nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Projectile p in Main.ActiveProjectiles)
			{
				if (p.owner != owner.whoAmI || p.type != type)
					continue;
				float distance = Vector2.DistanceSquared(p.Center, owner.Center);
				if (distance < nearestDistance)
				{
					nearestDistance = distance;
					nearest = p;
				}
			}
			return nearest;
		}

		// Top Y (in world px) of the first standable tile (solid block or platform) at x, from startY down,
		// searching up to maxTiles tiles. Null if there's none in range.
		private static float? FindGroundBelow(float x, float startY, int maxTiles)
		{
			int tileX = (int)(x / 16f);
			int startTileY = (int)(startY / 16f);
			for (int y = startTileY; y <= startTileY + maxTiles; y++)
			{
				if (!WorldGen.InWorld(tileX, y))
					return null;
				Tile tile = Main.tile[tileX, y];
				if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
					return y * 16f;
			}
			return null;
		}

		// Idle spot: lined up behind the player, one villager after another, in summon order
		private float IdleSpotX(Player owner) =>
			owner.Center.X - owner.direction * (LineUpStartDistance + LineUpPosition(owner) * LineUpSpacing);

		// Place in the line-up: how many of the player's villagers come before this one in summon order
		// (the Villagers list, starting from FirstVillager). Vanilla's minionPos isn't used, because it
		// follows projectile slots, which shuffle when villagers are re-summoned.
		private int LineUpPosition(Player owner)
		{
			int type = ModContent.ProjectileType<TownVillagerMinion>();
			int myOrder = SummonOrder(VillagerIndex);
			int position = 0;
			foreach (Projectile p in Main.ActiveProjectiles)
			{
				if (p.owner == owner.whoAmI && p.type == type && p.whoAmI != Projectile.whoAmI
					&& SummonOrder((int)p.ai[0]) < myOrder)
					position++;
			}
			return position;
		}

		// 0 for FirstVillager, 1 for the one after it, and so on (wrapping around the list)
		private static int SummonOrder(int villagerIndex) =>
			((villagerIndex - FirstVillager) % VillagerCount + VillagerCount) % VillagerCount;

		private void FlyMovement(Player owner, float ownerDistance)
		{
			Projectile.tileCollide = false;

			// Like Pygmies: fly to its own spot in the line-up behind the player (the spot it would walk to),
			// in two phases:
			//   1. Get to the player's level: fly to that spot at the player's height (a little above).
			//      No landing on the way, so it doesn't stop on a lower ledge and need to fly again.
			//   2. Seek the floor: from there, fly down to just above the ground below the spot and land.
			//      (If there's no ground below, it stays at the player's level.)
			float spotX = IdleSpotX(owner);
			Vector2 levelSpot = new Vector2(spotX, owner.Center.Y - FlyAboveHeight);
			if (!reachedLevel && Vector2.Distance(Projectile.Center, levelSpot) < FlyLevelArriveDistance)
				reachedLevel = true;

			Vector2 flySpot = levelSpot;
			if (reachedLevel)
			{
				float? groundY = FindGroundBelow(spotX, levelSpot.Y, GroundSearchTiles);
				if (groundY.HasValue)
					flySpot = new Vector2(spotX, groundY.Value - Projectile.height / 2f - FlyAboveGround);
			}
			Vector2 toSpot = flySpot - Projectile.Center;
			float speed = FlySpeed + ownerDistance / 60f; // faster the farther behind it is
			if (toSpot.Length() < speed)
				speed = toSpot.Length();                 // slow down on arrival instead of overshooting
			Vector2 desired = toSpot.SafeNormalize(Vector2.Zero) * speed;
			Projectile.velocity = (Projectile.velocity * (FlyInertia - 1f) + desired) / FlyInertia;

			// Land once it isn't inside tiles and:
			//   - (phase 2) it reached the floor spot, or it's near its spot sideways with ground just below it
			//     (even if the player is still in the air), or
			//   - its level spot is inside blocks (e.g. the player is against a wall) so it can't get there,
			//     and it's near the player at about their height
			bool inOpenSpace = !Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height);
			bool reachedSpot = toSpot.Length() < FlyArriveDistance;
			bool groundJustBelow = System.Math.Abs(spotX - Projectile.Center.X) < FlyLandDistance
				&& FindGroundBelow(Projectile.Center.X, Projectile.Bottom.Y, 2).HasValue;
			bool levelSpotBlocked = Collision.SolidCollision(levelSpot - Projectile.Size / 2f, Projectile.width, Projectile.height);
			bool fallbackLanding = levelSpotBlocked && ownerDistance < FlyLandDistance
				&& Projectile.Bottom.Y <= owner.Bottom.Y + 16f;
			if (inOpenSpace && ((reachedLevel && (reachedSpot || groundJustBelow)) || fallbackLanding))
			{
				Flying = false;
				reachedLevel = false;
				Projectile.tileCollide = true;
				if (Projectile.velocity.Y < -2f)
					Projectile.velocity.Y = -2f;
				Projectile.netUpdate = true;
			}
		}

		private void WalkMovement(Player owner, NPC target)
		{
			Projectile.tileCollide = true;

			// Where to walk to: an attack position for its combat range, or the idle spot behind the player
			float destinationX = target != null ? AttackSpotX(target) : IdleSpotX(owner);
			if (target == null)
			{
				repositioning = false;
				climbSpot = null;
			}

			// Horizontal: accelerate toward the destination, slow down near it
			float offsetX = destinationX - Projectile.Center.X;
			int direction = System.Math.Sign(offsetX);
			bool chasing = target != null && Range == CombatRange.Melee;
			if (chasing && System.Math.Abs(offsetX) < MeleeStickDistance)
			{
				// On the enemy: match its movement (plus a nudge toward the spot) so it sticks to it
				// instead of stopping and letting the enemy walk away
				float desiredX = target.velocity.X + offsetX * 0.2f;
				Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, desiredX, 0.35f);
			}
			else if (System.Math.Abs(offsetX) < 10f)
			{
				Projectile.velocity.X *= 0.8f;
				if (System.Math.Abs(Projectile.velocity.X) < 0.1f)
					Projectile.velocity.X = 0f;
			}
			else
			{
				// Melee villagers chase enemies faster (and can always outrun the enemy); everyone keeps up
				// with a running player
				bool far = System.Math.Abs(offsetX) > 300f;
				float maxSpeed = chasing
					? (far ? MeleeChaseSpeedFar : MeleeChaseSpeed)
					: (far ? WalkSpeedFar : WalkSpeed);
				maxSpeed = System.Math.Max(maxSpeed, System.Math.Abs(owner.velocity.X) + 1f);
				if (chasing)
					maxSpeed = System.Math.Max(maxSpeed, System.Math.Abs(target.velocity.X) + 2f);
				float acceleration = chasing ? MeleeChaseAcceleration : WalkAcceleration;

				Projectile.velocity.X += direction * acceleration;
				if (System.Math.Sign(Projectile.velocity.X) != direction)
					Projectile.velocity.X += direction * acceleration; // turn around faster
				Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X, -maxSpeed, maxSpeed);
			}

			// Walk up 1-tile steps smoothly
			Collision.StepUp(ref Projectile.position, ref Projectile.velocity, Projectile.width, Projectile.height,
				ref Projectile.stepSpeed, ref Projectile.gfxOffY);

			// Jump over walls (height of the jump matches the wall), or up to the player / enemy
			if (jumpCooldown > 0)
				jumpCooldown--;
			bool grounded = Projectile.velocity.Y == 0f;
			onGround = grounded; // remembered for the animation (velocity.Y isn't 0 any more once gravity is added below)
			if (grounded)
			{
				int wallHeight = direction != 0 ? WallHeightAhead(direction) : 0;
				if (wallHeight > 1)
				{
					Projectile.velocity.Y = JumpSpeedForWall(wallHeight);
				}
				else if (jumpCooldown <= 0)
				{
					bool meleeChasing = target != null && Range == CombatRange.Melee;
					float targetDx = target != null ? target.Center.X - Projectile.Center.X : 0f;
					float targetHeight = target != null ? Projectile.Bottom.Y - target.Bottom.Y : 0f; // > 0 = above

					// Melee: jump at an enemy above. If it's higher than it can jump, jump as high as it can anyway.
					if (meleeChasing && targetHeight > MeleeJumpMinHeight
						&& System.Math.Abs(targetDx) < MeleeJumpSidewaysRange)
					{
						if (!TryJumpUp(targetHeight, MeleeMaxJumpSpeed))
							Projectile.velocity.Y = -MeleeMaxJumpSpeed;
						jumpCooldown = MeleeJumpCooldownTicks;
					}
					// Melee: lunge at a nearby enemy at about the same level with a quick hop
					else if (meleeChasing && System.Math.Abs(targetDx) < MeleeLungeRange
						&& System.Math.Abs(targetHeight) <= MeleeJumpMinHeight)
					{
						// Hop toward where the enemy will be, not past it
						Projectile.velocity.Y = -MeleeLungeJumpSpeed;
						Projectile.velocity.X = MathHelper.Clamp(target.velocity.X + targetDx * 0.15f,
							-MeleeLungeSpeed, MeleeLungeSpeed);
						jumpCooldown = MeleeLungeCooldownTicks;
					}
					// Midrange climbing to a spot at the enemy's height (TownVillagerMinion.Combat.cs): when near
					// it and it's higher up, jump up onto it (stronger jump than normal)
					else if (target != null && Climbing)
					{
						float spotHeight = Projectile.Bottom.Y - climbSpot.Value.Y; // > 0 = spot is higher
						if (spotHeight > 16f && System.Math.Abs(climbSpot.Value.X - Projectile.Center.X) < ClimbJumpSidewaysRange)
						{
							if (!TryJumpUp(spotHeight, ClimbMaxJumpSpeed))
								Projectile.velocity.Y = -ClimbMaxJumpSpeed;
							jumpCooldown = ClimbJumpCooldownTicks;
						}
					}
					// Up to the player: only when they're standing on something higher up nearby
					// (melee villagers chasing an enemy focus on it instead).
					// Too high to jump: GroupShouldFly makes everyone fly up instead (like Pygmies).
					else if (!meleeChasing)
					{
						float ownerHeight = Projectile.Bottom.Y - owner.Bottom.Y;
						bool ownerStanding = owner.velocity.Y == 0f;
						if (ownerStanding && ownerHeight > JumpUpMinHeight
							&& System.Math.Abs(owner.Center.X - Projectile.Center.X) < 160f)
						{
							TryJumpUp(ownerHeight);
						}
					}
				}
			}

			// Melee villagers drop through platforms to reach an enemy below (see TileCollideStyle)
			dropToTarget = target != null && Range == CombatRange.Melee
				&& target.Bottom.Y > Projectile.Bottom.Y + 12f;
			chasingAsMelee = target != null && Range == CombatRange.Melee;

			// Gravity
			Projectile.velocity.Y = MathHelper.Min(Projectile.velocity.Y + Gravity, MaxFallSpeed);
		}

		// Jump just high enough to rise `height` px (plus a little margin). Returns false, without
		// jumping, if that needs more than `maxSpeed` (MaxJumpSpeed by default). Starts the cooldown so
		// it doesn't hop nonstop.
		private bool TryJumpUp(float height, float maxSpeed = MaxJumpSpeed)
		{
			float needed = (float)System.Math.Sqrt(2f * Gravity * (height + 12f));
			if (needed > maxSpeed)
				return false;
			Projectile.velocity.Y = -needed;
			jumpCooldown = JumpUpCooldownTicks;
			return true;
		}

		// How many tiles tall the wall directly ahead is (0 = no wall, 1 = a step StepUp can handle)
		private int WallHeightAhead(int direction)
		{
			int x = (int)((direction > 0 ? Projectile.Right.X + 4f : Projectile.Left.X - 4f) / 16f);
			int footY = (int)((Projectile.Bottom.Y - 1f) / 16f);
			int height = 0;
			for (int i = 0; i < 6; i++)
			{
				int y = footY - i;
				if (!WorldGen.InWorld(x, y))
					break;
				Tile tile = Main.tile[x, y];
				if (!(tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]))
					break;
				height++;
			}
			return height;
		}

		// Jump strength for a wall of the given height in tiles (like vanilla's ground minions)
		private static float JumpSpeedForWall(int tiles) => tiles switch
		{
			2 => -5.5f,
			3 => -6.5f,
			4 => -7.5f,
			5 => -8.5f,
			_ => -9.5f,
		};
	}
}
