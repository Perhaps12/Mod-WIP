using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	public class EnergySword : ModItem
	{
		public override string Texture => $"Terraria/Images/Item_{ItemID.Excalibur}";

		public override void SetDefaults()
		{
			Item.width = 40;
			Item.height = 40;
			Item.damage = 42;
			Item.DamageType = DamageClass.Melee;
			Item.useTime = 36;
			Item.useAnimation = 36;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.channel = true;
			Item.knockBack = 6f;
			Item.value = Item.buyPrice(gold: 2);
			Item.rare = ItemRarityID.Orange;
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = false;
			Item.shoot = ModContent.ProjectileType<EnergySwordCharge>();
			Item.shootSpeed = 1f;
		}

		public override bool MeleePrefix() => true;

		public override bool CanUseItem(Player player)
		{
			return player.ownedProjectileCounts[ModContent.ProjectileType<EnergySwordCharge>()] == 0
				&& player.ownedProjectileCounts[ModContent.ProjectileType<EnergySwordSwing>()] == 0;
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

			Projectile.NewProjectile(
				source,
				player.MountedCenter,
				Vector2.Zero,
				type,
				damage,
				knockback,
				player.whoAmI,
				player.GetAdjustedItemScale(Item));

			return false;
		}
	}
}
