using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	public class TentacleSentry : ModItem
	{
		public override string Texture => $"Terraria/Images/Item_{ItemID.TentacleSpike}";

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.damage = 15;
			Item.knockBack = 2f;
			Item.mana = 3;
			Item.DamageType = DamageClass.Summon;
			Item.useTime = 30;
			Item.useAnimation = 30;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.UseSound = SoundID.Item44;
			Item.noMelee = true;
			Item.sentry = true;
			Item.shoot = ModContent.ProjectileType<TentacleSentryProjectile>();
			Item.shootSpeed = 0f;
			Item.value = Item.buyPrice(gold: 1);
			Item.rare = ItemRarityID.Blue;
		}

		public override void ModifyShootStats(
			Player player,
			ref Vector2 position,
			ref Vector2 velocity,
			ref int type,
			ref int damage,
			ref float knockback)
		{
			player.FindSentryRestingSpot(type, out int worldX, out int worldY, out _);
			position = new Vector2(worldX, worldY - 10f);
			velocity = Vector2.Zero;
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
			if (player.whoAmI != Main.myPlayer)
				return false;

			Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
			int maxTurrets = player.maxTurrets;
			player.maxTurrets = maxTurrets * 2;
			try
			{
				player.UpdateMaxTurrets();
			}
			finally
			{
				player.maxTurrets = maxTurrets;
			}
			return false;
		}

		public override void AddRecipes()
		{
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(ItemID.TentacleSpike);
			recipe.AddIngredient(ItemID.TissueSample, 5);
			recipe.AddTile(TileID.Anvils);
			recipe.Register();
		}
	}
}
