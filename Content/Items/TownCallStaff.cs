using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Buffs;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	public class TownCallStaff : ModItem
	{
		public override string Texture => $"Terraria/Images/Item_{ItemID.HornetStaff}";

		public override void SetStaticDefaults()
		{
			ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
			ItemID.Sets.LockOnIgnoresCollision[Type] = true;
			ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;
		}

		public override void SetDefaults()
		{
			Item.damage = 22;
			Item.knockBack = 2f;
			Item.mana = 10;
			Item.width = 32;
			Item.height = 32;
			Item.useTime = 30;
			Item.useAnimation = 30;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.value = Item.buyPrice(gold: 10);
			Item.rare = ItemRarityID.Yellow;
			Item.UseSound = SoundID.Item44;
			Item.noMelee = true;
			Item.DamageType = DamageClass.Summon;
			Item.buffType = ModContent.BuffType<TownCallBuff>();
			Item.shoot = ModContent.ProjectileType<TownVillagerMinion>();
		}

		public override bool CanUseItem(Player player)
		{
			return TownVillagerMinion.FindNextVillager(player) >= 0
				&& player.slotsMinions + 1f <= player.maxMinions;
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

			int villager = TownVillagerMinion.FindNextVillager(player);
			if (villager < 0 || player.slotsMinions + 1f > player.maxMinions)
				return false;

			player.AddBuff(Item.buffType, 2);
			Projectile minion = Projectile.NewProjectileDirect(
				source,
				player.Center,
				Vector2.Zero,
				type,
				damage + villager * 5,
				knockback,
				player.whoAmI,
				villager);
			minion.originalDamage = minion.damage;
			return false;
		}

		public override void AddRecipes()
		{
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(ItemID.HallowedBar, 10);
			recipe.AddIngredient(ItemID.SoulofMight, 10);
			recipe.AddIngredient(ItemID.SoulofLight, 10);
			recipe.AddTile(TileID.MythrilAnvil);
			recipe.Register();
		}
	}
}
