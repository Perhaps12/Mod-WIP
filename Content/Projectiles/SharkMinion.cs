using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Buffs;

namespace Wprint.Content.Projectiles
{
	// Shark minion from the Shark Staff (Content/Items/SharkStaff.cs).
	//   Idle:      drifts slowly after the player.
	//   Attacking: moves relative to the player (carried along as the player moves), so it stays close by.
	//              When an enemy is within TargetRange of the player, drifts slowly toward it,
	//              never going more than LeashRange from the player, and fires Bubble Gun bubbles at it
	//              at the Bubble Gun's fire rate (like holding the Bubble Gun down).
	public class SharkMinion : ModProjectile
	{
		const float TargetRange = 30 * 16f;    // enemies within this distance of the player get attacked
		const float LeashRange = 20 * 16f;     // the shark never goes farther than this from the player while attacking
		const float IdleBehindDistance = 40f;  // idle spot: this many px behind the player...
		const float IdleAboveDistance = 50f;   // ...and this many px above
		const float IdleSpeed = 7f;            // px per tick when following the player (3x this when far behind)
		const float AttackMoveSpeed = 9f;      // px per tick when moving toward an enemy (on top of being carried with the player)
		const float StopDistance = 6 * 16f;    // stops approaching once this close to the enemy
		const float LeashReturnSpeed = 12f;    // px per tick it swims back in when it's outside LeashRange
		const float Inertia = 12f;             // higher = smoother, slower turns and acceleration
		const float BubbleSpread = 0.05f;      // random spread per bubble (Bubble Gun style)
		const int MinBubblesPerShot = 3;       // each shot fires this many bubbles at once...
		const int MaxBubblesPerShot = 4;       // ...up to this many (random), like the Bubble Gun
		const float FaceTurnSpeed = 0.6f;      // only turns to face its movement when moving sideways faster than this

		const int SharkPup = 774;              // vanilla Shark Pup pet projectile (placeholder sprite)
		static int Bubble => ModContent.ProjectileType<SharkBubble>(); // copy of the Bubble Gun's bubble that whips can tag

		ref float FireTimer => ref Projectile.localAI[0];

		// Placeholder: vanilla Shark Pup pet sprite
		public override string Texture => "Terraria/Images/Projectile_" + SharkPup;

		public override void SetStaticDefaults()
		{
			Main.projFrames[Type] = Main.projFrames[SharkPup];
			Main.projPet[Type] = true;                                   // needed for minions
			ProjectileID.Sets.MinionTargettingFeature[Type] = true;      // obeys right-click targeting
			ProjectileID.Sets.MinionSacrificable[Type] = true;           // can be replaced when summoning over the limit
			ProjectileID.Sets.CultistIsResistantTo[Type] = true;
		}

		public override void SetDefaults()
		{
			Projectile.width = 30;
			Projectile.height = 30;
			Projectile.tileCollide = false;
			Projectile.friendly = true;
			Projectile.minion = true;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.minionSlots = 1f;
			Projectile.penetrate = -1;
		}

		public override bool? CanCutTiles() => false;
		public override bool MinionContactDamage() => false; // it attacks with bubbles, not by touching

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!CheckActive(owner))
				return;

			NPC target = FindTarget(owner);
			if (target != null)
			{
				CarryWithPlayer(owner); // attacking: moves relative to the player
				AttackMovement(owner, target);
			}
			else
			{
				IdleMovement(owner);
			}

			UpdateShooting(owner, target);
			Animate(owner, target);
		}

		// Keeps the shark alive while the buff is up
		bool CheckActive(Player owner)
		{
			if (owner.dead || !owner.active)
			{
				owner.ClearBuff(ModContent.BuffType<SharkMinionBuff>());
				return false;
			}
			if (owner.HasBuff(ModContent.BuffType<SharkMinionBuff>()))
				Projectile.timeLeft = 2;
			return true;
		}

		// Right-click target first, otherwise the closest enemy within TargetRange of the player
		NPC FindTarget(Player owner)
		{
			if (owner.HasMinionAttackTargetNPC)
			{
				NPC npc = Main.npc[owner.MinionAttackTargetNPC];
				if (npc.CanBeChasedBy(this) && Vector2.Distance(npc.Center, owner.Center) <= TargetRange)
					return npc;
			}

			NPC closest = null;
			float closestDist = TargetRange;
			foreach (NPC npc in Main.ActiveNPCs)
			{
				if (!npc.CanBeChasedBy(this))
					continue;
				float dist = Vector2.Distance(npc.Center, owner.Center);
				if (dist < closestDist)
				{
					closestDist = dist;
					closest = npc;
				}
			}
			return closest;
		}

		// While attacking, the shark moves relative to the player: every tick it's first carried along by
		// however far the player moved, then its own slow velocity is added on top. So it stays close to the
		// player no matter how fast they go, while its own movement stays slow. Runs on every client, so it
		// follows the player smoothly in multiplayer too.
		void CarryWithPlayer(Player owner)
		{
			Projectile.position += owner.position - owner.oldPosition;
		}

		// Idle: follow a spot behind and above the player at its own slow speed (not carried along)
		void IdleMovement(Player owner)
		{
			// Sit right behind (and a bit above) the player. There's only ever one shark, so no
			// minionPos spacing: it always takes the same spot, whatever other minions are out.
			Vector2 home = owner.Center + new Vector2(-owner.direction * IdleBehindDistance, -IdleAboveDistance);
			Vector2 toHome = home - Projectile.Center;
			float dist = toHome.Length();

			// Way too far (e.g. the player teleported): jump back
			if (dist > 2000f)
			{
				Projectile.Center = home;
				Projectile.velocity = Vector2.Zero;
				Projectile.netUpdate = true;
				return;
			}

			// Slow drift, but speed up if falling far behind so it can keep up
			float speed = dist > 600f ? IdleSpeed * 3f : IdleSpeed;
			if (dist < 20f)
				speed = 0f; // close enough: settle
			MoveToward(toHome, speed);
		}

		// Attacking: drift slowly toward the enemy, staying within LeashRange of the player
		void AttackMovement(Player owner, NPC target)
		{
			Vector2 toTarget = target.Center - Projectile.Center;
			float speed = toTarget.Length() > StopDistance ? AttackMoveSpeed : 0f;
			MoveToward(toTarget, speed);

			ApplyLeash(owner);
		}

		// Keeps the shark within LeashRange of the player without ever teleporting it:
		//   - about to cross the edge: the outward part of its velocity is removed, so it slides along the edge
		//   - already outside (e.g. it was lagging behind when the attack started): it swims back in at LeashReturnSpeed
		// (Setting the velocity to the whole gap instead made it jump, and that huge velocity then lingered.)
		void ApplyLeash(Player owner)
		{
			Vector2 fromPlayer = Projectile.Center - owner.Center;
			float dist = fromPlayer.Length();
			if (dist < 1f)
				return;

			Vector2 outward = fromPlayer / dist;
			float outwardSpeed = Vector2.Dot(Projectile.velocity, outward);

			if (dist > LeashRange)
			{
				if (outwardSpeed > -LeashReturnSpeed)
					Projectile.velocity -= outward * (outwardSpeed + LeashReturnSpeed); // head back in
			}
			else if (dist + outwardSpeed > LeashRange && outwardSpeed > 0f)
			{
				Projectile.velocity -= outward * outwardSpeed; // stop at the edge
			}
		}

		void MoveToward(Vector2 offset, float speed)
		{
			Vector2 desired = offset.SafeNormalize(Vector2.Zero) * speed;
			Projectile.velocity = (Projectile.velocity * (Inertia - 1f) + desired) / Inertia;
		}

		// Fires like a held-down Bubble Gun: same projectile, fire rate, shot speed and sound,
		// with several bubbles per shot
		void UpdateShooting(Player owner, NPC target)
		{
			Item bubbleGun = ContentSamples.ItemsByType[ItemID.BubbleGun];

			// Capped so the first shot fires as soon as it's allowed to
			FireTimer = System.Math.Min(FireTimer + 1, bubbleGun.useTime);
			if (target == null || FireTimer < bubbleGun.useTime)
				return;
			if (!Collision.CanHitLine(Projectile.Center, 1, 1, target.position, target.width, target.height))
				return;

			FireTimer = 0;
			if (bubbleGun.UseSound.HasValue)
				SoundEngine.PlaySound(bubbleGun.UseSound.Value, Projectile.Center);

			if (Main.myPlayer != Projectile.owner)
				return; // only the owner spawns the bubbles; they sync to everyone else

			Vector2 aim = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * bubbleGun.shootSpeed;

			// Bubbles inherit the shark's actual movement: its own velocity plus, while attacking,
			// the player's movement that it's being carried along by
			Vector2 sharkMovement = Projectile.velocity + (owner.position - owner.oldPosition);

			int count = Main.rand.Next(MinBubblesPerShot, MaxBubblesPerShot + 1);
			for (int i = 0; i < count; i++)
			{
				// Each bubble gets its own random spread and speed, so the shot comes out as a cluster
				Vector2 velocity = aim * Main.rand.NextFloat(0.8f, 1.1f)
					+ new Vector2(Main.rand.Next(-40, 41), Main.rand.Next(-40, 41)) * BubbleSpread
					+ sharkMovement;
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity,
					Bubble, Projectile.damage, Projectile.knockBack, Projectile.owner);
			}
		}

		void Animate(Player owner, NPC target)
		{
			// Attacking: face the enemy.
			// Idle: face the way it's moving, but only when clearly moving sideways; while settling
			// near the player (small back-and-forth corrections) face the same way as the player,
			// so it doesn't keep flipping.
			int face;
			if (target != null)
				face = target.Center.X >= Projectile.Center.X ? 1 : -1;
			else if (System.Math.Abs(Projectile.velocity.X) > FaceTurnSpeed)
				face = Projectile.velocity.X > 0 ? 1 : -1;
			else
				face = owner.direction;
			Projectile.direction = face;
			Projectile.spriteDirection = -face; // the Shark Pup sprite faces the other way by default

			Projectile.frame = 0; // still image: always the first frame of the sprite sheet
		}
	}
}
