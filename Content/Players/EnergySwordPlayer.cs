using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Players
{
	public class EnergySwordPlayer : ModPlayer
	{
		public bool HasStoredCharge { get; private set; }
		public bool IsDetonating => _detonationTicks > 0;

		private int _detonationTicks;
		private Vector2 _detonationCenter;

		public void StoreCharge()
		{
			if (HasStoredCharge)
			{
				return;
			}

			HasStoredCharge = true;
			if (Main.netMode == NetmodeID.Server)
			{
				SendChargeState(-1, -1);
			}
		}

		internal void SetStoredCharge(bool hasCharge)
		{
			HasStoredCharge = hasCharge;
		}

		public void BeginDetonation()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				if (HasStoredCharge)
				{
					HasStoredCharge = false;
					if (Main.netMode == NetmodeID.Server)
					{
						SendChargeState(-1, -1);
					}
				}
				else
				{
					PayUnchargedDetonationCost();
				}
			}
			else
			{
				HasStoredCharge = false;
			}

			_detonationCenter = Player.Center;
			_detonationTicks = EnergySwordExplosion.DetonationDuration;
		}

		void PayUnchargedDetonationCost()
		{
			int lifeCost = (Player.statLifeMax2 + 2) / 3;
			Player.statLife -= lifeCost;
			if (Player.statLife <= 0)
			{
				Player.statLife = 1;
				Player.KillMe(
					PlayerDeathReason.ByCustomReason(
						NetworkText.FromLiteral($"{Player.name} was consumed by the Energy Sword.")),
					lifeCost,
					Player.direction);
			}
			else if (Main.netMode == NetmodeID.Server)
			{
				NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, Player.whoAmI);
			}
		}

		public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable)
		{
			return IsDetonating;
		}

		public override void PreUpdateMovement()
		{
			if (!IsDetonating)
			{
				return;
			}

			Player.Center = _detonationCenter;
			Player.velocity = Vector2.Zero;
			Player.noKnockback = true;
		}

		public override void PostUpdate()
		{
			if (HasStoredCharge && !Main.dedServ
				&& (Main.GameUpdateCount + (ulong)Player.whoAmI) % 8UL == 0UL)
			{
				float rotation = Main.GameUpdateCount * 0.08f;
				for (int i = 0; i < 4; i++)
				{
					float angle = rotation + MathHelper.TwoPi * i / 4f;
					Vector2 offset = angle.ToRotationVector2() * 28f;
					Vector2 velocity = new Vector2(-offset.Y, offset.X) * 0.025f;
					Dust particle = Dust.NewDustPerfect(
						Player.Center + offset,
						DustID.Torch,
						velocity,
						0,
						new Color(255, 190, 65),
						1.1f);
					particle.noGravity = true;
					particle.fadeIn = 1.2f;
				}
			}

			if (!IsDetonating)
			{
				return;
			}

			Player.Center = _detonationCenter;
			Player.velocity = Vector2.Zero;
			Player.noKnockback = true;
			Player.immune = true;
			Player.immuneTime = Math.Max(Player.immuneTime, 2);
			_detonationTicks--;
		}

		public override void OnRespawn()
		{
			_detonationTicks = 0;
			HasStoredCharge = false;
		}

		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			if (Main.netMode == NetmodeID.Server)
			{
				SendChargeState(toWho, fromWho);
			}
		}

		void SendChargeState(int toWho, int fromWho)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)global::Wprint.WprintPacketType.EnergySwordChargeState);
			packet.Write((byte)Player.whoAmI);
			packet.Write(HasStoredCharge);
			packet.Send(toWho, fromWho);
		}
	}
}
