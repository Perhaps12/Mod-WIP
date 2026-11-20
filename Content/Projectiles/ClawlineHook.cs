using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Projectiles
{
	// The Clawline hook. Spawned by Content/Items/Clawline.cs.
	//
	// States:
	//   Flying     - travels toward the mouse; player hangs in the air (no gravity)
	//   Latched    - stuck in a tile or enemy, or floating at max range after a miss; short pause before the pull
	//   Pulling    - yanks the player to the hook. On arrival the player keeps their velocity,
	//                or, if the hook is in an enemy, bounces back and upward off it (pogo-style).
	//                Jump can't cancel the hook in any state.
	//                If the enemy dies, the hook stays where it was and still pulls (no bounce).
	public class ClawlineHook : ModProjectile
	{
		const float MaxRange = 20 * 16f;   // 20 tiles
		const int LatchDelayTicks = 4;     // pause after landing before the pull starts
		const int MissHoverTicks = 4;     // how long the hook floats at max range before pulling
		const float PullSpeed = 22f;       // px per tick while being pulled
		const float ReleaseSpeedMultiplier = 0.6f; // fraction of PullSpeed the player keeps after reaching the hook
		const float BounceHorizontalSpeed = 6f; // px per tick pushed back away from the enemy
		const float BounceUpSpeed = 9f;         // px per tick launched upward off the enemy
		const int MaxPullTicks = 60;       // safety timeout

		enum State { Flying, Latched, Pulling }

		State CurrentState
		{
			get => (State)Projectile.ai[0];
			set { Projectile.ai[0] = (int)value; Timer = 0; Projectile.netUpdate = true; }
		}

		ref float Timer => ref Projectile.ai[1];

		// whoAmI of the enemy we're stuck in, or -1. Stored +1 so the default 0 means "none".
		int TargetNPC
		{
			get => (int)Projectile.ai[2] - 1;
			set => Projectile.ai[2] = value + 1;
		}

		// True when the hook reached max range without hitting anything
		bool HoveringInAir
		{
			get => Projectile.localAI[0] == 1;
			set => Projectile.localAI[0] = value ? 1 : 0;
		}

		// Direction from the player to the hook when the pull started (localAI[1], localAI[2])
		Vector2 ReleaseDirection
		{
			get => new Vector2(Projectile.localAI[1], Projectile.localAI[2]);
			set { Projectile.localAI[1] = value.X; Projectile.localAI[2] = value.Y; }
		}

		// Temporary sprite: reuse the sample sword
		public override string Texture => "Wprint/Content/Items/sample";

		public override void SetStaticDefaults()
		{
			// Marks this as a grappling hook: the Clawline item can go in the hook slot
			// and be fired with the Grapple key (E). Our AI is custom (aiStyle 0), so vanilla
			// grapple physics never touch the player; we only borrow the slot + keybind.
			Main.projHook[Type] = true;
		}

		// Grapple key: only one Clawline out at a time, and limited uses while airborne
		public override bool? CanUseGrapple(Player player)
		{
			return player.GetModPlayer<ClawlinePlayer>().CanUseClawline();
		}

		public override void OnSpawn(IEntitySource source)
		{
			if (Projectile.owner == Main.myPlayer)
				Main.player[Projectile.owner].GetModPlayer<ClawlinePlayer>().ConsumeChargeIfAirborne();

			// Vanilla's grapple key spawns hooks with 0 damage, so pull the damage from the item ourselves
			if (Projectile.damage <= 0 && source is EntitySource_ItemUse itemSource && itemSource.Item != null)
			{
				Player owner = Main.player[Projectile.owner];
				Projectile.damage = owner.GetWeaponDamage(itemSource.Item);
				Projectile.knockBack = owner.GetWeaponKnockback(itemSource.Item);
			}
		}

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;          // don't die on hit; we handle it ourselves
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 600;
			Projectile.extraUpdates = 1;        // 2 updates per tick so the fast hook doesn't skip through tiles
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
		}

		// Only damages while flying out
		public override bool? CanDamage() => CurrentState == State.Flying ? null : false;

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead)
			{
				Projectile.Kill();
				return;
			}

			ClawlinePlayer clawPlayer = owner.GetModPlayer<ClawlinePlayer>();
			int updatesPerTick = Projectile.MaxUpdates;

			switch (CurrentState)
			{
				case State.Flying:
					clawPlayer.ForcedVelocity = Vector2.Zero; // hang in the air
					Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4; // sword sprite points up-right

					if (Vector2.Distance(Projectile.Center, owner.MountedCenter) >= MaxRange)
					{
						// Missed: hover in midair for a moment, then pull the player to it
						Projectile.tileCollide = false;
						Projectile.velocity = Vector2.Zero;
						HoveringInAir = true;
						CurrentState = State.Latched;
					}
					break;

				case State.Latched:
					clawPlayer.ForcedVelocity = Vector2.Zero;
					Projectile.velocity = Vector2.Zero;

					if (TargetNPC >= 0)
					{
						NPC npc = Main.npc[TargetNPC];
						if (!npc.active)
							TargetNPC = -1;                 // enemy died: hook stays where it was and still pulls
						else
							Projectile.Center = npc.Center; // stick to the enemy
					}

					int delay = HoveringInAir ? MissHoverTicks : LatchDelayTicks;
					if (++Timer >= delay * updatesPerTick)
					{
						// Remember the direction of travel so the release keeps it
						Vector2 toHookAtStart = Projectile.Center - owner.MountedCenter;
						ReleaseDirection = toHookAtStart == Vector2.Zero
							? Vector2.UnitX * owner.direction
							: Vector2.Normalize(toHookAtStart);
						CurrentState = State.Pulling;
					}
					break;

				case State.Pulling:
					UpdatePull(owner, clawPlayer, updatesPerTick);
					break;

			}
		}

		void UpdatePull(Player owner, ClawlinePlayer clawPlayer, int updatesPerTick)
		{
			NPC target = null;
			if (TargetNPC >= 0)
			{
				target = Main.npc[TargetNPC];
				if (!target.active)
				{
					// Enemy died mid-pull: finish pulling to its last position, no bounce
					target = null;
					TargetNPC = -1;
				}
				else
					Projectile.Center = target.Center;
			}

			Vector2 toHook = Projectile.Center - owner.MountedCenter;
			float dist = toHook.Length();
			Vector2 pullDir = dist > 0 ? toHook / dist : Vector2.UnitX * owner.direction;

			if (target != null)
			{
				// i-frames while being pulled into the enemy so contact damage doesn't hit you
				owner.immune = true;
				owner.immuneNoBlink = true;
				if (owner.immuneTime < 4)
					owner.immuneTime = 4;
			}

			Timer++;
			bool arrived = dist <= PullSpeed || (target != null && owner.Hitbox.Intersects(target.Hitbox));
			bool stuck = Timer > 3 * updatesPerTick && owner.velocity.Length() < PullSpeed * 0.3f; // blocked by a wall
			bool timedOut = Timer > MaxPullTicks * updatesPerTick;

			if (arrived || stuck || timedOut)
			{
				if (target != null)
				{
					// Pogo-style bounce: back horizontally (away from the enemy), always upward vertically
					float side = owner.Center.X - target.Center.X;
					int awayDir = side != 0 ? System.Math.Sign(side) : -System.Math.Sign(pullDir.X);
					if (awayDir == 0)
						awayDir = -owner.direction;
					clawPlayer.ForcedVelocity = new Vector2(awayDir * BounceHorizontalSpeed, -BounceUpSpeed * owner.gravDir);
					owner.immuneTime = 20; // brief grace after the bounce
				}
				else
				{
					// Keep the pull momentum (it's applied once, then normal physics take over).
					// Uses the direction saved at the start of the pull, not the leftover gap to the
					// hook, which can point almost anywhere once the player is this close.
					clawPlayer.ForcedVelocity = stuck ? owner.velocity : ReleaseDirection * PullSpeed * ReleaseSpeedMultiplier;
				}
				Projectile.Kill();
				return;
			}

			clawPlayer.ForcedVelocity = pullDir * PullSpeed;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (CurrentState == State.Flying)
			{
				Projectile.tileCollide = false;
				Projectile.velocity = Vector2.Zero;
				CurrentState = State.Latched;
				RechargeOwner(); // grabbing a wall refills air uses
			}
			return false; // don't die on tile hit
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (CurrentState == State.Flying)
			{
				TargetNPC = target.whoAmI;
				Projectile.tileCollide = false;
				Projectile.velocity = Vector2.Zero;
				CurrentState = State.Latched;
				RechargeOwner(); // hooking an enemy refills air uses
			}
		}

		void RechargeOwner()
		{
			if (Projectile.owner == Main.myPlayer)
				Main.player[Projectile.owner].GetModPlayer<ClawlinePlayer>().Recharge();
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Player owner = Main.player[Projectile.owner];

			// Draw the vanilla grappling hook chain from the player to the hook
			Texture2D chain = TextureAssets.Chain.Value;
			Vector2 start = owner.MountedCenter;
			Vector2 diff = Projectile.Center - start;
			float length = diff.Length();
			if (length > 0)
			{
				Vector2 dir = diff / length;
				float chainRot = diff.ToRotation() - MathHelper.PiOver2;
				for (float d = 0; d < length; d += chain.Height)
				{
					Vector2 pos = start + dir * d;
					Color c = Lighting.GetColor(pos.ToTileCoordinates());
					Main.EntitySpriteDraw(chain, pos - Main.screenPosition, null, c, chainRot,
						chain.Size() / 2f, 1f, SpriteEffects.None);
				}
			}

			// Draw the hook itself
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
				Projectile.rotation, tex.Size() / 2f, Projectile.scale, SpriteEffects.None);
			return false;
		}
	}
}
