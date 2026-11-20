using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	// Silksong-style Clawline: fires a hook at the mouse. The player hangs in the air while it flies,
	// then gets yanked to wherever it lands (keeping their momentum), or bounces off an enemy it hits.
	// The actual behaviour lives in Content/Projectiles/ClawlineHook.cs.
	public class Clawline : ModItem
	{
		// Temporary sprite: reuse the sample sword
		public override string Texture => "Wprint/Content/Items/sample";

		public override void SetDefaults()
		{
			Item.width = 40;
			Item.height = 40;
			Item.damage = 15;
			Item.DamageType = DamageClass.Melee;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.noMelee = true;
			Item.noUseGraphic = true;      // the hook projectile is drawn instead
			Item.knockBack = 3f;
			Item.value = Item.buyPrice(silver: 1);
			Item.rare = ItemRarityID.Blue;
			Item.shoot = ModContent.ProjectileType<ClawlineHook>();
			Item.shootSpeed = 22f;         // x2 because the hook uses 1 extra update per tick = 28 px/tick
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = false;
		}

		// Only one hook out at a time, and limited uses while airborne (see ClawlinePlayer)
		public override bool CanUseItem(Player player)
		{
			return player.GetModPlayer<ClawlinePlayer>().CanUseClawline();
		}

		// No Shoot override needed: vanilla already fires the projectile toward the mouse.

		public override void AddRecipes()
		{
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(ItemID.DirtBlock, 1);
			recipe.AddTile(TileID.WorkBenches);
			recipe.Register();
		}
	}
}
