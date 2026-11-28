using Terraria.ModLoader;

namespace Wprint.Content.Players
{
	public class OneManBandPlayer : ModPlayer
	{
		public bool OneManBandEquipped { get; set; }

		public override void ResetEffects()
		{
			OneManBandEquipped = false;
		}
	}
}
