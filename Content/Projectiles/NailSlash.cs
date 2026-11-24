using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	// A Nail swing, spawned by Content/Items/Nail.cs. Uses the same rotation math as vanilla's
	// Excalibur slash (projectile 982), draws that slash sprite in grey with the Nail swinging
	// along its leading edge, and hits whatever is inside the slash cone.
	//   velocity = aim direction rotated 90 degrees by the sweep (see Nail.Shoot)
	//   ai[0]    = 1 for a down-slash
	//   ai[1]    = sweep direction (+1 clockwise, -1 counter-clockwise)
	//   ai[2]    = item scale
	public class NailSlash : ModProjectile
	{
		public const int SwingTicks = 10;      // swing duration in ticks (lower = faster)
		const int ExtraUpdates = 1;            // extra AI/hit checks per tick (doesn't change swing speed)
		const int SwingUpdates = SwingTicks * (ExtraUpdates + 1); // swing duration in updates
		const float ConeLength = 8*16f;         // hit reach at scale 1 (this, not width/height, sets the hitbox size)
		const float ConeHalfAngle = MathHelper.PiOver4; // vanilla slash covers +/-45 degrees
		const float SpriteReach = 110f / 1.4f; // px the slash sprite reaches when drawn at scale 1 (110 px looked right at 1.4x)
		const float SlashScale = ConeLength / SpriteReach; // drawn size follows ConeLength, so the sprite always matches the range

		// The vanilla Excalibur slash sprite, recolored grey in PreDraw
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Excalibur;

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;              // hit everything in the slash
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.extraUpdates = ExtraUpdates;  // checks hits more often per tick, so the fast swing doesn't skip enemies
			Projectile.timeLeft = SwingUpdates;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;    // each NPC is hit at most once per swing
			Projectile.ownerHitCheck = true;        // can't hit through walls
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			Projectile.Center = owner.MountedCenter;

			// Same formula vanilla uses for the Excalibur slash rotation
			float sweep = Projectile.ai[1];
			Projectile.localAI[0]++;
			float progress = Projectile.localAI[0] / SwingUpdates;
			Projectile.rotation = MathHelper.Pi * sweep * progress + Projectile.velocity.ToRotation()
				+ sweep * MathHelper.Pi + owner.fullRotation;

			// Lock facing during a side swing (the aim is velocity rotated back by 90 degrees)
			Vector2 aim = Projectile.velocity.RotatedBy(-sweep * MathHelper.PiOver2);
			if (Math.Abs(aim.X) > 0.5f)
				owner.ChangeDir(aim.X > 0 ? 1 : -1);
		}

		// Hit anything inside the slash's current 90-degree cone
		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			float length = ConeLength * Projectile.ai[2];
			return targetHitbox.IntersectsConeSlowMoreAccurate(Projectile.Center, length, Projectile.rotation, ConeHalfAngle);
		}

		// Angle the sword blade points at: the leading edge of the slash cone
		float SwordAngle => Projectile.rotation + Projectile.ai[1] * ConeHalfAngle;

		public override void PostAI()
		{
			// Draw over the player, and have the front arm hold the sword
			Player owner = Main.player[Projectile.owner];
			owner.heldProj = Projectile.whoAmI;
			owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, SwordAngle - MathHelper.PiOver2);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			float progress = Projectile.localAI[0] / SwingUpdates;
			Vector2 center = Projectile.Center - Main.screenPosition;
			float scale = Projectile.ai[2] * SlashScale;

			// --- Grey slash, drawn from the vanilla Excalibur slash sprite (4 vertical frames) ---
			Texture2D slash = TextureAssets.Projectile[Type].Value;
			Rectangle body = slash.Frame(1, 4, 0, 0);       // main arc
			Rectangle edge = slash.Frame(1, 4, 0, 3);       // bright leading edge
			Vector2 origin = body.Size() / 2f;
			SpriteEffects flip = Projectile.ai[1] >= 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
			float fade = Utils.Remap(progress, 0f, 0.6f, 0f, 1f) * Utils.Remap(progress, 0.6f, 1f, 1f, 0f);
			float sweep = Projectile.ai[1];

			Color dark = new Color(90, 90, 100);
			Color mid = new Color(160, 160, 170);
			Color light = new Color(220, 220, 230, 0);  // A = 0 draws it additively (glow)

			Main.EntitySpriteDraw(slash, center, body, dark * fade * 0.8f,
				Projectile.rotation - sweep * MathHelper.PiOver4 * (1f - progress), origin, scale, flip);
			Main.EntitySpriteDraw(slash, center, body, mid * fade * 0.6f, Projectile.rotation, origin, scale, flip);
			Main.EntitySpriteDraw(slash, center, body, light * fade * 0.4f, Projectile.rotation, origin, scale * 0.975f, flip);
			Main.EntitySpriteDraw(slash, center, edge, light * fade * 0.6f, Projectile.rotation + sweep * 0.01f, origin, scale, flip);
			Main.EntitySpriteDraw(slash, center, edge, light * fade * 0.4f, Projectile.rotation - sweep * 0.05f, origin, scale * 0.8f, flip);

			// --- The Nail itself, swinging with the slash ---
			// Item sprites point up-right, so the handle (origin) is the bottom-left corner
			Texture2D sword = TextureAssets.Item[ModContent.ItemType<Items.Nail>()].Value;
			Main.EntitySpriteDraw(sword, center, null, lightColor, SwordAngle + MathHelper.PiOver4,
				new Vector2(0f, sword.Height), Projectile.ai[2], SpriteEffects.None);

			return false; // we did the drawing ourselves
		}

		bool bounced; // pushback applies once per swing, even if it hits several enemies

		// Knockback is applied by hand in OnHitNPC, so turn off vanilla's for this hit. Vanilla's was
		// unreliable here (often only 1-2 enemies in a group got pushed), and it picks the side from the
		// projectile's velocity, which is rotated 90 degrees from the aim (see Nail.Shoot).
		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			modifiers.DisableKnockback();
			modifiers.HitDirectionOverride = KnockbackSide(target); // hit effects still face away from the player
		}

		// Which side of the player the enemy is on (+1 right, -1 left)
		int KnockbackSide(NPC target)
		{
			Player owner = Main.player[Projectile.owner];
			float side = target.Center.X - owner.Center.X;
			return side != 0f ? System.Math.Sign(side) : owner.direction;
		}

		// Push every enemy the slash hits away from the player, scaled by its knockback resistance
		// (bosses and other immovable enemies have 0, so they don't move)
		void ApplyKnockback(NPC target)
		{
			float strength = Projectile.knockBack * target.knockBackResist;
			if (strength <= 0f)
			{
				return;
			}

			target.velocity.X = KnockbackSide(target) * strength;
			if (!target.noGravity)
			{
				target.velocity.Y = -strength * 0.3f; // small hop, like vanilla knockback
			}
			target.netUpdate = true; // sync the new velocity in multiplayer
		}

		// Down-slash: pogo bounce (sets the player's velocity).
		// Side swing: small pushback away from the enemy, added on top of the player's velocity.
		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ApplyKnockback(target); // every enemy hit, before the once-per-swing player pushback below

			if (bounced)
			{
				return;
			}

			Player owner = Main.player[Projectile.owner];
			Vector2 attackDirection = Projectile.velocity.RotatedBy(-Projectile.ai[1] * MathHelper.PiOver2);
			if (attackDirection.Y < -0.5f)
			{
				return; // up-slash: no pushback
			}
			bounced = true;

			if (attackDirection.Y > 0.5f)
			{
				owner.velocity.Y = -8f;                           // down-slash pogo: bounce up, keep horizontal momentum
			}
			else
			{
				owner.velocity += -attackDirection * 2f;          // side swing: keeps existing momentum
			}
			owner.fallStart = (int)(owner.position.Y / 16f); // reset fall damage
		}
	}
}
