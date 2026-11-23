using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Players
{
	public class LaevateinnPlayer : ModPlayer
	{
		private const int TicksPerSecond = 60;
		public const int ChargeTicksPerSwing = 24;
		private const int LifeCostPerSwing = 10;
		private const float MaximumPowerMultiplier = 1_000_000f;

		public int ChargeTicks { get; private set; }
		public bool PowerFrozen { get; private set; }

		public float PowerMultiplier
		{
			get
			{
				double multiplier = Math.Pow(2d, ChargeTicks / (4d * TicksPerSecond));
				return (float)Math.Min(multiplier, MaximumPowerMultiplier);
			}
		}

		public float RegisterSwing()
		{
			if (PowerFrozen)
			{
				return PowerMultiplier;
			}

			if (Main.netMode != NetmodeID.MultiplayerClient && Player.statLife <= 1)
			{
				PowerFrozen = true;
				if (Main.netMode == NetmodeID.Server)
				{
					SendChargeState(-1, -1);
				}
				return PowerMultiplier;
			}

			int swingNumber = ChargeTicks / ChargeTicksPerSwing + 1;
			ChargeTicks = Math.Min(int.MaxValue - ChargeTicksPerSwing, ChargeTicks) + ChargeTicksPerSwing;

			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				return PowerMultiplier;
			}

			int lifeCost = (int)Math.Min(int.MaxValue, (long)swingNumber * LifeCostPerSwing);
			Player.statLife = Math.Max(1, Player.statLife - lifeCost);
			if (Player.statLife == 1)
			{
				PowerFrozen = true;
			}

			if (Main.netMode == NetmodeID.Server)
			{
				NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, Player.whoAmI);
				SendChargeState(-1, -1);
			}

			return PowerMultiplier;
		}

		public void ResetCharge()
		{
			ChargeTicks = 0;
			PowerFrozen = false;
		}

		public void RequestChargeReset()
		{
			ResetCharge();
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)global::Wprint.WprintPacketType.LaevateinnResetCharge);
				packet.Write((byte)Player.whoAmI);
				packet.Send();
			}
		}

		internal void SetChargeState(int chargeTicks, bool powerFrozen)
		{
			ChargeTicks = chargeTicks;
			PowerFrozen = powerFrozen;
		}

		internal void SendChargeState(int toWho, int fromWho)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)global::Wprint.WprintPacketType.LaevateinnChargeState);
			packet.Write((byte)Player.whoAmI);
			packet.Write(ChargeTicks);
			packet.Write(PowerFrozen);
			packet.Send(toWho, fromWho);
		}

		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			if (Main.netMode == NetmodeID.Server)
			{
				SendChargeState(toWho, fromWho);
			}
		}
	}
}
