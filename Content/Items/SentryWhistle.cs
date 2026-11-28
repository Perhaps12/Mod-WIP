using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Items
{
	/// <summary>
	/// Teleports the owner's active sentries to the cursor, subject to the shared whistle cooldown.
	/// </summary>
	public class SentryWhistle : ModItem
	{
		// Use a vanilla whistle sprite until a dedicated item texture is added.
		public override string Texture => $"Terraria/Images/Item_{ItemID.GolfWhistle}";

		public override void SetDefaults()
		{
			// Configure this as a short, non-melee utility use with a whistle sound.
			Item.width = 32;
			Item.height = 32;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = SoundID.Item128;
			Item.noMelee = true;
			Item.value = Item.buyPrice(silver: 50);
			Item.rare = ItemRarityID.Blue;
		}

		// Prevent use while either whistle is cooling down or the player has no sentries to move.
		public override bool CanUseItem(Player player)
		{
			return player.GetModPlayer<SentryWhistlePlayer>().CanUseWhistle;
		}

		// Only the local player's cursor is available here; the ModPlayer moves and syncs the sentries.
		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return false;

			// Cooldown is measured in game ticks (60 ticks per second).
			return player.GetModPlayer<SentryWhistlePlayer>().TeleportSentries(5 * 60);
		}

		// Temporary recipe for testing; replace the Dirt Blocks when balancing the item.
		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock, 10)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}

	/// <summary>
	/// Faster-cooldown version of SentryWhistle. It shares the same player's cooldown state.
	/// </summary>
	public class AdvancedSentryWhistle : ModItem
	{
		// Use a vanilla whistle sprite until a dedicated item texture is added.
		public override string Texture => $"Terraria/Images/Item_{ItemID.BabyGrinchMischiefWhistle}";

		public override void SetDefaults()
		{
			// Configure this as a short, non-melee utility use with a whistle sound.
			Item.width = 32;
			Item.height = 32;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = SoundID.Item128;
			Item.noMelee = true;
			Item.value = Item.buyPrice(gold: 2);
			Item.rare = ItemRarityID.Orange;
		}

		// Share the basic whistle's cooldown and require at least one sentry to teleport.
		public override bool CanUseItem(Player player)
		{
			return player.GetModPlayer<SentryWhistlePlayer>().CanUseWhistle;
		}

		// Only the local player's cursor is available here; the ModPlayer moves and syncs the sentries.
		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return false;

			// This version's cooldown is one second (60 ticks).
			return player.GetModPlayer<SentryWhistlePlayer>().TeleportSentries(1 * 60);
		}

		// Temporary recipe for testing; replace the Dirt Blocks when balancing the item.
		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock, 10)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
