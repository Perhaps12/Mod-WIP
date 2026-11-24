using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Items;

namespace Wprint.Content.NPCs
{
	public class SnowballerDropGlobalNPC : GlobalNPC
	{
		public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
		{
			if (npc.type != NPCID.SnowBalla
				&& npc.type != NPCID.MisterStabby
				&& npc.type != NPCID.SnowmanGangsta)
			{
				return;
			}

			int denominator = npc.type == NPCID.SnowBalla ? 25 : 100;
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Snowballer>(), denominator));
		}
	}
}
