using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	// The Towncall Staff Clothier's attack: a copy of the vanilla Clothier's Curse skull that homes in on
	// enemies. Its own type, so the real Clothier's skull is unchanged.
	public class VillagerHomingSkull : ModProjectile
	{
		private const int VanillaSkull = ProjectileID.ClothiersCurse;
		public const float Speed = 3f;              // constant flight speed (px/tick); the Clothier fires it at this speed
		private const int MaxHits = 3;              // pierces: hits up to this many enemies (each one once)
		private const float HomingRange = 40 * 16f; // looks for enemies within this distance
		private const float HomingInertia = 14f;    // higher = wider, lazier turns

		public override string Texture => "Terraria/Images/Projectile_" + VanillaSkull;

		public override void SetStaticDefaults()
		{
			Main.projFrames[Type] = Main.projFrames[VanillaSkull];
			ProjectileID.Sets.MinionShot[Type] = true; // a summon attack (whip tags apply)
		}

		public override void SetDefaults()
		{
			Projectile.CloneDefaults(VanillaSkull); // same size, look and behavior as the vanilla skull
			AIType = VanillaSkull;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.npcProj = false;
			Projectile.DamageType = DamageClass.Summon;

			// Pierce up to MaxHits enemies, hitting each one only once
			Projectile.penetrate = MaxHits;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
		}

		// After the vanilla skull's own movement: steer toward the nearest enemy it hasn't hit yet,
		// then fix its speed at Speed (constant, whatever the vanilla movement or the steering did)
		public override void PostAI()
		{
			NPC target = FindTarget();
			if (target != null)
			{
				Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Speed;
				Projectile.velocity = (Projectile.velocity * (HomingInertia - 1f) + desired) / HomingInertia;
			}

			Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * Speed;
		}

		private NPC FindTarget()
		{
			NPC closest = null;
			float closestDistance = HomingRange;
			foreach (NPC npc in Main.ActiveNPCs)
			{
				// Skip enemies it already hit (it can't hit them again), so after piercing it moves on
				if (!npc.CanBeChasedBy(this) || Projectile.localNPCImmunity[npc.whoAmI] != 0)
					continue;
				float distance = Vector2.Distance(npc.Center, Projectile.Center);
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
