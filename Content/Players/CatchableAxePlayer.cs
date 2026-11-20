using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Buffs;

namespace Wprint.Content.Players
{
	public class CatchableAxePlayer : ModPlayer
	{
		// The axe's reward caps at ten catches and expires after five seconds.
		public const int MaxStacks = 10;
		public const int StackDurationTicks = 5 * 60;
		public const int MaxStackSpeedBuffDurationTicks = 2 * 60;

		public int Stacks { get; private set; }

		public void CatchAxe()
		{
			bool wasAlreadyAtMax = Stacks >= MaxStacks;

			// Add a stack, refresh the buff, and show the current progress above the player.
			AddStack();
			float colorProgress = (float)(Stacks - 1) / (MaxStacks - 1);
			Color counterColor = Color.Lerp(Color.Gray, Color.Yellow, colorProgress);
			string counterText = $"{Stacks}/{MaxStacks}";
			if (Stacks == MaxStacks)
			{
				counterText = wasAlreadyAtMax ? "MAX+" : "MAX";
				counterColor = wasAlreadyAtMax ? Color.Yellow : Color.Gold;
			}
			CombatText.NewText(Player.Hitbox, counterColor, counterText);

			float pitch = MathHelper.Lerp(-0.5f, 0.5f, colorProgress);
			SoundEngine.PlaySound(SoundID.Item129 with { Pitch = pitch }, Player.Center);

			// Tell the server about the catch so it can update the player's stack state.
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)global::Wprint.WprintPacketType.CatchableAxeCaught);
				packet.Write((byte)Player.whoAmI);
				packet.Send();
			}
		}

		// The server independently applies the catch reported by the owning client.
		internal void RegisterServerCatch()
		{
			AddStack();
		}

		// Apply a stack count received from the server or another joining client.
		internal void SetSyncedState(int stacks)
		{
			Stacks = stacks;
		}

		// Send this player's current stack count to a client or to the server.
		internal void SendState(int toWho, int fromWho)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)global::Wprint.WprintPacketType.SyncCatchableAxeState);
			packet.Write((byte)Player.whoAmI);
			packet.Write((byte)Stacks);
			packet.Send(toWho, fromWho);
		}

		// Share the stack count when a player joins or another player joins.
		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			SendState(toWho, fromWho);
		}

		public override void PostUpdate()
		{
			// The buff controls the timer; clear stacks when it expires.
			if (Stacks > 0 && !Player.HasBuff(ModContent.BuffType<CatchableAxeBuff>()))
			{
				Stacks = 0;
				if (Main.netMode == NetmodeID.Server)
				{
					SendState(-1, Player.whoAmI);
				}
			}
		}

		// Increase stacks up to the cap and refresh the five-second buff.
		private void AddStack()
		{
			Stacks = System.Math.Min(Stacks + 1, MaxStacks);
			Player.AddBuff(ModContent.BuffType<CatchableAxeBuff>(), StackDurationTicks);

			// Catching at maximum stacks also grants a brief movement-speed boost.
			if (Stacks == MaxStacks)
			{
				Player.AddBuff(ModContent.BuffType<CatchableAxeSpeedBuff>(), MaxStackSpeedBuffDurationTicks);
			}
		}
	}
}
