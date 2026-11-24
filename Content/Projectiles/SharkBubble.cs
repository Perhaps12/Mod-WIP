using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	// The bubble fired by the Shark Staff's shark (SharkMinion). A copy of the vanilla Bubble Gun bubble
	// (projectile 410), but its own type so it can be flagged as a minion shot, which lets whip tag
	// damage apply to it. (Flagging the vanilla bubble would also change the real Bubble Gun.)
	public class SharkBubble : ModProjectile
	{
		const int VanillaBubble = ProjectileID.Bubble; // 410

		public override string Texture => "Terraria/Images/Projectile_" + VanillaBubble;

		public override void SetStaticDefaults()
		{
			Main.projFrames[Type] = Main.projFrames[VanillaBubble];
			ProjectileID.Sets.MinionShot[Type] = true; // counts as a minion attack: whip tags and summon effects apply
			ProjectileID.Sets.SummonTagDamageMultiplier[Type] = 0.5f; // only gets 50% of whip tag damage (it hits very often)
		}

		public override void SetDefaults()
		{
			Projectile.CloneDefaults(VanillaBubble); // same size, behavior and look as the Bubble Gun's bubble
			AIType = VanillaBubble;                  // run the vanilla bubble's movement
			Projectile.DamageType = DamageClass.Summon;
		}

		// Pop like a vanilla bubble (only the movement is borrowed from vanilla, not the death effect)
		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item54, Projectile.Center); // bubble pop
			for (int i = 0; i < 5; i++)
			{
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.BubbleBurst_Blue, 0f, 0f, 50);
				d.noGravity = true;
				d.velocity *= 0.5f;
			}
		}
	}
}
