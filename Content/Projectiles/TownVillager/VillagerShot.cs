using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	// Marks every projectile fired by a Towncall villager (and anything those projectiles spawn, like
	// explosions or shrapnel). Marked projectiles:
	//   - can't hurt players (vanilla explosives such as the Demolitionist's and Party Girl's grenades normally
	//     damage their owner, which here would be the player)
	//   - get whip tag effects, like minion projectiles do
	public class VillagerShot : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public bool FromVillager; // fired by a villager, or spawned by something that was: can't hurt players
		public bool FiredByVillager; // fired directly by a villager: gets whip tags

		public static bool IsFromVillager(Projectile projectile) =>
			projectile.TryGetGlobalProjectile(out VillagerShot shot) && shot.FromVillager;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (source is not EntitySource_Parent parent || parent.Entity is not Projectile shooter)
				return;

			if (shooter.type == ModContent.ProjectileType<TownVillagerMinion>())
			{
				FromVillager = true;
				FiredByVillager = true;
			}
			else if (IsFromVillager(shooter))
			{
				// Spawned by a villager's shot (e.g. a whip effect like Dark Harvest's reaper): it still can't
				// hurt players, but doesn't get whip tags itself, or each tag effect would trigger another one
				FromVillager = true;
			}
		}

		public override bool CanHitPlayer(Projectile projectile, Player target) => !FromVillager;

		// ---- Whip tags ----
		// The game only applies whip tags (bonus damage, Firecracker explosions, etc.) to minions and to
		// projectile types marked as minion shots. The villagers fire vanilla types (grenades, arrows, ...)
		// that aren't marked, and marking the whole type would also affect the player's own grenades.
		// So the type is marked only while one of the villagers' shots is dealing damage: the game asks
		// CanDamage right before a projectile deals damage, and the mark is undone again before the next
		// projectile updates (and at the end of every tick).
		private static readonly HashSet<int> markedTypes = new();

		public override bool? CanDamage(Projectile projectile)
		{
			if (FiredByVillager && !ProjectileID.Sets.MinionShot[projectile.type])
			{
				ProjectileID.Sets.MinionShot[projectile.type] = true;
				markedTypes.Add(projectile.type);
			}
			else if (!FiredByVillager && markedTypes.Contains(projectile.type))
			{
				UnmarkTypes(); // a non-villager projectile of a temporarily marked type: undo the mark first
			}
			return null;
		}

		public override bool PreAI(Projectile projectile)
		{
			if (markedTypes.Count > 0)
				UnmarkTypes();
			return true;
		}

		public static void UnmarkTypes()
		{
			foreach (int type in markedTypes)
				ProjectileID.Sets.MinionShot[type] = false;
			markedTypes.Clear();
		}
	}

	// Makes sure no type stays marked as a minion shot past the end of a tick
	public class VillagerShotSystem : ModSystem
	{
		public override void PostUpdateProjectiles() => VillagerShot.UnmarkTypes();
	}

	// Backups for the explosives' "hurt the owner" damage, which goes through the player's side
	public class VillagerShotPlayer : ModPlayer
	{
		public override bool CanBeHitByProjectile(Projectile proj) => !VillagerShot.IsFromVillager(proj);

		// Last resort: cancel any hit whose damage source is a villager's projectile
		public override bool FreeDodge(Player.HurtInfo info)
		{
			int index = info.DamageSource.SourceProjectileLocalIndex;
			return index >= 0 && index < Main.maxProjectiles && Main.projectile[index].active
				&& VillagerShot.IsFromVillager(Main.projectile[index]);
		}
	}
}
