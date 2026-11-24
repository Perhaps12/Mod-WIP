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
	// (down only while airborne). Uses the vanilla Excalibur slash visual.
	public class Nail : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.damage = 40;
			Item.DamageType = DamageClass.Melee;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.useStyle = ItemUseStyleID.Shoot;   // no vanilla swing arc
			Item.noMelee = true;                    // the item itself deals no damage; the slash does
			Item.noUseGraphic = true;               // hide the held item; the slash projectile is drawn instead
			Item.knockBack = 6f;
			Item.value = Item.buyPrice(silver: 1);
			Item.rare = ItemRarityID.Blue;
			Item.shoot = ModContent.ProjectileType<NailSlash>();
			Item.shootSpeed = 1f;                   // must be > 0 or Shoot won't run
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = true;
			Item.scale = 0.95f;                      // slightly smaller base size; size prefixes (e.g. Legendary +15%) make up for it
		}

		// Allow sword reforges (Legendary, Large, Massive, ...). The Nail hides its held sprite, which would
		// normally limit it to Universal reforges with no size. Size reaches the slash through
		// GetAdjustedItemScale in Shoot, so it scales the hitbox, the slash and the drawn sword.
		public override bool MeleePrefix() => true;

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

			// Sweep direction (+1 = clockwise on screen), randomly picked each swing:
			//   side swing:      top -> bottom, or bottom -> top
			//   up/down-slash:   back -> front, or front -> back
			float sweep;
			if (dir.X != 0)
				sweep = dir.X * player.gravDir;
			else if (dir.Y < 0)
				sweep = player.direction;
			else
				sweep = -player.direction;
			if (Main.rand.NextBool())
				sweep = -sweep;

			// Vanilla's slash sweeps 180 degrees starting at (velocity angle + sweep * 180).
			// Rotating the velocity by sweep * 90 centers that sweep on the aim direction.
			Vector2 slashVelocity = dir.RotatedBy(sweep * MathHelper.PiOver2);
			float scale = player.GetAdjustedItemScale(Item);

			// The slash (hitbox + grey Excalibur-style visual + swinging sword);
			// ai[0] = 1 marks a down-slash, ai[1] = sweep direction, ai[2] = scale
			Projectile.NewProjectile(source, player.MountedCenter, slashVelocity, type, damage, knockback,
				player.whoAmI, dir.Y > 0 ? 1 : 0, sweep, scale);
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
