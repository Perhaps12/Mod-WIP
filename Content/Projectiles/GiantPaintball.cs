using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.NPCs;

namespace Wprint.Content.Projectiles
{
	// A giant paintball fired by the Paintball Umbrella (Content/Items/PaintballUmbrella.cs).
	// Looks like the vanilla Cursed Flame (projectile 96) recolored to a random paint color,
	// with a trail of paint dust behind it.
	//   ai[0] = hue (0-1) of the paint color, picked randomly when fired
	//   ai[2] = tile paint color to paint the burst area with (0 = none)
	public class GiantPaintball : ModProjectile
	{
		public const float Scale = 1.3f;          // size multiplier for the sprite and hitbox
		const int BaseSize = 10;                // hitbox size (px) at scale 1
		const int GravityDelayTicks = 20;       // flies straight for this long, then starts to drop
		const float Gravity = 0.15f;
		const float MaxRange = 40 * 16f;        // bursts after travelling this far (45 tiles)
		const float TrailDustScale = 1.6f;      // size of the trail particles
		const int SplatDustCount = 40;          // particles in the burst
		const float SplatSpeed = 7f;            // how far/fast the burst particles fly out
		const float SpriteRotationOffset = MathHelper.PiOver2; // rotate if the flame doesn't point along its flight
		const int BurstSize = 8 * 16;           // px, width/height of the area-damage burst (8 tiles across)
		const float BurstDamageMultiplier = 0.5f; // burst deals this fraction of the paintball's damage
		const int DebuffTicks = 10 * 60;        // 10 seconds
		const int DebuffChanceDenominator = 5; // 1 in 10 paintballs applies its color's debuff

		const int CursedFlame = ProjectileID.CursedFlameHostile; // 96

		// Greyscale copy of the Cursed Flame sprite, made at load so it can be tinted any color cleanly
		// (tinting the original green sprite would give muddy colors)
		static Texture2D greyTexture;

		public override string Texture => "Terraria/Images/Projectile_" + CursedFlame;

		public override void Load()
		{
			if (Main.dedServ)
				return;
			Main.QueueMainThreadAction(() =>
			{
				Texture2D source = ModContent.Request<Texture2D>(Texture, AssetRequestMode.ImmediateLoad).Value;
				Color[] pixels = new Color[source.Width * source.Height];
				source.GetData(pixels);
				for (int i = 0; i < pixels.Length; i++)
				{
					Color c = pixels[i];
					byte v = System.Math.Max(c.R, System.Math.Max(c.G, c.B)); // keep the brightness, drop the hue
					pixels[i] = new Color(v, v, v, c.A);
				}
				greyTexture = new Texture2D(Main.graphics.GraphicsDevice, source.Width, source.Height);
				greyTexture.SetData(pixels);
			});
		}

		public override void Unload()
		{
			Texture2D tex = greyTexture;
			greyTexture = null;
			if (tex != null)
				Main.QueueMainThreadAction(tex.Dispose);
		}

		public override void SetStaticDefaults()
		{
			Main.projFrames[Type] = Main.projFrames[CursedFlame]; // same animation frames as the original
		}

		public override void SetDefaults()
		{
			Projectile.width = (int)(BaseSize * Scale);
			Projectile.height = (int)(BaseSize * Scale);
			Projectile.scale = Scale;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Ranged;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 300;
			Projectile.extraUpdates = 1;          // fast shot: check collision twice per tick so it doesn't skip
			Projectile.usesLocalNPCImmunity = true; // own immunity, so the burst can hit the enemy the paintball just hit
			Projectile.localNPCHitCooldown = -1;
		}

		Color PaintColor => Main.hslToRgb(Projectile.ai[0], 1f, 0.6f);

		// Debuff picked from the paint color's hue (0 = red, 0.33 = green, 0.67 = blue)
		int PaintDebuff
		{
			get
			{
				float hue = Projectile.ai[0];
				if (hue < 0.05f || hue >= 0.95f) return BuffID.OnFire3;       // red: Hellfire
				if (hue < 0.11f) return BuffID.Oiled;                          // orange: Oiled
				if (hue < 0.20f) return BuffID.Ichor;                          // yellow: Ichor
				if (hue < 0.45f) return BuffID.CursedInferno;                  // green: Cursed Inferno
				if (hue < 0.55f) return BuffID.Frostburn2;                     // teal/cyan: Frostbite
				if (hue < 0.72f) return BuffID.Slimed;                         // blue: Slimed
				if (hue < 0.85f) return BuffID.Venom;                          // purple: Acid Venom
				return BuffID.ShadowFlame;                                     // pink: Shadowflame
			}
		}

		// Each paintball rolls once (on its first hit) whether it applies its debuff.
		// If it does, both its direct hit and its burst apply it.
		bool debuffRolled;
		bool debuffActive;

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!debuffRolled)
			{
				debuffRolled = true;
				debuffActive = Main.rand.NextBool(DebuffChanceDenominator);
			}
			if (debuffActive)
				target.AddBuff(PaintDebuff, DebuffTicks);

			// Every hit (direct or burst) paints the enemy for a few seconds
			PaintedGlobalNPC.Paint(target, PaintColor);
		}

		// Collide with tiles using a small box, so the giant paintball doesn't pop on walls it only grazes
		public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac)
		{
			width = height = BaseSize;
			return true;
		}

		public override void AI()
		{
			// Burst after travelling MaxRange (OnKill plays the splat)
			Projectile.localAI[0] += Projectile.velocity.Length();
			if (Projectile.localAI[0] >= MaxRange)
			{
				Projectile.Kill();
				return;
			}

			Projectile.ai[1]++;
			if (Projectile.ai[1] > GravityDelayTicks * Projectile.MaxUpdates)
				Projectile.velocity.Y = MathHelper.Min(Projectile.velocity.Y + Gravity / Projectile.MaxUpdates, 16f);

			Projectile.rotation = Projectile.velocity.ToRotation() + SpriteRotationOffset;
			Lighting.AddLight(Projectile.Center, PaintColor.ToVector3() * 0.3f);

			// Paint trail
			if (Main.rand.NextBool(1))
			{
				Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(6f), 12, 12,
					DustID.RainbowMk2, 0f, 0f, 0, PaintColor, TrailDustScale);
				d.velocity = Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f);
				d.noGravity = true;
			}

			// Animate through the Cursed Flame frames
			if (Main.projFrames[Type] > 1 && ++Projectile.frameCounter >= 4 * Projectile.MaxUpdates)
			{
				Projectile.frameCounter = 0;
				Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
			}
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);

			// Grow into the burst area (Resize keeps the center in place)
			Projectile.Resize(BurstSize, BurstSize);

			// Area damage, on top of the direct hit (done by the owner, like vanilla explosives)
			if (Projectile.owner == Main.myPlayer)
			{
				Projectile.damage = (int)(Projectile.damage * BurstDamageMultiplier);
				Projectile.knockBack *= BurstDamageMultiplier;
				Projectile.penetrate = -1;                       // hit everything in the burst
				for (int i = 0; i < Projectile.localNPCImmunity.Length; i++)
					Projectile.localNPCImmunity[i] = 0;          // let the directly-hit enemy take the burst too
				Projectile.Damage();
				PaintTiles();
			}

			// Paint splat, spread over the burst area
			for (int i = 0; i < SplatDustCount; i++)
			{
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.RainbowMk2, 0f, 0f, 0, PaintColor, Main.rand.NextFloat(1.5f, 2.3f));
				d.velocity = Main.rand.NextVector2Circular(SplatSpeed, SplatSpeed);
				d.noGravity = true;
			}
		}

		// Paint every tile within the burst circle with the paintball's paint color (ai[2]).
		// Runs on the owner; broadcast sends the change to the server/other players.
		void PaintTiles()
		{
			byte color = (byte)Projectile.ai[2];
			if (color == 0)
				return;

			Point center = Projectile.Center.ToTileCoordinates();
			int radius = BurstSize / 2 / 16;
			for (int x = center.X - radius; x <= center.X + radius; x++)
			{
				for (int y = center.Y - radius; y <= center.Y + radius; y++)
				{
					if (!WorldGen.InWorld(x, y) || (x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y) > radius * radius)
						continue;
					Tile tile = Main.tile[x, y];
					if (tile.HasTile && tile.TileColor != color)
						WorldGen.paintTile(x, y, color, broadCast: true);
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (greyTexture == null)
				return false; // still being created on the first frames after load

			int frames = Main.projFrames[Type];
			Rectangle frame = greyTexture.Frame(1, frames, 0, Projectile.frame);
			Color color = PaintColor;
			color.A = 200; // slightly see-through, like a flame
			Main.EntitySpriteDraw(greyTexture, Projectile.Center - Main.screenPosition, frame, color,
				Projectile.rotation, frame.Size() / 2f, Projectile.scale, SpriteEffects.None);
			return false;
		}
	}
}
