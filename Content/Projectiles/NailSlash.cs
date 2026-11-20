using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	// The hitbox + visual for a Nail swing. Spawned by Content/Items/Nail.cs.
	// Velocity holds the swing direction, ai[0] = 1 marks a down-slash (pogo),
	// ai[1] picks which half of the Arkhalis sprite sheet to play (0 or 1).
	public class NailSlash : ModProjectile
	{
		const float Reach = 36f;        // distance from player center to slash center
		const int TotalFrames = 28;     // Arkhalis sheet: 28 frames, 68x64 each
		const int FramesPerSwing = 14;  // each swing plays half the sheet
		const int TicksPerFrame = 1;    // vanilla Arkhalis speed

		// Use the vanilla Arkhalis projectile sprite
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Arkhalis;

		public override void SetStaticDefaults()
		{
			Main.projFrames[Type] = TotalFrames;
		}

		public override void SetDefaults()
		{
			Projectile.width = 60;
			Projectile.height = 60;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;              // hit everything in the slash
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = FramesPerSwing * TicksPerFrame; // lives exactly one swing
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;    // each NPC is hit at most once per swing
			Projectile.ownerHitCheck = true;        // can't hit through walls
		}

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			Projectile.frame = (int)Projectile.ai[1] * FramesPerSwing;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];

			// Keep the original direction, but stay attached to the player
			Vector2 dir = Vector2.Normalize(Projectile.velocity);
			Projectile.Center = owner.MountedCenter + dir * Reach;

			// Lock facing during a side swing
			if (dir.X != 0)
				owner.ChangeDir(dir.X > 0 ? 1 : -1);

			// Same rotation/flip logic as vanilla Arkhalis: sprite points right,
			// and is mirrored when the player faces left
			Projectile.direction = Projectile.spriteDirection = owner.direction;
			Projectile.rotation = dir.ToRotation() + (Projectile.spriteDirection == -1 ? MathHelper.Pi : 0f);

			// Advance the animation
			if (++Projectile.frameCounter >= TicksPerFrame)
			{
				Projectile.frameCounter = 0;
				int start = (int)Projectile.ai[1] * FramesPerSwing;
				if (Projectile.frame < start + FramesPerSwing - 1)
					Projectile.frame++;
			}

			owner.heldProj = Projectile.whoAmI;
			Lighting.AddLight(Projectile.Center, 0.3f, 0.5f, 0.6f); // faint glow like Arkhalis
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			int frameHeight = tex.Height / TotalFrames;
			Rectangle frame = new Rectangle(0, frameHeight * Projectile.frame, tex.Width, frameHeight);
			SpriteEffects flip = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame,
				lightColor,           // drawn full-bright, like vanilla's glowing slash
				Projectile.rotation, frame.Size() / 2f, Projectile.scale, flip);
			return false; // we did the drawing ourselves
		}

		// Pogo: bounce upward when a down-slash hits an enemy
		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Projectile.ai[0] == 1)
			{
				Player owner = Main.player[Projectile.owner];
				owner.velocity.Y = -8f;
				owner.fallStart = (int)(owner.position.Y / 16f); // reset fall damage
			}
		}
	}
}
