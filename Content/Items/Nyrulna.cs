using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	public class Nyrulna : ModItem
	{
		public const float ThrowSpeed = 28f;

		public override string Texture => $"Terraria/Images/Item_{ItemID.TitaniumTrident}";

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.damage = 100;
			Item.DamageType = DamageClass.Melee;
			Item.useTime = 30;
			Item.useAnimation = 30;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.knockBack = 8f;
			Item.value = Item.buyPrice(gold: 5);
			Item.rare = ItemRarityID.Lime;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.shoot = ModContent.ProjectileType<NyrulnaThrownProjectile>();
			Item.shootSpeed = 1f;
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = false;
		}

		public override bool AltFunctionUse(Player player) => true;

		public override bool CanUseItem(Player player)
		{
			bool alternateUse = player.altFunctionUse == 2;
			Item.shoot = alternateUse
				? ModContent.ProjectileType<NyrulnaConeProjectile>()
				: ModContent.ProjectileType<NyrulnaThrownProjectile>();
			Item.useTime = alternateUse ? 24 : 30;
			Item.useAnimation = Item.useTime;
			return alternateUse || player.ownedProjectileCounts[ModContent.ProjectileType<NyrulnaThrownProjectile>()] == 0;
		}

		public override void ModifyShootStats(
			Player player,
			ref Vector2 position,
			ref Vector2 velocity,
			ref int type,
			ref int damage,
			ref float knockback)
		{
			position = player.MountedCenter;
			Vector2 direction = Main.MouseWorld - position;
			if (direction.LengthSquared() <= 0.001f)
				direction = Vector2.UnitX * player.direction;
			else
				direction.Normalize();

			type = player.altFunctionUse == 2
				? ModContent.ProjectileType<NyrulnaConeProjectile>()
				: ModContent.ProjectileType<NyrulnaThrownProjectile>();
			velocity = direction * (player.altFunctionUse == 2 ? 1f : ThrowSpeed);
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

			if (player.altFunctionUse == 2)
			{
				Projectile.NewProjectile(
					source,
					position,
					velocity,
					type,
					Math.Max(1, damage / 4),
					18f,
					player.whoAmI,
					velocity.ToRotation());
			}
			else
			{
				Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
			}

			return false;
		}

		public override void AddRecipes()
		{
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(ItemID.TitaniumTrident);
			recipe.AddIngredient(ItemID.TitaniumBar, 10);
			recipe.AddIngredient(ItemID.SoulofMight, 5);
			recipe.AddTile(TileID.MythrilAnvil);
			recipe.Register();
		}
	}
}
