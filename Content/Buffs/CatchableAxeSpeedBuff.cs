using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Buffs
{
	public class CatchableAxeSpeedBuff : ModBuff
	{
		public const float MoveSpeedBonus = 1.2f;

		public override string Texture => $"Terraria/Images/Buff_{BuffID.Swiftness}";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			player.moveSpeed += MoveSpeedBonus;
		}
	}
}
