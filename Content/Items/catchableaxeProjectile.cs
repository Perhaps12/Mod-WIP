using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;

namespace Wprint.Content.Items
{
	public class catchableaxeProjectile : ModProjectile
	{
		public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            Projectile.rotation += 0.3f * (float)Projectile.direction;
            Projectile.velocity.Y += 0.2f; // Gravity effect
            Projectile.velocity.X *= 0.99f; // Air resistance
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 300); 
            SoundEngine.PlaySound(SoundID.Item174, Projectile.position); 
            Projectile.velocity = -Projectile.velocity;
        }
	}
}
