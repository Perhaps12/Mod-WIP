using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace Wprint.Content.Projectiles
{
	// ---- Attack animations ----
	// When a villager attacks, it plays the attack frames from its own Town NPC sprite sheet (the last
	// NPCID.Sets.AttackFrameCount frames), in the style the game uses for that NPC (NPCID.Sets.AttackType):
	//   Throw (0): the throwing frames play through once     (Merchant, Demolitionist, Party Girl)
	//   Shoot (1): holds the frame for the aim angle, with the weapon drawn in hand (Guide, Arms Dealer, Steampunker)
	//   Magic (2): the casting frames play through, with a glow at the hand (Wizard, Clothier, Princess)
	// Melee villagers attack by touch and the Dryad doesn't attack, so they don't use this.
	// The animation is started where the attack is fired, which is the player's own game, so in multiplayer
	// other players see the villagers attack without the animation.
	public partial class TownVillagerMinion
	{
		private const int ThrowAnimTicks = 18;  // how long each kind of attack animation lasts
		private const int ShootAnimTicks = 16;
		private const int MagicAnimTicks = 24;
		private const float HandOffsetX = 10f;  // where the hand is, from the villager's center (px; X is toward the facing side)
		private const float HandOffsetY = 2f;
		private const float WeaponReach = 8f;   // the weapon is held out this far from the hand, along the aim (px)
		private const float GlowScale = 0.35f;  // size of the magic glow

		private int attackAnimTicks;   // ticks left in the current attack animation (0 = not attacking)
		private int attackAnimLength;  // its total length
		private Vector2 attackAim = Vector2.UnitX; // direction of the last attack (for shooting frames and the weapon)

		private int AttackType => NPCID.Sets.AttackType[VillagerType];
		private int AttackFrameCount => NPCID.Sets.AttackFrameCount[VillagerType];

		// The weapon drawn in hand while attacking (0 = none)
		private static int HeldWeapon(int npcType) => npcType switch
		{
			NPCID.Guide => ItemID.WoodenBow,
			NPCID.ArmsDealer => ItemID.Minishark,
			NPCID.Steampunker => ItemID.ClockworkAssaultRifle,
			NPCID.Princess => ResonanceScepterItem,
			_ => 0,
		};

		// Staffs are drawn diagonally (like held staffs); everything else points along the aim like a gun/bow
		private static bool IsStaff(int itemType) => itemType == ResonanceScepterItem;

		// Color of the magic glow
		private static Color GlowColor(int npcType) => npcType switch
		{
			NPCID.Wizard => new Color(130, 90, 255),
			NPCID.Clothier => new Color(255, 60, 90),
			NPCID.Princess => new Color(255, 120, 220),
			_ => new Color(255, 255, 255),
		};

		// Called when the villager fires (FireAt)
		private void StartAttackAnimation(Vector2 aim)
		{
			if (AttackFrameCount <= 0)
				return;
			attackAim = aim.SafeNormalize(Vector2.UnitX * Projectile.spriteDirection);
			attackAnimLength = AttackType switch
			{
				0 => ThrowAnimTicks,
				1 => ShootAnimTicks,
				_ => MagicAnimTicks,
			};
			attackAnimTicks = attackAnimLength;
		}

		// The sprite sheet frame to show while attacking, or -1 if not attacking. Counts the animation down.
		private int AttackAnimationFrame(int frameCount)
		{
			if (attackAnimTicks <= 0)
				return -1;
			attackAnimTicks--;

			int count = AttackFrameCount;
			int first = frameCount - count; // attack frames are the last ones on the sheet
			if (count <= 0 || first < 0)
				return -1;

			if (AttackType == 1)
			{
				// Shooting: pick the frame for the aim's angle, from straight up (first) to straight down (last)
				float angle = (float)System.Math.Atan2(attackAim.Y, System.Math.Abs(attackAim.X)); // -pi/2 up .. pi/2 down
				float t = (angle + MathHelper.PiOver2) / MathHelper.Pi;
				return first + System.Math.Clamp((int)(t * count), 0, count - 1);
			}

			// Throwing / casting: play the frames through once over the animation
			float progress = 1f - attackAnimTicks / (float)System.Math.Max(1, attackAnimLength);
			return first + System.Math.Clamp((int)(progress * count), 0, count - 1);
		}

		private Vector2 HandPosition() =>
			Projectile.Center + new Vector2(Projectile.spriteDirection * HandOffsetX, HandOffsetY + Projectile.gfxOffY);

		// Drawn in front of the villager while attacking: the weapon in hand and/or the magic glow
		private void DrawAttackExtras(Color lightColor)
		{
			if (attackAnimTicks <= 0)
				return;

			Vector2 hand = HandPosition() - Main.screenPosition;
			int dir = Projectile.spriteDirection;

			int weapon = HeldWeapon(VillagerType);
			if (weapon > 0)
			{
				Main.instance.LoadItem(weapon);
				Texture2D itemTexture = TextureAssets.Item[weapon].Value;
				Rectangle source = Main.itemAnimations[weapon] != null
					? Main.itemAnimations[weapon].GetFrame(itemTexture)
					: itemTexture.Frame();
				float aimAngle = attackAim.ToRotation();
				SpriteEffects effects = dir > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
				float rotation;
				Vector2 origin;
				if (IsStaff(weapon))
				{
					// Staff sprites point up and to the right: line the tip up with the aim, held near the bottom
					rotation = dir > 0 ? aimAngle + MathHelper.PiOver4 : aimAngle + 3f * MathHelper.PiOver4;
					origin = new Vector2(dir > 0 ? source.Width * 0.15f : source.Width * 0.85f, source.Height * 0.85f);
				}
				else
				{
					// Guns and bows point to the right: rotate along the aim, held near the back
					rotation = dir > 0 ? aimAngle : aimAngle - MathHelper.Pi;
					origin = new Vector2(dir > 0 ? source.Width * 0.3f : source.Width * 0.7f, source.Height * 0.5f);
				}
				Vector2 weaponSpot = hand + attackAim * WeaponReach; // held out in front, not inside the body
				Main.EntitySpriteDraw(itemTexture, weaponSpot, source, lightColor, rotation, origin, Projectile.scale, effects);
			}

			if (AttackType == 2)
			{
				// Magic: a pulsing glow at the hand, strongest in the middle of the cast
				float progress = 1f - attackAnimTicks / (float)System.Math.Max(1, attackAnimLength);
				float strength = (float)System.Math.Sin(progress * MathHelper.Pi);
				Color glow = GlowColor(VillagerType) * strength;
				glow.A = 0; // additive
				Texture2D glowSprite = MagicGlowTexture.Value;
				Vector2 glowSpot = hand + new Vector2(dir * 6f, -6f);
				Main.EntitySpriteDraw(glowSprite, glowSpot, null, glow, 0f, glowSprite.Size() / 2f,
					GlowScale * (0.8f + 0.2f * strength), SpriteEffects.None);
				Main.EntitySpriteDraw(glowSprite, glowSpot, null, glow * 0.6f, MathHelper.PiOver2, glowSprite.Size() / 2f,
					GlowScale * 0.6f, SpriteEffects.None);
				Lighting.AddLight(glowSpot + Main.screenPosition, GlowColor(VillagerType).ToVector3() * 0.5f * strength);
			}
		}

		// Soft glow (vanilla's Extra_98 sparkle texture), loaded right away if the game hasn't yet
		private static Asset<Texture2D> magicGlowTexture;
		private static Asset<Texture2D> MagicGlowTexture
		{
			get
			{
				if (magicGlowTexture == null)
				{
					magicGlowTexture = TextureAssets.Extra[ExtrasID.SharpTears]; // Extra_98
					if (!magicGlowTexture.IsLoaded)
						magicGlowTexture = Main.Assets.Request<Texture2D>(magicGlowTexture.Name, AssetRequestMode.ImmediateLoad);
				}
				return magicGlowTexture;
			}
		}
	}
}
