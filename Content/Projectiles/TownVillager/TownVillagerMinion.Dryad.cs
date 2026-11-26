using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace Wprint.Content.Projectiles
{
	// ---- Dryad's Ward aura (like the vanilla Dryad's) ----
	// The Dryad doesn't attack. Instead, a circle around her: players inside get Dryad's Blessing
	// (the Dryad's Ward buff), enemies inside get Dryad's Bane. Refreshed every AttackIntervals tick count.
	// Shown as glowing green leaves orbiting the edge of the circle.
	public partial class TownVillagerMinion
	{
		private const float DryadAuraRadius = 25 * 16f;
		private const int DryadAuraBuffTicks = 120;      // buffs last this long, so they wear off soon after leaving
		private const float DryadRingOpacity = 0.8f;     // how strongly the leaves glow
		private const float DryadRingSpinSpeed = 0.70f;     // how fast the outer ring's leaves orbit (radians per second)
		private const int DryadLeafCount = 12;           // leaves around the outer ring (the edge of the aura)
		private const float DryadInnerRingRadius = 9 * 16f; // inner ring (visual only; the aura covers the full radius)
		private const int DryadInnerLeafCount = 6;       // leaves around the inner ring
		private const float DryadInnerRingSpinSpeed = -0.60f; // inner ring orbits the other way (negative = counter-clockwise)
		private const float DryadLeafScale = 1f;         // size of each leaf
		private const bool DryadSparkles = false;        // green sparkle particles along the outer ring (off for now)
		private const float DryadLeafRadiusVariation = 0.025f; // each leaf drifts in/out from its ring by up to this fraction (0.1 = 10%)
		private const float DryadLeafWobbleSpeed = 1f; // how fast the leaves drift in and out
		private const int DryadLeafFrames = 5;           // animation frames in the vanilla Dryad's Ward leaf sprite (stacked vertically)
		private const float DryadLeafFramesPerSecond = 10f; // leaf animation speed
		private static readonly Color DryadLeafColor = new Color(120, 255, 120); // green glow

		private void UpdateDryadAura()
		{
			// Green light from the orbiting leaves, plus green sparkles along the ring
			if (Main.netMode != NetmodeID.Server)
			{
				LightDryadRing();
				for (int i = 0; DryadSparkles && i < 2; i++)
				{
					Vector2 offset = Main.rand.NextVector2CircularEdge(DryadAuraRadius, DryadAuraRadius);
					Dust ring = Dust.NewDustPerfect(Projectile.Center + offset, DustID.GreenTorch, Vector2.Zero, 100, default, 1.2f);
					ring.noGravity = true;
				}
			}

			if (AttackTimer < AttackIntervals[VillagerIndex])
				return;
			AttackTimer = 0f;

			// Each game buffs its own player (so it works for every player in multiplayer)
			Player local = Main.LocalPlayer;
			if (local.active && !local.dead && Vector2.Distance(local.Center, Projectile.Center) <= DryadAuraRadius)
				local.AddBuff(BuffID.DryadsWard, DryadAuraBuffTicks);

			// The owner applies Dryad's Bane to enemies (AddBuff syncs it to everyone)
			if (Main.myPlayer != Projectile.owner)
				return;
			foreach (NPC npc in Main.ActiveNPCs)
			{
				if (npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
					continue;
				if (Vector2.Distance(npc.Center, Projectile.Center) <= DryadAuraRadius + npc.width / 2f)
					npc.AddBuff(BuffID.DryadsWardDebuff, DryadAuraBuffTicks);
			}
		}

		// Draws the Dryad's ward like the vanilla one: the Dryad's Ward leaf sprite repeated around two rings
		// (the outer edge of the aura and a smaller inner ring), orbiting and glowing green.
		// (The vanilla sprite is a single leaf, not a whole ring.)
		private void DrawDryadRing()
		{
			DrawLeafRing(DryadAuraRadius, DryadLeafCount, DryadRingSpinSpeed);
			DrawLeafRing(DryadInnerRingRadius, DryadInnerLeafCount, DryadInnerRingSpinSpeed);
		}

		private void DrawLeafRing(float radius, int leafCount, float spinSpeed)
		{
			Main.instance.LoadProjectile(ProjectileID.DryadsWardCircle);
			Texture2D leaf = TextureAssets.Projectile[ProjectileID.DryadsWardCircle].Value;

			Color glow = DryadLeafColor * DryadRingOpacity;
			glow.A = 0; // additive, so the leaves glow
			float spin = Main.GlobalTimeWrappedHourly * spinSpeed;
			int animationStep = (int)(Main.GlobalTimeWrappedHourly * DryadLeafFramesPerSecond);

			for (int i = 0; i < leafCount; i++)
			{
				// The sprite is a vertical sheet of DryadLeafFrames frames. Each leaf starts at a different
				// frame so they don't all animate in sync.
				Rectangle frame = leaf.Frame(1, DryadLeafFrames, 0, (animationStep + i) % DryadLeafFrames);

				float angle = spin + MathHelper.TwoPi * i / leafCount;
				Vector2 position = Projectile.Center + angle.ToRotationVector2() * LeafRadius(radius, i);
				// Each leaf points along the circle, in the direction it's orbiting
				float facing = angle + (spinSpeed >= 0f ? MathHelper.PiOver2 : -MathHelper.PiOver2);
				Main.EntitySpriteDraw(leaf, position - Main.screenPosition, frame, glow,
					facing, frame.Size() / 2f, DryadLeafScale, SpriteEffects.None);
			}
		}

		// Green light along both rings, so the leaves light up their surroundings
		private void LightDryadRing()
		{
			LightLeafRing(DryadAuraRadius, DryadLeafCount, DryadRingSpinSpeed);
			LightLeafRing(DryadInnerRingRadius, DryadInnerLeafCount, DryadInnerRingSpinSpeed);
		}

		private void LightLeafRing(float radius, int leafCount, float spinSpeed)
		{
			float spin = Main.GlobalTimeWrappedHourly * spinSpeed;
			for (int i = 0; i < leafCount; i += 2)
			{
				float angle = spin + MathHelper.TwoPi * i / leafCount;
				Lighting.AddLight(Projectile.Center + angle.ToRotationVector2() * LeafRadius(radius, i), 0.1f, 0.35f, 0.1f);
			}
		}

		// Each leaf drifts in and out from its ring by up to DryadLeafRadiusVariation, on a slow wave.
		// Every leaf is at a different point in the wave (spread by the golden angle), so the ring wobbles
		// organically instead of pulsing all at once.
		private static float LeafRadius(float radius, int leafIndex)
		{
			float wave = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * DryadLeafWobbleSpeed + leafIndex * 2.39996f);
			return radius * (1f + DryadLeafRadiusVariation * wave);
		}
	}
}
