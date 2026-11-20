using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	public class Laevateinn : ModItem
	{
		// Temporary icon until the item gets its own texture.
		public override string Texture => $"Terraria/Images/Item_{ItemID.WoodenSword}";

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.damage = 12;
			Item.DamageType = DamageClass.Melee;
			Item.useTime = LaevateinnPlayer.ChargeTicksPerSwing;
			Item.useAnimation = LaevateinnPlayer.ChargeTicksPerSwing;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.knockBack = 5f;
			Item.value = Item.buyPrice(silver: 10);
			Item.rare = ItemRarityID.Blue;
			Item.UseSound = SoundID.Item1;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.channel = false;
			Item.shoot = ModContent.ProjectileType<LaevateinnSwingProjectile>();
			Item.shootSpeed = 1f;
			Item.autoReuse = false;
		}

		public override bool AltFunctionUse(Player player)
		{
			return true;
		}

		public override bool CanUseItem(Player player)
		{
			if (player.altFunctionUse == 2)
			{
				Item.channel = false;
				Item.shoot = ModContent.ProjectileType<LaevateinnStabProjectile>();
				Item.shootSpeed = 1f;
			}
			else
			{
				Item.channel = false;
				Item.shoot = ModContent.ProjectileType<LaevateinnSwingProjectile>();
				Item.shootSpeed = 1f;
			}

			return true;
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
			Vector2 aim = Main.MouseWorld - player.MountedCenter;
			if (aim.LengthSquared() <= 0.001f)
			{
				aim = Vector2.UnitX * player.direction;
			}
			else
			{
				aim.Normalize();
			}

			velocity = aim;
			type = player.altFunctionUse == 2
				? ModContent.ProjectileType<LaevateinnStabProjectile>()
				: ModContent.ProjectileType<LaevateinnSwingProjectile>();
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

			LaevateinnPlayer laevateinnPlayer = player.GetModPlayer<LaevateinnPlayer>();
			if (player.altFunctionUse == 2)
			{
				Projectile.NewProjectile(
					source,
					player.MountedCenter,
					velocity,
					type,
					damage,
					knockback,
					player.whoAmI,
					laevateinnPlayer.PowerMultiplier,
					velocity.ToRotation());
				laevateinnPlayer.RequestChargeReset();
				return false;
			}

			Projectile.NewProjectile(
				source,
				player.MountedCenter,
				velocity,
				type,
				damage,
				knockback,
				player.whoAmI,
				laevateinnPlayer.PowerMultiplier,
				velocity.ToRotation());
			return false;
		}
	}
}
