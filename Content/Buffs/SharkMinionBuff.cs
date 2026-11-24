using Terraria;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Buffs
{
	// Shown while you have sharks from the Shark Staff out. Removing it dismisses them.
	public class SharkMinionBuff : ModBuff
	{
		// Placeholder: vanilla Shark Pup pet buff icon
		public override string Texture => "Terraria/Images/Buff_219";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			// Stay as long as at least one shark is out; otherwise remove the buff
			if (player.ownedProjectileCounts[ModContent.ProjectileType<SharkMinion>()] > 0)
			{
				player.buffTime[buffIndex] = 18000;
			}
			else
			{
				player.DelBuff(buffIndex);
				buffIndex--;
			}
		}
	}
}
