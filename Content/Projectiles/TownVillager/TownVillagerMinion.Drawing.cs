using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace Wprint.Content.Projectiles
{
	// ---- Animation and drawing ----
	public partial class TownVillagerMinion
	{
		private void Animate(Player owner, NPC target)
		{
			// Face the enemy while attacking; otherwise the way it's walking, or the player's way when standing
			if (target != null)
				Projectile.spriteDirection = target.Center.X >= Projectile.Center.X ? 1 : -1;
			else if (System.Math.Abs(Projectile.velocity.X) > 0.5f)
				Projectile.spriteDirection = Projectile.velocity.X > 0f ? 1 : -1;
			else
				Projectile.spriteDirection = owner.direction;

			// Town NPC sheets: frame 0 = standing, frame 1 = in the air, then the walk cycle,
			// then attack/sitting frames at the end (ExtraFramesCount)
			int frameCount = System.Math.Max(1, Main.npcFrameCount[VillagerType]);
			int walkFrames = frameCount - NPCID.Sets.ExtraFramesCount[VillagerType] - 2;

			if (frameCount <= 2 || walkFrames < 1)
			{
				Projectile.frame = 0;
			}
			else if (Flying || !onGround)
			{
				Projectile.frame = 1; // airborne
			}
			else if (System.Math.Abs(Projectile.velocity.X) > 0.3f)
			{
				// Walk cycle, faster when moving faster
				Projectile.frameCounter += (int)(System.Math.Abs(Projectile.velocity.X) * 1.5f) + 1;
				if (Projectile.frameCounter >= 12)
				{
					Projectile.frameCounter = 0;
					int walkFrame = Projectile.frame - 2;
					Projectile.frame = 2 + (walkFrame < 0 ? 0 : (walkFrame + 1) % walkFrames);
				}
			}
			else
			{
				Projectile.frame = 0; // standing
				Projectile.frameCounter = 0;
			}
		}

		// The sprite each villager is drawn with in-game today. Since 1.4.4 the game draws Town NPCs from
		// their "profile" texture (TownNPCProfiles), and the old NPC_<id> image is outdated for some of them
		// (e.g. the Princess). Falls back to NPC_<id> for NPCs without a profile. Cached per NPC type.
		private static readonly Dictionary<int, Asset<Texture2D>> villagerTextures = new();

		private static Asset<Texture2D> GetVillagerTexture(int npcType)
		{
			if (villagerTextures.TryGetValue(npcType, out Asset<Texture2D> cached))
				return cached;

			NPC dummy = new NPC();
			dummy.SetDefaults(npcType);
			Asset<Texture2D> texture;
			if (TownNPCProfiles.Instance.GetProfile(dummy, out ITownNPCProfile profile))
			{
				texture = profile.GetTextureNPCShouldUse(dummy);
			}
			else
			{
				Main.instance.LoadNPC(npcType);
				texture = TextureAssets.Npc[npcType];
			}

			// Vanilla requests profile textures without loading them, so make sure it's actually loaded;
			// otherwise it draws as an empty (invisible) texture
			if (!texture.IsLoaded)
				texture = Main.Assets.Request<Texture2D>(texture.Name, AssetRequestMode.ImmediateLoad);

			villagerTextures[npcType] = texture;
			return texture;
		}

		public override void Unload()
		{
			villagerTextures.Clear();
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (VillagerType == NPCID.Dryad)
				DrawDryadRing(); // behind the Dryad herself (TownVillagerMinion.Dryad.cs)

			Texture2D texture = GetVillagerTexture(VillagerType).Value;
			int frameCount = System.Math.Max(1, Main.npcFrameCount[VillagerType]);
			int frameHeight = texture.Height / frameCount;
			if (frameHeight < 16)
			{
				frameCount = 1;
				frameHeight = texture.Height;
			}

			int frameIndex = System.Math.Clamp(Projectile.frame, 0, frameCount - 1);
			Rectangle frame = new Rectangle(0, frameIndex * frameHeight, texture.Width, frameHeight);
			// Town NPC sprites face left, so flip them to face right
			SpriteEffects effects = Projectile.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

			Main.EntitySpriteDraw(
				texture,
				Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY), // gfxOffY smooths step-ups
				frame,
				lightColor,
				0f,
				frame.Size() / 2f,
				Projectile.scale,
				effects);
			return false;
		}
	}
}
