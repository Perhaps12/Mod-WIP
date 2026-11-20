using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint.Content.Buffs
{
	public class CatchableAxeBuff : ModBuff
	{
		// Reuse Terraria's Archery icon until this mod has a custom buff texture.
		public override string Texture => $"Terraria/Images/Buff_{BuffID.Archery}";

		public override void SetStaticDefaults()
		{
			// This short-lived reward should not persist when the player saves and quits.
			Main.buffNoSave[Type] = true;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			// Show the current stack count and remaining buff time in the icon tooltip.
			Player player = Main.LocalPlayer;
			CatchableAxePlayer modPlayer = player.GetModPlayer<CatchableAxePlayer>();
			int buffIndex = player.FindBuffIndex(Type);
			int secondsRemaining = buffIndex >= 0 ? (player.buffTime[buffIndex] + 59) / 60 : 0;
			tip = Language.GetTextValue("Mods.Wprint.Buffs.CatchableAxeBuff.Description", modPlayer.Stacks, secondsRemaining);
		}
	}
}
