using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Items
{
	public class catchableaxe : ModItem
	{
		public override void SetDefaults()
		{
			// Basic weapon stats and how often the axe can be used.
			Item.damage = 27;
			Item.DamageType = DamageClass.Ranged;
			Item.width = 75;
			Item.height = 75;
			Item.useTime = 30;
			Item.useAnimation = 30;
			Item.useStyle = ItemUseStyleID.Swing;
            Item.noUseGraphic = true;
            Item.crit = 19;
			Item.knockBack = 6;
			Item.value = Item.buyPrice(gold: 1);
			Item.rare = ItemRarityID.Orange;
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = true;
			// Configure the thrown projectile and its initial speed.
            Item.shootSpeed = 18f;
            Item.shoot = ModContent.ProjectileType<catchableaxeProjectile>();
            Item.useTurn = true;
		}

		public override float UseSpeedMultiplier(Player player)
		{
			// This is a speed multiplier: doubling it at ten stacks halves the 30-tick use interval.
			int stacks = player.GetModPlayer<CatchableAxePlayer>().Stacks;
			return 1f / (1f - stacks * 0.05f);
		}

		public override void AddRecipes()
		{
			// Make this axe at a work bench using one dirt block.
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(ItemID.DirtBlock, 1);
			recipe.AddTile(TileID.WorkBenches);
			recipe.Register();
		}
	}
}
