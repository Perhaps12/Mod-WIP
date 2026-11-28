using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Players;
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
			Item.useTime = 90;
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

		public override bool AltFunctionUse(Player player) => true;

		public override bool CanUseItem(Player player)
		{
			bool alternateUse = player.altFunctionUse == 2;
			Item.channel = !alternateUse;
			Item.shoot = alternateUse
				? ModContent.ProjectileType<EnergySwordExplosion>()
				: ModContent.ProjectileType<EnergySwordCharge>();

			return player.ownedProjectileCounts[ModContent.ProjectileType<EnergySwordCharge>()] == 0
				&& player.ownedProjectileCounts[ModContent.ProjectileType<EnergySwordSwing>()] == 0
				&& player.ownedProjectileCounts[ModContent.ProjectileType<EnergySwordExplosion>()] == 0
				&& !player.GetModPlayer<EnergySwordPlayer>().IsDetonating;
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

			if (player.altFunctionUse == 2)
			{
				player.itemTime = Math.Max(player.itemTime, player.itemTimeMax);
				int explosionDamage = (int)Math.Min(int.MaxValue, (long)damage * EnergySwordExplosion.DamageMultiplier);
				Projectile.NewProjectile(
					source,
					player.Center - new Vector2(EnergySwordExplosion.BlastRadius),
					Vector2.Zero,
					type,
					explosionDamage,
					0f,
					player.whoAmI);
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
				player.GetAdjustedItemScale(Item),
				0f,
				player.direction);

			return false;
		}
	}
}
