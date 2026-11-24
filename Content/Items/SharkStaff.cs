using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Buffs;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	// Summons a shark that follows you and blows bubbles at nearby enemies, like a held-down Bubble Gun.
	public class SharkStaff : ModItem
	{
		// Placeholder: vanilla Shark Pup pet item sprite
		public override string Texture => "Terraria/Images/Item_4425";

		public override void SetStaticDefaults()
		{
			ItemID.Sets.GamepadWholeScreenUseRange[Type] = true; // controller: summon anywhere on screen
			ItemID.Sets.LockOnIgnoresCollision[Type] = true;
			ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;
		}

		public override void SetDefaults()
		{
			Item.damage = 30;                 // per bubble
			Item.knockBack = 1f;
			Item.mana = 10;
			Item.width = 32;
			Item.height = 32;
			Item.useTime = 36;
			Item.useAnimation = 36;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.value = Item.buyPrice(gold: 5);
			Item.rare = ItemRarityID.Pink;
			Item.UseSound = SoundID.Item44;   // standard summon sound
			Item.noMelee = true;
			Item.DamageType = DamageClass.Summon;
			Item.buffType = ModContent.BuffType<SharkMinionBuff>();
			Item.shoot = ModContent.ProjectileType<SharkMinion>();
		}

		public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
		{
			position = Main.MouseWorld; // summon at the cursor
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			// Only one shark at a time: summoning again replaces the old one
			foreach (Projectile p in Main.ActiveProjectiles)
			{
				if (p.owner == player.whoAmI && p.type == type)
					p.Kill();
			}

			player.AddBuff(Item.buffType, 2);
			Projectile shark = Projectile.NewProjectileDirect(source, position, Vector2.Zero, type, damage, knockback, player.whoAmI);
			shark.originalDamage = Item.damage; // minions scale from originalDamage
			return false;
		}

		public override void AddRecipes()
		{
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(ItemID.DirtBlock, 1);
			recipe.AddTile(TileID.WorkBenches);
			recipe.Register();
		}
	}
}
