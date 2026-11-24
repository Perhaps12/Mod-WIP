using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	// A shotgun that blasts a spread of giant paintballs.
	// Stats copied from the Onyx Blaster for now; uses no ammo, like the Paintball Gun.
	public class PaintballUmbrella : ModItem
	{
		const int Pellets = 5;                            // paintballs per shot
		const float SpreadRadians = MathHelper.Pi / 40f;  // +/-6 degrees of random spread
		// Paintball size is set by GiantPaintball.Scale in Content/Projectiles/GiantPaintball.cs

		// Paint items that lock the shot color, mapped to a hue inside that color's debuff band
		// (see GiantPaintball.PaintDebuff)
		const float Red = 0f, Orange = 0.08f, Yellow = 0.15f, Green = 0.33f, Cyan = 0.5f, Blue = 0.62f, Purple = 0.78f, Pink = 0.9f;
		static readonly Dictionary<int, float> PaintHues = new()
		{
			[ItemID.RedPaint] = Red,         [ItemID.DeepRedPaint] = Red,
			[ItemID.OrangePaint] = Orange,   [ItemID.DeepOrangePaint] = Orange,
			[ItemID.YellowPaint] = Yellow,   [ItemID.DeepYellowPaint] = Yellow,
			[ItemID.LimePaint] = Green,      [ItemID.DeepLimePaint] = Green,
			[ItemID.GreenPaint] = Green,     [ItemID.DeepGreenPaint] = Green,
			[ItemID.TealPaint] = Cyan,       [ItemID.DeepTealPaint] = Cyan,
			[ItemID.CyanPaint] = Cyan,       [ItemID.DeepCyanPaint] = Cyan,
			[ItemID.SkyBluePaint] = Blue,    [ItemID.DeepSkyBluePaint] = Blue,
			[ItemID.BluePaint] = Blue,       [ItemID.DeepBluePaint] = Blue,
			[ItemID.PurplePaint] = Purple,   [ItemID.DeepPurplePaint] = Purple,
			[ItemID.VioletPaint] = Purple,   [ItemID.DeepVioletPaint] = Purple,
			[ItemID.PinkPaint] = Pink,       [ItemID.DeepPinkPaint] = Pink,
		};

		// Tiles are always painted with Deep paints. The 12 basic paints (Red, Orange, Yellow, Lime, Green,
		// Teal, Cyan, Sky Blue, Blue, Purple, Violet, Pink = paint colors 1-12) are spaced 1/12 apart
		// around the color wheel starting at red; their Deep versions are the same order at 13-24.
		const byte DeepPaintOffset = 12;

		// Closest Deep paint color to a hue
		static byte HueToDeepPaintColor(float hue) => (byte)((int)System.Math.Round(hue * 12f) % 12 + 1 + DeepPaintOffset);

		// A basic paint color (1-12) turned into its Deep version; Deep colors are kept as they are
		static byte ToDeepPaintColor(byte paintColor) =>
			paintColor >= 1 && paintColor <= 12 ? (byte)(paintColor + DeepPaintOffset) : paintColor;

		// First matching paint in the inventory (ammo slots first, then the rest), or null
		static Item FindPaint(Player player)
		{
			for (int i = 54; i < 58; i++)   // ammo slots
				if (PaintHues.ContainsKey(player.inventory[i].type) && player.inventory[i].stack > 0)
					return player.inventory[i];
			for (int i = 0; i < 54; i++)    // hotbar + main inventory
				if (PaintHues.ContainsKey(player.inventory[i].type) && player.inventory[i].stack > 0)
					return player.inventory[i];
			return null;
		}

		// Vanilla Tragic Umbrella sprite
		public override string Texture => "Terraria/Images/Item_" + ItemID.TragicUmbrella;

		public override void SetDefaults()
		{
			Item.CloneDefaults(ItemID.OnyxBlaster);        // damage, use time, knockback, rarity, value, sound...
			Item.damage = 40;                              // per paintball
			Item.ArmorPenetration = 20;                    // ignores 20 enemy defense (passed on to the paintballs)
			Item.shoot = ModContent.ProjectileType<GiantPaintball>();
			Item.useAmmo = AmmoID.None;                    // paintballs are free, like the Paintball Gun
			Item.shootSpeed = 15f;                         // much faster than the Onyx Blaster's shots
			Item.noUseGraphic = true;                      // the umbrella is drawn rotated by PaintballUmbrellaLayer
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
			Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			// One paint color per shot, shared by every paintball in it: the color of a paint
			// in your inventory if you have one, otherwise random
			Item paint = FindPaint(player);
			float hue = paint != null ? PaintHues[paint.type] : Main.rand.NextFloat();
			// The bursts paint tiles with Deep paint: the Deep version of the carried paint, or the
			// closest Deep paint to the hue
			float tilePaint = paint != null ? ToDeepPaintColor(paint.paint) : HueToDeepPaintColor(hue);

			for (int i = 0; i < Pellets; i++)
			{
				Vector2 pelletVelocity = velocity.RotatedByRandom(SpreadRadians) * Main.rand.NextFloat(0.85f, 1.15f);
				// ai[0] = paint hue, ai[2] = tile paint color (ai[] syncs in multiplayer)
				Projectile.NewProjectile(source, position, pelletVelocity, type, damage, knockback,
					player.whoAmI, hue, 0f, tilePaint);
			}
			return false; // we spawned the paintballs ourselves
		}

		public override void AddRecipes()
		{
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(ItemID.DirtBlock, 1);
			recipe.AddTile(TileID.WorkBenches);
			recipe.Register();
		}
	}

	// Draws the held umbrella turned 90 degrees, so the canopy points where you aim
	// and the handle stays at your hand.
	public class PaintballUmbrellaLayer : PlayerDrawLayer
	{
		public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.HeldItem);

		public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
		{
			Player player = drawInfo.drawPlayer;
			return player.itemAnimation > 0 && !player.dead
				&& player.HeldItem.type == ModContent.ItemType<PaintballUmbrella>();
		}

		protected override void Draw(ref PlayerDrawSet drawInfo)
		{
			Player player = drawInfo.drawPlayer;
			Texture2D tex = TextureAssets.Item[player.HeldItem.type].Value;

			// itemRotation is relative to the facing direction; turn it into a world aim angle
			float aim = player.itemRotation + (player.direction == -1 ? MathHelper.Pi : 0f);

			// The sprite stands upright (canopy up, handle down), so rotate it 90 degrees
			// to point the canopy along the aim, with the handle (bottom-center) at the hand
			float rotation = aim + MathHelper.PiOver2;
			Vector2 origin = new Vector2(tex.Width / 2f, tex.Height);
			SpriteEffects flip = player.direction == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

			Vector2 position = (player.MountedCenter - Main.screenPosition).Floor();
			Color color = Lighting.GetColor(player.MountedCenter.ToTileCoordinates());

			drawInfo.DrawDataCache.Add(new DrawData(tex, position, null, color, rotation, origin,
				player.HeldItem.scale, flip));
		}
	}
}
