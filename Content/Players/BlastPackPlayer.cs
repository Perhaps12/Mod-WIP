using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Wprint.Content.Items;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Players
{
	// Per-player Blast Pack state:
	//  - Right click while holding the Blast Pack detonates every Blast Pack you have out.
	//    Done here instead of as the item's alt use so it ignores the item's use time:
	//    you can detonate at any moment, even while the throw animation is still playing.
	//    Right clicks that interact with something (doors, chests, town NPCs...) don't detonate.
	//  - Low gravity after a launch: full reduction for a moment, then it fades back to normal.
	public class BlastPackPlayer : ModPlayer
	{
		const int LowGravityHoldTicks = 20;    // full reduction for this long after a launch
		const int LowGravityFadeTicks = 60;    // then fades back to normal gravity over this long
		const float LowGravityStrength = 0.4f; // fraction of gravity removed at full reduction

		const int LowGravityTotalTicks = LowGravityHoldTicks + LowGravityFadeTicks;

		bool rightHeldLastTick;
		int lowGravityTimer;                   // counts down from LowGravityTotalTicks

		// Called by the Blast Pack when it launches this player
		public void StartLowGravity()
		{
			lowGravityTimer = LowGravityTotalTicks;
		}

		public override void PreUpdateMovement()
		{
			if (lowGravityTimer <= 0)
				return;

			// Landed: end the effect (skip the first few ticks, when you're still leaving the ground)
			bool onGround = ClawlinePlayer.IsOnGround(Player); // counts platforms of any style
			if (onGround && lowGravityTimer < LowGravityTotalTicks - 5)
			{
				lowGravityTimer = 0;
				return;
			}

			float reduction = lowGravityTimer > LowGravityFadeTicks
				? 1f
				: (float)lowGravityTimer / LowGravityFadeTicks;

			// Gravity was already added to velocity this tick; give part of it back
			Player.velocity.Y -= Player.gravity * Player.gravDir * LowGravityStrength * reduction;
			lowGravityTimer--;
		}

		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer)
				return;

			bool rightPressed = Player.controlUseTile && !rightHeldLastTick;
			rightHeldLastTick = Player.controlUseTile;

			if (rightPressed
				&& !Player.mouseInterface                                   // not clicking on UI
				&& !InteractingWithSomething()
				&& Player.HeldItem.type == ModContent.ItemType<BlastPack>()
				&& BlastPackProjectile.HasActive(Player))
			{
				BlastPackProjectile.DetonateAll(Player);
			}
		}

		// True when this right click is (or would be) used on something in the world instead
		bool InteractingWithSomething()
		{
			return Player.tileInteractionHappened    // opened a door/chest, flipped a switch, etc. this tick
				|| Player.cursorItemIconEnabled      // hovering something interactable (shows its icon)
				|| Main.HoveringOverAnNPC;           // hovering a town NPC you can talk to
		}

		public override void OnRespawn()
		{
			lowGravityTimer = 0;
		}
	}
}
