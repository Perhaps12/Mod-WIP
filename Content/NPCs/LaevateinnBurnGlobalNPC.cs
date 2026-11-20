using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Wprint.Content.Buffs;

namespace Wprint.Content.NPCs
{
	public class LaevateinnBurnGlobalNPC : GlobalNPC
	{
		private const float BaseDamagePerSecond = 1f;
		private const float MaximumDamagePerSecond = 1_000_000f;
		private float _burnMultiplier;

		public override bool InstancePerEntity => true;

		public override void ResetEffects(NPC npc)
		{
			if (!npc.HasBuff(ModContent.BuffType<LaevateinnBurnBuff>()))
			{
				_burnMultiplier = 0f;
			}
		}

		public override void UpdateLifeRegen(NPC npc, ref int damage)
		{
			int damagePerSecond = GetDamagePerSecond(npc);
			if (damagePerSecond == 0)
			{
				return;
			}

			if (npc.lifeRegen > 0)
			{
				npc.lifeRegen = 0;
			}

			npc.lifeRegen -= damagePerSecond * 2;
			damage = Math.Max(damage, damagePerSecond);
		}

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
		{
			binaryWriter.Write(_burnMultiplier);
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
		{
			float multiplier = binaryReader.ReadSingle();
			_burnMultiplier = float.IsNaN(multiplier) || float.IsInfinity(multiplier)
				? 0f
				: Math.Max(0f, Math.Min(multiplier, 1_000_000f));
		}

		public void ApplyBurn(float multiplier)
		{
			if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
			{
				return;
			}

			_burnMultiplier = Math.Max(_burnMultiplier, Math.Min(multiplier, 1_000_000f));
		}

		public void ClearBurn()
		{
			_burnMultiplier = 0f;
		}

		public int GetRemainingDamage(NPC npc)
		{
			int buffIndex = npc.FindBuffIndex(ModContent.BuffType<LaevateinnBurnBuff>());
			if (buffIndex < 0)
			{
				return 0;
			}

			int damagePerSecond = GetDamagePerSecond(npc);
			double remainingDamage = Math.Ceiling(damagePerSecond * npc.buffTime[buffIndex] / 60d);
			return (int)Math.Min(int.MaxValue, remainingDamage);
		}

		private int GetDamagePerSecond(NPC npc)
		{
			if (_burnMultiplier <= 0f || !npc.HasBuff(ModContent.BuffType<LaevateinnBurnBuff>()))
			{
				return 0;
			}

			return (int)Math.Min(MaximumDamagePerSecond, Math.Ceiling(BaseDamagePerSecond * _burnMultiplier));
		}
	}
}
