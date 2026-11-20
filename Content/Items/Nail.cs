using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	// A Hollow Knight-style nail: swings toward the mouse, snapped to up/down/left/right
	// (down only while airborne). Uses the Arkhalis slash animation.
	public class Nail : ModItem
	{
		private int swingVariant; // toggles 0/1 each swing to alternate slash animations

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.damage = 20;
			Item.DamageType = DamageClass.Melee;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.useStyle = ItemUseStyleID.Shoot;   // no vanilla swing arc
			Item.noMelee = true;                    // the item itself deals no damage; the slash does
			Item.noUseGraphic = true;               // hide the held item; the slash projectile is drawn instead
			Item.knockBack = 4f;
			Item.value = Item.buyPrice(silver: 1);
			Item.rare = ItemRarityID.Blue;
			Item.shoot = ModContent.ProjectileType<NailSlash>();
			Item.shootSpeed = 1f;                   // must be > 0 or Shoot won't run
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = true;
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
			Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			// Aim toward the mouse, snapped to the nearest of 4 directions (up/down/left/right)
			Vector2 toMouse = Main.MouseWorld - player.Center;
			Vector2 dir;
			if (Math.Abs(toMouse.Y) > Math.Abs(toMouse.X))
			{
				if (toMouse.Y < 0)
					dir = -Vector2.UnitY;                                  // mouse above: up-slash
				else if (player.velocity.Y != 0)
					dir = Vector2.UnitY;                                   // mouse below + airborne: down-slash
				else
					dir = new Vector2(toMouse.X >= 0 ? 1 : -1, 0);         // mouse below on ground: side swing
			}
			else
			{
				dir = new Vector2(toMouse.X >= 0 ? 1 : -1, 0);             // mouse to the side
			}

			// velocity carries the direction; ai[0] = 1 marks a down-slash (used for pogo);
			// ai[1] alternates which half of the Arkhalis animation plays, for a combo feel
			swingVariant = 1 - swingVariant;
			Projectile.NewProjectile(source, player.Center, dir, type, damage, knockback,
				player.whoAmI, dir.Y > 0 ? 1 : 0, swingVariant);
			return false; // we spawned the projectile ourselves
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
