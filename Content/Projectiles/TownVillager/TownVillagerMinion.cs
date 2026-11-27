using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Buffs;

namespace Wprint.Content.Projectiles
{
	// A Town NPC minion summoned by the Towncall Staff (Content/Items/TownCallStaff.cs).
	// This class is split across several files in this folder:
	//   TownVillagerMinion.cs          - villager list, setup, summon order, AI entry point, targeting
	//   TownVillagerMinion.Movement.cs - Pygmy-style walking, jumping, flying back, line-up
	//   TownVillagerMinion.Combat.cs   - combat ranges, attack positioning, aiming and shooting
	//   TownVillagerMinion.Dryad.cs    - the Dryad's ward aura
	//   TownVillagerMinion.Drawing.cs  - animation and drawing
	//   TownVillagerMinion.AttackAnimation.cs - attack frames, weapon in hand, magic glow
	public partial class TownVillagerMinion : ModProjectile
	{
		private const int VillagerCount = 11;

		// Which villager the staff summons first (index into Villagers below: 0 = Guide, 1 = Merchant,
		// 2 = Demolitionist, ...). Later summons continue in order from there and wrap around to the start.
		public const int FirstVillager = 0;

		private const float TargetRange = 800f;        // enemies within this distance of the player get attacked (50 tiles)
		private const float MeleeTargetRange = 1200f;  // melee villagers seek out enemies farther away (75 tiles)

		// Summon order. Keep Villagers, AttackIntervals, DamageMultipliers and Ranges (TownVillagerMinion.Combat.cs)
		// in the same order.
		// Attacks are picked by NPC type in FireAt, so reordering this list doesn't mix them up.
		private static readonly int[] Villagers =
		{
			NPCID.Guide,
			NPCID.Merchant,
			NPCID.Demolitionist,
			NPCID.BestiaryGirl, // the Zoologist
			NPCID.Dryad,
			NPCID.ArmsDealer,
			NPCID.PartyGirl,
			NPCID.Clothier,
			NPCID.Wizard,
			NPCID.Steampunker,
			NPCID.Princess
		};

		// Ticks between attacks (melee villagers hit by touch instead, see MinionContactDamage;
		// for the Dryad it's how often her aura refreshes)
		private static readonly int[] AttackIntervals =
		{
			48,  // Guide
			36,  // Merchant
			90, // Demolitionist
			20,  // Zoologist (melee: unused)
			45,  // Dryad (aura refresh)
			12,   // Arms Dealer
			90,  // Party Girl
			75,  // Clothier
			20,  // Wizard
			30,  // Steampunker (pause between bursts, see BurstShots in TownVillagerMinion.Combat.cs)
			36,  // Princess
		};

		// Each villager's damage per hit, as a multiple of the Towncall Staff's damage (1.5 = 150%), so it scales
		// with the staff. (The Dryad doesn't deal damage, so hers is unused.)
		private static readonly float[] DamageMultipliers =
		{
			0.40f, // Guide
			0.40f, // Merchant
			1.50f, // Demolitionist
			0.40f, // Zoologist
			1.00f, // Dryad (unused)
			0.40f, // Arms Dealer
			5.00f, // Party Girl
			2.00f, // Clothier
			2.00f, // Wizard
			2.00f, // Steampunker
			4.00f, // Princess
		};

		// Damage for a villager summoned by a staff dealing staffDamage (used by the Towncall Staff)
		public static int DamageFor(int villagerIndex, int staffDamage) =>
			System.Math.Max(1, (int)System.Math.Round(staffDamage * DamageMultipliers[System.Math.Clamp(villagerIndex, 0, VillagerCount - 1)]));

		private ref float AttackTimer => ref Projectile.localAI[0];
		private int VillagerIndex => (int)Projectile.ai[0];
		private int VillagerType => Villagers[System.Math.Clamp(VillagerIndex, 0, VillagerCount - 1)];

		public override string Texture => $"Terraria/Images/NPC_{NPCID.Guide}";

		public override void SetStaticDefaults()
		{
			Main.projPet[Type] = true;
			// Keep drawing when the villager is just off screen, so the Dryad's big ward ring doesn't pop out
			ProjectileID.Sets.DrawScreenCheckFluff[Type] = (int)(DryadAuraRadius * 2f + 64f);
			ProjectileID.Sets.MinionTargettingFeature[Type] = true;
			ProjectileID.Sets.MinionSacrificable[Type] = true;
			ProjectileID.Sets.CultistIsResistantTo[Type] = true;
		}

		public override void SetDefaults()
		{
			Projectile.width = 22;
			Projectile.height = 44;
			Projectile.tileCollide = true;
			Projectile.friendly = true;
			Projectile.minion = true;
			Projectile.minionSlots = 1f;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.penetrate = -1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
		}

		public override bool? CanCutTiles() => false;

		// Melee villagers attack by touching enemies (once per localNPCHitCooldown ticks per enemy);
		// everyone else attacks with projectiles
		public override bool MinionContactDamage() => Range == CombatRange.Melee;

		public static int FindNextVillager(Player player)
		{
			bool[] present = new bool[VillagerCount];
			foreach (Projectile projectile in Main.ActiveProjectiles)
			{
				if (projectile.owner == player.whoAmI && projectile.type == ModContent.ProjectileType<TownVillagerMinion>())
				{
					int index = (int)projectile.ai[0];
					if (index >= 0 && index < VillagerCount)
						present[index] = true;
				}
			}

			// Go through the villagers in order, starting from FirstVillager and wrapping around
			for (int i = 0; i < VillagerCount; i++)
			{
				int index = (FirstVillager + i) % VillagerCount;
				if (!present[index])
					return index;
			}

			return -1;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!CheckActive(owner))
				return;

			NPC target = FindTarget(owner);

			Move(owner, target);
			UpdateAttack(owner, target);
			Animate(owner, target);
		}

		private bool CheckActive(Player owner)
		{
			if (!owner.active || owner.dead)
			{
				owner.ClearBuff(ModContent.BuffType<TownCallBuff>());
				return false;
			}

			if (owner.HasBuff(ModContent.BuffType<TownCallBuff>()))
				Projectile.timeLeft = 2;

			return true;
		}

		private NPC FindTarget(Player owner)
		{
			// Melee villagers go looking for enemies farther away
			float range = Range == CombatRange.Melee ? MeleeTargetRange : TargetRange;

			if (owner.HasMinionAttackTargetNPC)
			{
				NPC targeted = Main.npc[owner.MinionAttackTargetNPC];
				if (targeted.CanBeChasedBy(this) && Vector2.Distance(targeted.Center, owner.Center) <= range)
					return targeted;
			}

			NPC closest = null;
			float closestDistance = range;
			foreach (NPC npc in Main.ActiveNPCs)
			{
				if (!npc.CanBeChasedBy(this))
					continue;

				float distance = Vector2.Distance(npc.Center, owner.Center);
				if (distance < closestDistance)
				{
					closest = npc;
					closestDistance = distance;
				}
			}

			return closest;
		}
	}
}
