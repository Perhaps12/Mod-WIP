using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	// The Towncall Staff Clothier fires the real vanilla Clothier's Curse skull, so it keeps all of the vanilla
	// visuals (vanilla draws and animates projectiles by type, which a copy under a new type wouldn't get).
	// This adds, only to skulls fired by a villager (the real Clothier's skulls are untouched):
	//   - homing on enemies, at a constant speed
	//   - piercing up to MaxHits hits; it can hit (and home back in on) the same enemy again after RehitCooldownTicks
	// Note: whip tag damage doesn't apply, since that's set per projectile type and the type is vanilla's.
	public class VillagerSkull : GlobalProjectile
	{
		public const float Speed = 3f;              // constant flight speed (px/tick); the Clothier fires it at this speed
		private const int MaxHits = 3;              // pierces: hits up to this many times in total
		private const int RehitCooldownTicks = 60;  // after hitting an enemy, can hit (and home in on) it again after this long
		private const float HomingRange = 40 * 16f; // looks for enemies within this distance
		private const float HomingInertia = 14f;    // higher = wider, lazier turns
		private const int SyncIntervalTicks = 10;   // how often the owner syncs the homing velocity in multiplayer

		public override bool InstancePerEntity => true;

		private bool fromVillager;

		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) =>
			entity.type == ProjectileID.ClothiersCurse;

		// Only skulls fired by a Towncall villager get the extras (runs on the shooter's game)
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (source is EntitySource_Parent parent && parent.Entity is Projectile shooter
				&& shooter.type == ModContent.ProjectileType<TownVillagerMinion>())
			{
				fromVillager = true;
				projectile.penetrate = MaxHits;
				projectile.usesLocalNPCImmunity = true;
				projectile.localNPCHitCooldown = RehitCooldownTicks;
			}
		}

		// After the vanilla skull's own movement: steer toward the nearest enemy it hasn't hit yet, then fix
		// its speed at Speed. The shooter's game does this and syncs the velocity to everyone else.
		public override void PostAI(Projectile projectile)
		{
			if (!fromVillager)
				return;

			NPC target = FindTarget(projectile);
			if (target != null)
			{
				Vector2 desired = (target.Center - projectile.Center).SafeNormalize(Vector2.Zero) * Speed;
				projectile.velocity = (projectile.velocity * (HomingInertia - 1f) + desired) / HomingInertia;
			}

			projectile.velocity = projectile.velocity.SafeNormalize(Vector2.UnitX) * Speed;

			if (projectile.timeLeft % SyncIntervalTicks == 0)
				projectile.netUpdate = true;
		}

		private static NPC FindTarget(Projectile projectile)
		{
			NPC closest = null;
			float closestDistance = HomingRange;
			foreach (NPC npc in Main.ActiveNPCs)
			{
				// Skip enemies it hit within the last RehitCooldownTicks (it can't hit them yet), so after piercing
				// it moves on; once the cooldown runs out it can come back for them
				if (!npc.CanBeChasedBy(projectile) || projectile.localNPCImmunity[npc.whoAmI] != 0)
					continue;
				float distance = Vector2.Distance(npc.Center, projectile.Center);
				if (distance < closestDistance)
				{
					closestDistance = distance;
					closest = npc;
				}
			}
			return closest;
		}
	}
}
