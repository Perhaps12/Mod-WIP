using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Wprint.Content.Players;

namespace Wprint.Content.Items
{
	// Thrown axe behavior: bounce from NPCs and let its owner catch it by touching it.
	public class catchableaxeProjectile : ModProjectile
	{
		public override void SetDefaults()
        {
            // Set the projectile's size, damage type, hit limit, lifetime, and collision behavior.
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
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
            Projectile.rotation += (0.4f + stacks * 0.05f) * Projectile.direction;

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

            // Gravity pulls the axe down and horizontal drag gradually slows it.
            Projectile.velocity.Y += 0.19f;
            Projectile.velocity.X *= 0.99f;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Every catch stack adds 10% damage to this axe's hits.
            int stacks = Main.player[Projectile.owner].GetModPlayer<CatchableAxePlayer>().Stacks;
            modifiers.SourceDamage *= 1f + stacks * 0.1f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // At maximum stacks, hits also give the target Bleeding.
            if (Main.player[Projectile.owner].GetModPlayer<CatchableAxePlayer>().Stacks >= CatchableAxePlayer.MaxStacks)
            {
                target.AddBuff(BuffID.Bleeding, 300);
            }

            SoundEngine.PlaySound(SoundID.Item174, Projectile.position); 

            // The owner chooses the random bounce; netUpdate shares it with other players.
            if (Main.myPlayer == Projectile.owner)
            {
                // Reverse horizontal travel, but choose a random side if the hit was nearly vertical.
                float incomingX = Projectile.velocity.X;
                float horizontalDirection = Math.Abs(incomingX) < 1f
                    ? (Main.rand.NextBool() ? -1f : 1f)
                    : (incomingX > 0f ? -1f : 1f);
                // Vary horizontal speed from 20% to 70% of the incoming speed.
                float horizontalSpeed = Math.Max(Math.Abs(incomingX), 3f) * Main.rand.NextFloat(0.2f, 0.7f);
                Projectile.velocity.X = horizontalDirection * horizontalSpeed;

                // Randomize the upward speed to vary the bounce height and landing point.
                Projectile.velocity.Y = -Main.rand.NextFloat(4f, 14f);
                Projectile.ai[0] = 1f;

                // Keep the axe catchable, but prevent it from hurting any more NPCs.
                Projectile.friendly = false;
                Projectile.damage = 0;
                Projectile.netUpdate = true;
            }
        }
	}
}
