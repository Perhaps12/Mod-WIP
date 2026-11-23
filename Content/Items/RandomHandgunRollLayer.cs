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
			string measurementText;
			Color textColor;
			if (gun.IsRolling)
			{
				string previewName = gun.RollPreviewName;
				measurementText = Language.GetTextValue(
					"Mods.Wprint.Items.RandomHandgun.Rolling",
					previewName);
				Color previewTint = gun.RollPreviewTint;
				string coloredPreviewName =
					$"[c/{previewTint.R:X2}{previewTint.G:X2}{previewTint.B:X2}:{previewName}]";
				text = Language.GetTextValue(
					"Mods.Wprint.Items.RandomHandgun.Rolling",
					coloredPreviewName);
				textColor = Color.White;
			}
			else
			{
				string equippedName = gun.EquippedProfileName;
				measurementText = Language.GetTextValue(
					"Mods.Wprint.Items.RandomHandgun.Equipped",
					equippedName);
				Color equippedTint = gun.EquippedProfileTint;
				string coloredEquippedName =
					$"[c/{equippedTint.R:X2}{equippedTint.G:X2}{equippedTint.B:X2}:{equippedName}]";
				text = Language.GetTextValue(
					"Mods.Wprint.Items.RandomHandgun.Equipped",
					coloredEquippedName);
				textColor = Color.White;
			}

			Vector2 textSize = FontAssets.MouseText.Value.MeasureString(measurementText);
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
