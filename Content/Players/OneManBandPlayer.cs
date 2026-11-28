using Terraria.ModLoader;

namespace Wprint.Content.Players
{
	/// <summary>
	/// Per-player equipment state used by OneManBandGlobalProjectile.
	/// </summary>
	public class OneManBandPlayer : ModPlayer
	{
		// Set by OneManBand.UpdateAccessory while equipped.
		public bool OneManBandEquipped { get; set; }

		// Accessory effects are recalculated every tick, so clear the flag before equipment updates.
		public override void ResetEffects()
		{
			OneManBandEquipped = false;
		}
	}
}
