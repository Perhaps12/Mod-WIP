using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	public class Snowballer : ModItem
	{
		public virtual float SnowballSizeMultiplier => 1f;

		public override string Texture => $"Terraria/Images/Item_{ItemID.Snowball}";

		public override void AddRecipes()
		{
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(ItemID.Snowball, 999);
			recipe.AddTile(TileID.IceMachine);
			recipe.Register();
		}

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.damage = 8;
			Item.DamageType = DamageClass.Melee;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.knockBack = 12f;
			Item.value = Item.buyPrice(silver: 50);
			Item.rare = ItemRarityID.Blue;
			Item.UseSound = SoundID.Item1;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.autoReuse = false;
			Item.shoot = ModContent.ProjectileType<SnowballProjectile>();
			Item.shootSpeed = 1f;
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
			{
				return false;
			}

			Vector2 aim = Main.MouseWorld - player.MountedCenter;
			if (aim.LengthSquared() <= 0.001f)
			{
				aim = Vector2.UnitX * player.direction;
			}

			Projectile heldSnowball = SnowballProjectile.FindHeldSnowball(player, SnowballSizeMultiplier);
			if (heldSnowball != null)
			{
				aim.Normalize();
				((SnowballProjectile)heldSnowball.ModProjectile).Throw(aim);
				return false;
			}

			Projectile.NewProjectile(
				source,
				player.MountedCenter,
				Vector2.Zero,
				type,
				damage,
				knockback,
				player.whoAmI,
				1f,
				SnowballProjectile.HeldMode,
				SnowballSizeMultiplier);

			return false;
		}
	}

	public class Slushballer : Snowballer
	{
		public override float SnowballSizeMultiplier => 2.2f;

		public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.SlushBall}";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.damage = 20;
		}

		// public override void AddRecipes()
		// {
		// 	Recipe recipe = CreateRecipe();
		// 	recipe.AddIngredient(ModContent.ItemType<Snowballer>(), 3);
		// 	recipe.AddTile(TileID.WorkBenches);
		// 	recipe.Register();
		// }
	}
}
