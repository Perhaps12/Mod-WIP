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
	// (Whip tags and not hurting players come from VillagerShot.cs, which covers all villager projectiles.)
	public class VillagerSkull : GlobalProjectile
	{
		public const float Speed = 5f;              // constant flight speed (px/tick); the Clothier fires it at this speed
		private const int MaxHits = 3;              // pierces: hits up to this many times in total
		private const int RehitCooldownTicks = 60;  // after hitting an enemy, can hit (and home in on) it again after this long
		private const float HomingRange = 40 * 16f; // looks for enemies within this distance
		private const float HomingInertia = 14f;    // higher = wider, lazier turns
		private const int SyncIntervalTicks = 10;   // how often the owner syncs the homing velocity in multiplayer

		public override bool InstancePerEntity => true;

		private bool fromVillager;

		// Which dust slots were in use before the vanilla skull AI ran this tick (shared, reused every call)
		private static readonly bool[] dustActiveBefore = new bool[Main.maxDust + 1];

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

		// The vanilla skull's particles drift with the camera instead of staying put in the world. So whenever
		// vanilla runs the skull's AI or drawing, note which dust already exists, then afterwards swap any new
		// particles for copies placed at the skull's real position in the world.
		private static void SnapshotDust()
		{
			for (int i = 0; i < Main.maxDust; i++)
				dustActiveBefore[i] = Main.dust[i].active;
		}

		private static void ReplaceNewDust(Projectile projectile)
		{
			System.Collections.Generic.List<Dust> created = null;
			for (int i = 0; i < Main.maxDust; i++)
			{
				Dust dust = Main.dust[i];
				if (dust.active && !dustActiveBefore[i])
					(created ??= new System.Collections.Generic.List<Dust>()).Add(dust);
			}
			if (created == null)
				return;

			// Copy each particle's look, then remove the original before spawning the replacement
			foreach (Dust original in created)
			{
				int type = original.type;
				float scale = original.scale;
				int alpha = original.alpha;
				Color color = original.color;
				bool noGravity = original.noGravity;
				bool noLight = original.noLight;
				float fadeIn = original.fadeIn;
				original.active = false;

				Dust copy = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, type,
					0f, 0f, alpha, color, scale);
				// Drift gently, slightly behind the skull
				copy.velocity = Main.rand.NextVector2Circular(0.6f, 0.6f) - projectile.velocity * 0.15f;
				copy.noGravity = noGravity;
				copy.noLight = noLight;
				copy.fadeIn = fadeIn;
				copy.customData = null;
			}
		}

		public override bool PreAI(Projectile projectile)
		{
			if (fromVillager)
				SnapshotDust();
			return true;
		}

		public override bool PreDraw(Projectile projectile, ref Color lightColor)
		{
			if (fromVillager)
				SnapshotDust();
			return true;
		}

		public override void PostDraw(Projectile projectile, Color lightColor)
		{
			if (fromVillager)
				ReplaceNewDust(projectile);
		}

		// After the vanilla skull's own movement: steer toward the nearest enemy it hasn't hit yet, then fix
		// its speed at Speed. The shooter's game does this and syncs the velocity to everyone else.
		public override void PostAI(Projectile projectile)
		{
			if (!fromVillager)
				return;

			ReplaceNewDust(projectile);

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
