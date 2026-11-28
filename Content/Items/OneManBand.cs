using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Items
{
	public class OneManBand : ModItem
	{
		public override string Texture => $"Terraria/Images/Item_{ItemID.DrumSet}";

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.value = Item.buyPrice(gold: 2);
			Item.rare = ItemRarityID.Blue;
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.maxTurrets++;
			player.GetModPlayer<OneManBandPlayer>().OneManBandEquipped = true;
		}
	}
}
