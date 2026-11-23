using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace Wprint.Content.Items
{
	public class RandomHandgunRollLayer : PlayerDrawLayer
	{
		public override Position GetDefaultPosition()
		{
			return new AfterParent(PlayerDrawLayers.Head);
		}

		public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
		{
			return FindDisplayingGun(drawInfo.drawPlayer) != null;
		}

		protected override void Draw(ref PlayerDrawSet drawInfo)
		{
			if (drawInfo.shadow != 0f)
			{
				return;
			}

			RandomHandgun gun = FindDisplayingGun(drawInfo.drawPlayer);
			if (gun == null)
			{
				return;
			}

			string text;
			Color textColor;
			if (gun.IsRolling)
			{
				text = Language.GetTextValue(
					"Mods.Wprint.Items.RandomHandgun.Rolling",
					gun.RollPreviewName);
				textColor = gun.RollPreviewTint;
			}
			else
			{
				text = Language.GetTextValue(
					"Mods.Wprint.Items.RandomHandgun.Equipped",
					gun.EquippedProfileName);
				textColor = gun.EquippedProfileTint;
			}

			Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text);
			Vector2 position = drawInfo.drawPlayer.Top
				- Main.screenPosition
				- new Vector2(textSize.X * 0.5f, 34f);

			ChatManager.DrawColorCodedStringWithShadow(
				Main.spriteBatch,
				FontAssets.MouseText.Value,
				text,
				position,
				textColor,
				0f,
				Vector2.Zero,
				Vector2.One);
		}

		private static RandomHandgun FindDisplayingGun(Player player)
		{
			foreach (Item item in player.inventory)
			{
				if (item.ModItem is RandomHandgun gun && (gun.IsRolling || gun.IsShowingEquipNotice))
				{
					return gun;
				}
			}

			return null;
		}
	}
}
