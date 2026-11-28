using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	/// <summary>
	/// Minimal example summon weapon that creates the editable SentrySkeletonProjectile.
	/// </summary>
	public class SentrySkeletonStaff : ModItem
	{
		// Reuse a vanilla staff icon until this example has a custom texture.
		public override string Texture => $"Terraria/Images/Item_{ItemID.HornetStaff}";

		public override void SetDefaults()
		{
			// Weapon stats and normal item-use animation.
			Item.width = 32;
			Item.height = 32;
			Item.damage = 18;
			Item.DamageType = DamageClass.Summon;
			Item.mana = 5;
			Item.knockBack = 2f;
			Item.useTime = 30;
			Item.useAnimation = 30;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.UseSound = SoundID.Item44;
			Item.noMelee = true;
			Item.sentry = true; // Marks this weapon as a sentry summon.
			Item.shoot = ModContent.ProjectileType<SentrySkeletonProjectile>();
			Item.shootSpeed = 0f;
			Item.value = Item.buyPrice(gold: 1);
			Item.rare = ItemRarityID.Blue;
		}

		/// <summary>
		/// Places the sentry on the ground below the cursor, spawns it on the owning client,
		/// and enforces the player's sentry limit.
		/// </summary>
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

			// Find a grounded position below the cursor, then align the projectile's bottom with the floor.
			player.FindSentryRestingSpot(type, out int worldX, out int worldY, out _);
			int halfHeight = ContentSamples.ProjectilesByType[type].height / 2;
			position = new Vector2(worldX, worldY - halfHeight);

			Projectile.NewProjectile(source, position, Vector2.Zero, type, damage, knockback, player.whoAmI);
			player.UpdateMaxTurrets(); // Enforce the player's sentry limit immediately.
			return false;
		}

		// Temporary recipe for testing; replace the Dirt Blocks when building a real weapon.
		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.DirtBlock, 10)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
