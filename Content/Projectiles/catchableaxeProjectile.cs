using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Wprint.Content.Players;

namespace Wprint.Content.Projectiles
{
	// Thrown axe behavior: bounce from NPCs and let its owner catch it by touching it.
	public class catchableaxeProjectile : ModProjectile
	{
		private const float NormalGravity = 0.19f;
		private const float BouncedFallGravity = 0.7f;
		private const float HorizontalDrag = 0.99f;
		private const float BounceVerticalSpeed = 8f;
		private const float MinimumBounceVerticalSpeed = 2f;
		private const float MovementPredictionAccuracy = 0.5f;
		private const float PlayerMomentumBoost = 0.65f;
		private const float LandingErrorRange = 140f;
		private static readonly int BounceFlightTicks = (int)Math.Ceiling(
			BounceVerticalSpeed / NormalGravity
			+ BounceVerticalSpeed / Math.Sqrt(NormalGravity * BouncedFallGravity));
		private static readonly float HorizontalTravelFactor = HorizontalDrag
			* (1f - (float)Math.Pow(HorizontalDrag, BounceFlightTicks))
			/ (1f - HorizontalDrag);

		public override void SetDefaults()
        {
            // Set the projectile's size, damage type, hit limit, lifetime, and collision behavior.
            Projectile.width = 75;
            Projectile.height = 75;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void OnSpawn(IEntitySource source)
        {
            if (Projectile.owner == Main.myPlayer)
            {
                Projectile.ai[1] = Main.rand.Next(2) == 0 ? -1f : 1f;
                Projectile.netUpdate = true;
            }
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return Projectile.ai[0] == 1f
                ? Color.Lerp(lightColor, Color.Gold, 0.65f)
                : null;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            int stacks = owner.GetModPlayer<CatchableAxePlayer>().Stacks;

            // Only the bounced axe glows; the values are red, green, and blue light intensity.
            if (Projectile.ai[0] == 1f && Main.netMode != NetmodeID.Server)
            {
                Lighting.AddLight(Projectile.Center, 0.5f, 0.325f, 0.05f);
            }

            // Increase the gold dust trail chance from 5% at zero stacks to 80% at ten stacks.
            float goldDustChance = 0.05f + stacks * 0.075f;
            if (Main.netMode != NetmodeID.Server && Main.rand.NextFloat() < goldDustChance)
            {
                int dustIndex = Dust.NewDust(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Gold,
                    -Projectile.velocity.X * 0.15f,
                    -Projectile.velocity.Y * 0.15f,
                    100,
                    default,
                    1f);
                Main.dust[dustIndex].noGravity = true;
            }

            // Each stack makes the axe spin faster while it is moving.
            Projectile.rotation += (0.5f + stacks * 0.06f) * Projectile.direction * Projectile.ai[1];

            // ai[0] is set after an NPC hit, so the axe can only be caught after bouncing.
            if (Projectile.ai[0] == 1f && owner.active && !owner.dead && owner.Hitbox.Intersects(Projectile.Hitbox))
            {
                // Only the owning client awards the catch, preventing multiplayer duplicates.
                if (Main.myPlayer == Projectile.owner)
                {
                    owner.GetModPlayer<CatchableAxePlayer>().CatchAxe();
                }

                Projectile.Kill();
                return;
            }

            // Increase gravity only while a bounced axe is descending, keeping its upward arc unchanged.
            float gravity = Projectile.ai[0] == 1f && Projectile.velocity.Y >= 0f
                ? BouncedFallGravity
                : NormalGravity;
            Projectile.velocity.Y += gravity;
            Projectile.velocity.X *= HorizontalDrag;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Every catch stack adds 10% damage to this axe's hits.
            int stacks = Main.player[Projectile.owner].GetModPlayer<CatchableAxePlayer>().Stacks;
            modifiers.SourceDamage *= 1f + stacks * 0.1f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // At maximum stacks, hits also give the target OnFire.
            if (Main.player[Projectile.owner].GetModPlayer<CatchableAxePlayer>().Stacks >= CatchableAxePlayer.MaxStacks)
            {
                target.AddBuff(BuffID.OnFire, 300);
            }

            //SoundEngine.PlaySound(SoundID.Item174, Projectile.position);

            // The owner underpredicts movement and adds a small landing error to make catches less automatic.
            if (Main.myPlayer == Projectile.owner)
            {
                Player owner = Main.player[Projectile.owner];
                float predictedPlayerX = owner.Center.X
                    + owner.velocity.X * BounceFlightTicks * MovementPredictionAccuracy
                    + Main.rand.NextFloat(-LandingErrorRange, LandingErrorRange);
                float predictedPlayerY = owner.Center.Y
                    + owner.velocity.Y * BounceFlightTicks * MovementPredictionAccuracy
                    + Main.rand.NextFloat(-LandingErrorRange, LandingErrorRange);
                Projectile.velocity.X = (predictedPlayerX - Projectile.Center.X) / HorizontalTravelFactor
                    + owner.velocity.X * PlayerMomentumBoost;
                Projectile.velocity.Y = GetBounceVerticalVelocity(predictedPlayerY - Projectile.Center.Y);
                Projectile.ai[0] = 1f;

                // Keep the axe catchable, but prevent it from hurting any more NPCs.
                Projectile.friendly = false;
                Projectile.damage = 0;
                Projectile.netUpdate = true;
            }
        }

        private static float GetBounceVerticalVelocity(float targetDisplacement)
        {
            float low = MinimumBounceVerticalSpeed;
            float high = NormalGravity * BounceFlightTicks * 0.98f;

            for (int i = 0; i < 24; i++)
            {
                float upwardSpeed = (low + high) * 0.5f;
                float ascentTicks = upwardSpeed / NormalGravity;
                float descentTicks = BounceFlightTicks - ascentTicks;
                float verticalDisplacement =
                    -0.5f * upwardSpeed * ascentTicks
                    + 0.5f * BouncedFallGravity * descentTicks * descentTicks;

                if (verticalDisplacement > targetDisplacement)
                {
                    low = upwardSpeed;
                }
                else
                {
                    high = upwardSpeed;
                }
            }

            return -(low + high) * 0.5f;
        }
	}
}
