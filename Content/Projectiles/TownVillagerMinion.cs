using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Buffs;

namespace Wprint.Content.Projectiles
{
	public class TownVillagerMinion : ModProjectile
	{
		private const int VillagerCount = 11;
		// The vanilla Resonance Scepter is not exposed as an ItemID constant in this tModLoader build.
		private const int ResonanceScepterItem = 5065;
		private const float TargetRange = 800f;
		private const float LeashRange = 1200f;
		private const float DryadAuraRadius = 112f;

		private static readonly int[] Villagers =
		{
			NPCID.Guide,
			NPCID.Demolitionist,
			NPCID.Painter,
			NPCID.Nurse,
			NPCID.PartyGirl,
			NPCID.Dryad,
			NPCID.ArmsDealer,
			NPCID.Clothier,
			NPCID.Wizard,
			NPCID.Steampunker,
			NPCID.Princess
		};

		private static readonly int[] AttackIntervals = { 48, 105, 38, 300, 82, 45, 5, 52, 42, 12, 36 };

		private ref float AttackTimer => ref Projectile.localAI[0];
		private int VillagerIndex => (int)Projectile.ai[0];
		private int VillagerType => Villagers[System.Math.Clamp(VillagerIndex, 0, VillagerCount - 1)];

		public override string Texture => $"Terraria/Images/NPC_{NPCID.Guide}";

		public override void SetStaticDefaults()
		{
			Main.projPet[Type] = true;
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
		public override bool MinionContactDamage() => false;

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (oldVelocity.X != Projectile.velocity.X)
				Projectile.velocity.X = 0f;
			if (oldVelocity.Y != Projectile.velocity.Y)
				Projectile.velocity.Y = 0f;
			return false;
		}

		public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac)
		{
			if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
				fallThrough = Main.player[Projectile.owner].Bottom.Y > Projectile.Bottom.Y + 12f;
			return true;
		}

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

			for (int i = 0; i < VillagerCount; i++)
			{
				if (!present[i])
					return i;
			}

			return -1;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!CheckActive(owner))
				return;

			NPC target = FindTarget(owner);
			if (VillagerIndex == 5)
				UpdateDryad(owner);

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
			if (owner.HasMinionAttackTargetNPC)
			{
				NPC targeted = Main.npc[owner.MinionAttackTargetNPC];
				if (targeted.CanBeChasedBy(this) && Vector2.Distance(targeted.Center, owner.Center) <= TargetRange)
					return targeted;
			}

			NPC closest = null;
			float closestDistance = TargetRange;
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

		private void Move(Player owner, NPC target)
		{
			int side = VillagerIndex % 2 == 0 ? -1 : 1;
			int row = VillagerIndex / 2;
			float homeX = owner.Center.X + side * (34f + row * 28f);

			if (Vector2.Distance(Projectile.Center, owner.Center) > 2200f)
			{
				Projectile.Center = new Vector2(homeX, owner.Bottom.Y - Projectile.height / 2f);
				Projectile.velocity = Vector2.Zero;
				Projectile.netUpdate = true;
				return;
			}

			float destinationX = homeX;
			if (target != null && VillagerIndex != 3 && VillagerIndex != 5)
			{
				destinationX = target.Center.X + side * (46f + row * 14f);
			}

			float horizontalOffset = destinationX - Projectile.Center.X;
			if (System.Math.Abs(horizontalOffset) < 8f)
				Projectile.velocity.X *= 0.75f;
			else
				Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, System.Math.Sign(horizontalOffset) * 6f, 0.18f);

			bool grounded = Projectile.velocity.Y == 0f;
			int direction = System.Math.Sign(horizontalOffset);
			bool blocked = direction != 0
				&& Collision.SolidCollision(
					Projectile.position + new Vector2(direction * 5f, -4f),
					Projectile.width,
					Projectile.height - 8);
			bool targetAbove = target != null
				&& target.Center.Y < Projectile.Center.Y - 80f
				&& System.Math.Abs(target.Center.X - Projectile.Center.X) < 180f;
			if (grounded && (blocked || owner.Bottom.Y < Projectile.Bottom.Y - 24f || targetAbove))
				Projectile.velocity.Y = -5.5f;

			Projectile.velocity.Y = MathHelper.Min(Projectile.velocity.Y + 0.3f, 10f);

			Vector2 fromOwner = Projectile.Center - owner.Center;
			float ownerDistance = fromOwner.Length();
			if (ownerDistance > LeashRange)
				Projectile.velocity.X += fromOwner.X > 0f ? -1f : 1f;
		}

		private void UpdateAttack(Player owner, NPC target)
		{
			AttackTimer = System.Math.Min(AttackTimer + 1f, AttackIntervals[VillagerIndex]);

			if (VillagerIndex == 3)
			{
				if (owner.statLife < owner.statLifeMax2 && AttackTimer >= AttackIntervals[VillagerIndex]
					&& Main.myPlayer == Projectile.owner)
				{
					AttackTimer = 0f;
					Projectile.NewProjectile(
						Projectile.GetSource_FromAI(),
						Projectile.Center,
						(owner.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 12f,
						ModContent.ProjectileType<TrackingSyringe>(),
						0,
						0f,
						Projectile.owner);
					SoundEngine.PlaySound(SoundID.Item17, Projectile.Center);
				}
				return;
			}

			if (VillagerIndex == 5)
			{
				if (AttackTimer >= AttackIntervals[VillagerIndex] && Main.myPlayer == Projectile.owner)
				{
					AttackTimer = 0f;
					Projectile.Center = owner.Center;
					Projectile.Resize((int)(DryadAuraRadius * 2f), (int)(DryadAuraRadius * 2f));
					Projectile.Damage();
					Projectile.Resize(22, 44);
				}
				return;
			}

			if (target == null || AttackTimer < AttackIntervals[VillagerIndex]
				|| Main.myPlayer != Projectile.owner
				|| !Collision.CanHitLine(Projectile.Center, 1, 1, target.Center, 1, 1))
				return;

			AttackTimer = 0f;
			FireAt(target);
		}

		private void FireAt(NPC target)
		{
			int projectileType;
			float speed;
			float spread = 0f;

			switch (VillagerIndex)
			{
				case 0:
					projectileType = ProjectileID.FireArrow;
					speed = 11f;
					break;
				case 1:
					projectileType = ProjectileID.Grenade;
					speed = 8f;
					break;
				case 2:
					projectileType = ProjectileID.PainterPaintball;
					speed = 10f;
					spread = 0.025f;
					break;
				case 4:
					projectileType = ProjectileID.PartyGirlGrenade;
					speed = 9f;
					break;
				case 6:
					projectileType = ProjectileID.Bullet;
					speed = 14f;
					spread = 0.07f;
					break;
				case 7:
					projectileType = ProjectileID.ClothiersCurse;
					speed = 10f;
					break;
				case 8:
					projectileType = ProjectileID.PurpleLaser;
					speed = 10f;
					break;
				case 9:
					projectileType = ProjectileID.BulletHighVelocity;
					speed = 16f;
					spread = 0.02f;
					break;
				default:
					projectileType = ContentSamples.ItemsByType[ResonanceScepterItem].shoot;
					speed = 12f;
					break;
			}

			Vector2 aim = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
			if (spread > 0f)
				aim = aim.RotatedBy(Main.rand.NextFloat(-spread, spread));

			int projectileIndex = Projectile.NewProjectile(
				Projectile.GetSource_FromAI(),
				Projectile.Center,
				aim * speed,
				projectileType,
				Projectile.damage,
				Projectile.knockBack,
				Projectile.owner);

			if (projectileIndex >= 0 && projectileIndex < Main.maxProjectiles)
			{
				Projectile shot = Main.projectile[projectileIndex];
				shot.friendly = true;
				shot.hostile = false;
				shot.DamageType = DamageClass.Summon;
				shot.netUpdate = true;
			}

			SoundEngine.PlaySound(VillagerIndex == 8 ? SoundID.Item20 : SoundID.Item1, Projectile.Center);
		}

		private void UpdateDryad(Player owner)
		{
			Projectile.localAI[1]++;
			if (Projectile.localAI[1] >= 30f)
			{
				Projectile.localAI[1] = 0f;
				if (Main.myPlayer == owner.whoAmI)
					owner.AddBuff(BuffID.DryadsWard, 60);
			}

			if (Main.rand.NextBool(6))
			{
				Vector2 offset = Main.rand.NextVector2CircularEdge(DryadAuraRadius, DryadAuraRadius);
				Dust dust = Dust.NewDustPerfect(owner.Center + offset, DustID.GreenTorch, Vector2.Zero, 100, default, 1.1f);
				dust.noGravity = true;
			}
		}

		private void Animate(Player owner, NPC target)
		{
			if (target != null)
				Projectile.spriteDirection = target.Center.X >= Projectile.Center.X ? 1 : -1;
			else
				Projectile.spriteDirection = owner.direction;

			Projectile.frameCounter++;
			int frameCount = System.Math.Max(1, Main.npcFrameCount[VillagerType]);
			Projectile.frame = frameCount <= 1 ? 0 : (int)(Projectile.frameCounter / 10f) % System.Math.Min(4, frameCount);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Asset<Texture2D> npcTexture = ModContent.Request<Texture2D>(
				$"Terraria/Images/NPC_{VillagerType}",
				AssetRequestMode.ImmediateLoad);
			Texture2D texture = npcTexture.Value;
			int frameCount = System.Math.Max(1, Main.npcFrameCount[VillagerType]);
			int frameHeight = texture.Height / frameCount;
			if (frameHeight < 16)
			{
				frameCount = 1;
				frameHeight = texture.Height;
			}

			int frameIndex = System.Math.Clamp(Projectile.frame, 0, frameCount - 1);
			Rectangle frame = new Rectangle(0, frameIndex * frameHeight, texture.Width, frameHeight);
			SpriteEffects effects = Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

			Main.EntitySpriteDraw(
				texture,
				Projectile.Center - Main.screenPosition,
				frame,
				lightColor,
				0f,
				frame.Size() / 2f,
				Projectile.scale,
				effects);
			return false;
		}
	}

	public class TrackingSyringe : ModProjectile
	{
		public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.PoisonDart}";

		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 120;
		}

		public override void AI()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
			{
				Projectile.Kill();
				return;
			}

			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead)
			{
				Projectile.Kill();
				return;
			}

			Vector2 toOwner = owner.Center - Projectile.Center;
			if (toOwner.LengthSquared() <= 20f * 20f)
			{
				if (Main.myPlayer == Projectile.owner)
					owner.Heal(40);
				Projectile.Kill();
				return;
			}

			Vector2 desiredVelocity = toOwner.SafeNormalize(Vector2.UnitY) * 13f;
			Projectile.velocity = (Projectile.velocity * 12f + desiredVelocity) / 13f;
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
		}
	}
}
