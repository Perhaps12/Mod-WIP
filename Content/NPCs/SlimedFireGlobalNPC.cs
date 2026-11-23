using Terraria;
using Terraria.ModLoader;

namespace Wprint.Content.NPCs
{
	// Slimed enemies take double damage from fire debuffs.
	// Vanilla has already subtracted each debuff's damage-over-time from lifeRegen by the time
	// this runs, so subtracting the same amount again doubles it.
	public class SlimedFireGlobalNPC : GlobalNPC
	{
		public override void UpdateLifeRegen(NPC npc, ref int damage)
		{
			if (!npc.HasBuff(Terraria.ID.BuffID.Slimed))
				return;

			// Vanilla lifeRegen loss per fire debuff (lifeRegen is in half-HP per second)
			int extra = 0;
			if (npc.onFire) extra += 8;          // On Fire!
			if (npc.onFire2) extra += 48;        // Cursed Inferno
			if (npc.onFire3) extra += 30;        // Hellfire
			if (npc.shadowFlame) extra += 30;    // Shadowflame
			if (npc.onFrostBurn) extra += 16;    // Frostburn
			if (npc.onFrostBurn2) extra += 50;   // Frostbite
			if (extra == 0)
				return;

			if (npc.lifeRegen > 0)
				npc.lifeRegen = 0;
			npc.lifeRegen -= extra;

			// "damage" is the number shown on each damage tick; raise it so the faster burn shows
			int shown = extra / 8;
			if (damage < shown)
				damage = shown;
		}
	}
}
