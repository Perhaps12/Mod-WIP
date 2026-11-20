using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	// Blast Pack: a thrown, stacking, sticky explosive (like a Sticky Grenade) whose blast launches
	// the player away from it. Right click (while holding it) detonates every Blast Pack you have out.
	// The actual behaviour lives in Content/Projectiles/BlastPackProjectile.cs.
	public class BlastPack : ModItem
	{
		// Inventory sprite: vanilla item 3630 (Orange Weighted Pressure Plate)
		public override string Texture => "Terraria/Images/Item_3630";

		public override void SetDefaults()
		{
			// Stats based on the vanilla Sticky Grenade
			Item.width = 20;
			Item.height = 20;
			Item.damage = 60;
			Item.DamageType = DamageClass.Ranged;
			Item.knockBack = 8f;
			Item.useTime = 45;
			Item.useAnimation = 45;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.noMelee = true;
			Item.noUseGraphic = true;      // the thrown projectile is drawn instead
			Item.consumable = true;
			Item.maxStack = Item.CommonMaxStack;
			Item.value = Item.buyPrice(copper: 75);
			Item.rare = ItemRarityID.White;
			Item.shoot = ModContent.ProjectileType<BlastPackProjectile>();
			Item.shootSpeed = 5.5f;
			Item.UseSound = SoundID.Item1;
		}

		// Right click detonation is handled in Content/Players/BlastPackPlayer.cs, outside the
		// item's use cycle, so it works even mid-throw.

		public override void AddRecipes()
		{
			// Placeholder recipe for testing
			Recipe recipe = CreateRecipe(100);
			recipe.AddIngredient(ItemID.DirtBlock, 1);
			recipe.AddTile(TileID.WorkBenches);
			recipe.Register();
		}
	}
}
