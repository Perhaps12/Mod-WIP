using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Items
{
	public class SentryWhistle : ModItem
	{
		public override string Texture => $"Terraria/Images/Item_{ItemID.DrumSet}";

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = SoundID.Item4;
			Item.noMelee = true;
			Item.value = Item.buyPrice(silver: 50);
			Item.rare = ItemRarityID.Blue;
		}

		public override bool CanUseItem(Player player)
		{
			return player.GetModPlayer<SentryWhistlePlayer>().CanUseWhistle;
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return false;

			return player.GetModPlayer<SentryWhistlePlayer>().TeleportSentries(8 * 60);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock, 10)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}

	public class AdvancedSentryWhistle : ModItem
	{
		public override string Texture => $"Terraria/Images/Item_{ItemID.DrumSet}";

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = SoundID.Item4;
			Item.noMelee = true;
			Item.value = Item.buyPrice(gold: 2);
			Item.rare = ItemRarityID.Orange;
		}

		public override bool CanUseItem(Player player)
		{
			return player.GetModPlayer<SentryWhistlePlayer>().CanUseWhistle;
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return false;

			return player.GetModPlayer<SentryWhistlePlayer>().TeleportSentries(3 * 60);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock, 10)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
