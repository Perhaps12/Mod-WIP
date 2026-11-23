using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Buffs
{
	public class LaevateinnBurnBuff : ModBuff
	{
		public override string Texture => $"Terraria/Images/Buff_{BuffID.OnFire}";

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = true;
		}
	}
}
