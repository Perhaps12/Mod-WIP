using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Items;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Players
{
	public class EokaPistolPlayer : ModPlayer
	{
		private const float PlayerRecoil = 5f;
		private static readonly SoundStyle AttemptSound = new SoundStyle("Wprint/Sounds/RNGGunTick")
		{
			Volume = 0.55f,
			PitchVariance = 0.15f
		};
		private static readonly SoundStyle FireSound = new SoundStyle("Wprint/Sounds/GlockShot")
		{
			Volume = 0.8f,
			PitchVariance = 0.08f
		};

		private bool firing;
		private bool canStartAfterRelease = true;
		private int weaponSlot;
		private int useTime;
		private int ticksUntilAttempt;
		private int attemptsRemaining;
		private int damage;
		private float knockback;
		private Vector2 aimDirection;
		private int lockedDirection;
		private float lockedItemRotation;

		public override bool CanUseItem(Item item)
		{
			return !firing;
		}

		public override void PreUpdate()
		{
			if (Player.whoAmI != Main.myPlayer)
			{
				return;
			}

			if (!firing)
			{
				if (!Player.controlUseItem)
				{
					canStartAfterRelease = true;
				}

				return;
			}

			if (Player.dead || Player.inventory[weaponSlot].type != ModContent.ItemType<EokaPistol>())
			{
				firing = false;
				return;
			}

			Player.selectedItem = weaponSlot;
			ApplyLockedAim();
			ticksUntilAttempt--;
			if (ticksUntilAttempt > 0)
			{
				return;
			}

			if (attemptsRemaining > 0)
			{
				CreateMisfireSparks();
				SoundEngine.PlaySound(AttemptSound, Player.Center);
				attemptsRemaining--;
				ticksUntilAttempt = useTime;
				return;
			}

			Fire();
		}

		public override void PostUpdate()
		{
			if (Player.whoAmI == Main.myPlayer && firing)
			{
				Player.selectedItem = weaponSlot;
				ApplyLockedAim();
				Player.itemAnimation = System.Math.Max(Player.itemAnimation, 2);
			}
		}

		public bool TryBegin(Item item)
		{
			if (firing || !canStartAfterRelease || item.type != ModContent.ItemType<EokaPistol>())
			{
				return false;
			}

			Vector2 aim = Main.MouseWorld - Player.MountedCenter;
			aimDirection = aim.LengthSquared() <= 0.001f
				? Vector2.UnitX * Player.direction
				: Vector2.Normalize(aim);
			lockedDirection = System.Math.Abs(aimDirection.X) <= 0.001f
				? Player.direction
				: aimDirection.X > 0f ? 1 : -1;
			lockedItemRotation = MathHelper.WrapAngle(
				aimDirection.ToRotation() - (lockedDirection == -1 ? MathHelper.Pi : 0f));
			ApplyLockedAim();

			weaponSlot = Player.selectedItem;
			useTime = System.Math.Max(1, item.useTime);
			ticksUntilAttempt = useTime;
			int attemptCount = RollAttemptCount();
			attemptsRemaining = attemptCount > 0 ? attemptCount - 1 : 0;
			damage = Player.GetWeaponDamage(item);
			knockback = Player.GetWeaponKnockback(item);
			firing = true;
			canStartAfterRelease = false;

			if (attemptCount == 0)
			{
				Fire();
				return true;
			}

			CreateMisfireSparks();
			SoundEngine.PlaySound(AttemptSound, Player.Center);
			return true;
		}

		public bool IsFiring => firing;

		private void CreateMisfireSparks()
		{
			if (Main.dedServ)
			{
				return;
			}

			Vector2 sparkOrigin = Player.MountedCenter + aimDirection * EokaBlastProjectile.MuzzleOffset;
			for (int i = 0; i < 10; i++)
			{
				Dust spark = Dust.NewDustDirect(
					sparkOrigin - new Vector2(5f),
					10,
					10,
					DustID.Torch,
					aimDirection.X * 1.5f,
					aimDirection.Y * 1.5f,
					50,
					default,
					Main.rand.NextFloat(0.8f, 1.3f));
				spark.velocity += new Vector2(
					Main.rand.NextFloat(-1.5f, 1.5f),
					Main.rand.NextFloat(-1.5f, 1.5f));
				spark.noGravity = true;
			}
		}

		private void ApplyLockedAim()
		{
			Player.direction = lockedDirection;
			Player.itemRotation = lockedItemRotation;
		}

		private void Fire()
		{
			Vector2 muzzlePosition = Player.MountedCenter + aimDirection * EokaBlastProjectile.MuzzleOffset;
			SoundEngine.PlaySound(FireSound, muzzlePosition);
			Projectile.NewProjectile(
				Player.GetSource_ItemUse(Player.inventory[weaponSlot]),
				muzzlePosition,
				aimDirection,
				ModContent.ProjectileType<EokaBlastProjectile>(),
				damage,
				knockback,
				Player.whoAmI);
			Player.velocity -= aimDirection * PlayerRecoil;
			firing = false;
			Player.itemAnimation = 0;
			Player.itemTime = 0;
		}

		private static int RollAttemptCount()
		{
			if (Main.rand.Next(20) == 0)
			{
				return 0;
			}

			int roll = Main.rand.Next(100);
			if (roll < 10) return 1;
			if (roll < 30) return 2;
			if (roll < 85) return 3;
			if (roll < 92) return 4;
			if (roll < 95) return 5;
			if (roll < 97) return 6;
			if (roll < 98) return 7;
			if (roll < 99) return 8;
			return 9;
		}
	}
}
