using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;

namespace Wprint
{
	// Identifies the kind of information sent in each mod network packet.
	internal enum WprintPacketType : byte
	{
		CatchableAxeCaught,
		SyncCatchableAxeState,
		LaevateinnChargeState,
		LaevateinnResetCharge
	}

	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class Wprint : Mod
	{
		// sample change
		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			// Read the packet type first so the remaining bytes can be interpreted correctly.
			WprintPacketType packetType = (WprintPacketType)reader.ReadByte();
			if (packetType == WprintPacketType.CatchableAxeCaught)
			{
				int playerIndex = reader.ReadByte();
				// Accept catch reports only from that player's own client.
				if (Main.netMode != NetmodeID.Server || whoAmI != playerIndex || playerIndex >= Main.maxPlayers)
				{
					return;
				}

				CatchableAxePlayer modPlayer = Main.player[playerIndex].GetModPlayer<CatchableAxePlayer>();
				modPlayer.RegisterServerCatch();
				modPlayer.SendState(-1, whoAmI);
			}
			else if (packetType == WprintPacketType.SyncCatchableAxeState)
			{
				int playerIndex = reader.ReadByte();
				int stacks = reader.ReadByte();
				// Reject invalid player indices or stack counts before applying network data.
				if (playerIndex >= Main.maxPlayers || stacks > CatchableAxePlayer.MaxStacks)
				{
					return;
				}

				if (Main.netMode == NetmodeID.Server)
				{
					// Verify the sender owns this player, then relay the updated count.
					if (whoAmI != playerIndex)
					{
						return;
					}

					CatchableAxePlayer modPlayer = Main.player[playerIndex].GetModPlayer<CatchableAxePlayer>();
					modPlayer.SetSyncedState(stacks);
					modPlayer.SendState(-1, whoAmI);
				}
				else
				{
					// Clients apply the stack count sent by the server.
					Main.player[playerIndex].GetModPlayer<CatchableAxePlayer>().SetSyncedState(stacks);
				}
			}
			else if (packetType == WprintPacketType.LaevateinnChargeState)
			{
				int playerIndex = reader.ReadByte();
				int chargeTicks = reader.ReadInt32();
				bool powerFrozen = reader.ReadBoolean();
				if (Main.netMode == NetmodeID.MultiplayerClient
					&& playerIndex < Main.maxPlayers
					&& chargeTicks >= 0)
				{
					Main.player[playerIndex].GetModPlayer<LaevateinnPlayer>().SetChargeState(chargeTicks, powerFrozen);
				}
			}
			else if (packetType == WprintPacketType.LaevateinnResetCharge)
			{
				int playerIndex = reader.ReadByte();
				if (Main.netMode != NetmodeID.Server || whoAmI != playerIndex || playerIndex >= Main.maxPlayers)
				{
					return;
				}

				LaevateinnPlayer laevateinnPlayer = Main.player[playerIndex].GetModPlayer<LaevateinnPlayer>();
				laevateinnPlayer.ResetCharge();
				laevateinnPlayer.SendChargeState(-1, whoAmI);
			}
		}
	}
}
