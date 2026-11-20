using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Players
{
	// Per-player Clawline state:
	//  - ForcedVelocity: the hook sets this every tick it wants control of the player's movement.
	//    When it stops setting it, the player moves normally and keeps their velocity (momentum).
	//  - Air charges: only MaxAirCharges uses while airborne; touching the ground refills them.
	//  - Jump cancel: pressing Jump while the hook is out cancels it and jumps, like vanilla hooks.
	public class ClawlinePlayer : ModPlayer
	{
		public const int MaxAirCharges = 1;

		public Vector2? ForcedVelocity;
		public int AirCharges = MaxAirCharges;

		bool jumpHeldLastTick;

		int HookType => ModContent.ProjectileType<ClawlineHook>();

		bool HookOut => Player.ownedProjectileCounts[HookType] > 0;

		// Standing on solid ground or a platform (not just "vertical speed is 0", which is also
		// true while the Clawline holds the player still in midair)
		public bool OnGround =>
			Player.velocity.Y == 0f &&
			Collision.SolidCollision(Player.BottomLeft, Player.width, 1, true);

		// Checked by both the item (hotbar use) and the hook (Grapple key)
		public bool CanUseClawline() => !HookOut && (OnGround || AirCharges > 0);

		// Called by the hook when it spawns
		public void ConsumeChargeIfAirborne()
		{
			if (!OnGround && AirCharges > 0)
				AirCharges--;
		}

		// Called by the hook when it latches onto a tile or an enemy
		public void Recharge()
		{
			AirCharges = MaxAirCharges;
		}

		public override void PreUpdateMovement()
		{
			if (Player.whoAmI == Main.myPlayer)
			{
				bool jumpPressed = Player.controlJump && !jumpHeldLastTick;
				jumpHeldLastTick = Player.controlJump;

				// Jump only cancels during the pull, not while the hook is flying or hovering
				if (jumpPressed && TryCancelPullingHook())
				{
					ForcedVelocity = null;

					// Same as releasing a vanilla grappling hook: jump, keeping horizontal momentum
					Player.velocity.Y = -Player.jumpSpeed * Player.gravDir;
					Player.jump = Player.jumpHeight;
					Player.fallStart = (int)(Player.position.Y / 16f);
					return;
				}
			}

			if (ForcedVelocity is Vector2 v)
			{
				// While held still, vanilla thinks you're on the ground (vertical speed is 0), so a
				// Jump press would start a jump that kicks in after the pull. Clear it.
				Player.jump = 0;
				Player.velocity = v;                                   // overrides gravity for this tick
				Player.fallStart = (int)(Player.position.Y / 16f);     // no fall damage from clawline movement
				ForcedVelocity = null;
			}
		}

		public override void PostUpdate()
		{
			// Touching the ground recharges the air uses
			if (!HookOut && OnGround)
				AirCharges = MaxAirCharges;
		}

		// Kills this player's Clawline if it's currently pulling. Returns true if one was cancelled.
		bool TryCancelPullingHook()
		{
			if (!HookOut)
				return false;

			bool cancelled = false;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == Player.whoAmI && p.type == HookType
					&& p.ModProjectile is ClawlineHook hook && hook.IsPulling)
				{
					p.Kill();
					cancelled = true;
				}
			}
			return cancelled;
		}
	}
}
