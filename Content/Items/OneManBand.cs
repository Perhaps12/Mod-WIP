using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Items
{
	/// <summary>
	/// Accessory that raises the player's sentry limit and marks them for the sentry-only damage bonus.
	/// </summary>
	public class OneManBand : ModItem
	{
		// Reuse a vanilla instrument icon until this item has a dedicated texture.
		public override string Texture => $"Terraria/Images/Item_{ItemID.DrumSet}";

		public override void SetDefaults()
		{
			// Basic inventory dimensions and accessory properties.
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.value = Item.buyPrice(gold: 2);
			Item.rare = ItemRarityID.Blue;
		}

		/// <summary>
		/// Runs while the accessory is equipped. The extra turret slot is rebuilt each tick,
		/// and the player flag lets the projectile hook recognize the damage bonus.
		/// </summary>
		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.maxTurrets++;
			player.GetModPlayer<OneManBandPlayer>().OneManBandEquipped = true;
		}
	}
}
