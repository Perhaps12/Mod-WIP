using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Wprint.Content.Players
{
	public class SentryWhistlePlayer : ModPlayer
	{
		private int _cooldownTicks;

		public bool CanUseWhistle => _cooldownTicks <= 0 && HasOwnedSentries();

		public override void PostUpdate()
		{
			if (_cooldownTicks > 0)
				_cooldownTicks--;
		}

		public bool TeleportSentries(int cooldownTicks)
		{
			if (Player.whoAmI != Main.myPlayer || !HasOwnedSentries())
				return false;

			Vector2 destination = Main.MouseWorld;
			Player.LimitPointToPlayerReachableArea(ref destination);

			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile projectile = Main.projectile[i];
				if (!projectile.active || !projectile.sentry || projectile.owner != Player.whoAmI)
					continue;

				projectile.Center = destination;
				projectile.velocity = Vector2.Zero;
				projectile.netUpdate = true;
			}

			_cooldownTicks = cooldownTicks;
			return true;
		}

		private bool HasOwnedSentries()
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile projectile = Main.projectile[i];
				if (projectile.active && projectile.sentry && projectile.owner == Player.whoAmI)
					return true;
			}

			return false;
		}
	}
}
