using Terraria;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Buffs
{
	public class TownCallBuff : ModBuff
	{
		public override string Texture => $"Terraria/Images/Buff_{Terraria.ID.BuffID.BabyBird}";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			if (player.ownedProjectileCounts[ModContent.ProjectileType<TownVillagerMinion>()] > 0)
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
