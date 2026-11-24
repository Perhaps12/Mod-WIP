using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	public class EokaPistol : ModItem
	{
		public override string Texture => $"Terraria/Images/Item_{ItemID.FlintlockPistol}";

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.damage = 77;
			Item.DamageType = DamageClass.Ranged;
			Item.useTime = 30;
			Item.useAnimation = 30;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.knockBack = 6f;
			Item.value = Item.buyPrice(gold: 1);
			Item.rare = ItemRarityID.Green;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.autoReuse = false;
			Item.useAmmo = AmmoID.None;
			Item.shoot = ModContent.ProjectileType<EokaBlastProjectile>();
			Item.shootSpeed = 1f;
		}

		public override bool CanUseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
			{
				player.GetModPlayer<EokaPistolPlayer>().TryBegin(Item);
				return false;
			}

			return true;
		}

		public override bool Shoot(
			Player player,
			EntitySource_ItemUse_WithAmmo source,
			Vector2 position,
			Vector2 velocity,
			int type,
			int damage,
			float knockback)
		{
			return false;
		}

		public override void AddRecipes()
		{
			AddRecipeForOre(ItemID.IronOre);
			AddRecipeForOre(ItemID.LeadOre);
		}

		private void AddRecipeForOre(int oreType)
		{
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(oreType, 12);
			recipe.AddIngredient(ItemID.Wood, 50);
			recipe.AddTile(TileID.WorkBenches);
			recipe.Register();
		}
	}

	public class EokaPistolHeldItemLayer : PlayerDrawLayer
	{
		public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.HeldItem);

		public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
		{
			Player player = drawInfo.drawPlayer;
			return !player.dead
				&& player.HeldItem.type == ModContent.ItemType<EokaPistol>()
				&& player.GetModPlayer<EokaPistolPlayer>().IsFiring;
		}

		protected override void Draw(ref PlayerDrawSet drawInfo)
		{
			if (drawInfo.shadow != 0f)
			{
				return;
			}

			Player player = drawInfo.drawPlayer;
			Item item = player.HeldItem;
			Texture2D texture = TextureAssets.Item[item.type].Value;
			float worldAim = player.itemRotation
				+ (player.direction == -1 ? MathHelper.Pi : 0f);
			Vector2 direction = worldAim.ToRotationVector2();
			Vector2 position = (player.MountedCenter + direction * 10f - Main.screenPosition).Floor();
			Vector2 origin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
			Color color = Lighting.GetColor(player.MountedCenter.ToTileCoordinates());
			SpriteEffects effects = player.direction == -1
				? SpriteEffects.FlipHorizontally
				: SpriteEffects.None;

			drawInfo.DrawDataCache.Add(new DrawData(
				texture,
				position,
				null,
				color,
				player.itemRotation,
				origin,
				item.scale,
				effects));
		}
	}
}
