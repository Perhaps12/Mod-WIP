using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using Terraria;
using Terraria.DataStructures;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.GameContent;
using Wprint.Content.Projectiles;

namespace Wprint.Content.Items
{
	public class RandomHandgun : ModItem
	{
		private const int RollDurationTicks = 70;
		private const int FastestPreviewChangeInterval = 2;
		private const int SlowestPreviewChangeInterval = 10;
		private const int PreviewSettleDurationTicks = 12;
		private const int EquipPauseTicks = 40;
		private const int EquipNoticeDurationTicks = 60;
		private const int EmptyMagazineDisplayTicks = 10;
		private const int KnifeEquipInwardDurationTicks = 18;
		private const int KnifeEquipExplosionParticleCount = 40;
		private static readonly SoundStyle RollTickSound = new SoundStyle("Wprint/Sounds/RNGGunTick")
		{
			MaxInstances = 0
		};

		private sealed class GunProfile
		{
			public readonly string NameKey;
			public readonly int Weight;
			public readonly int Damage;
			public readonly int CritChance;
			public readonly int UseTime;
			public readonly int MagazineSize;
			public readonly float Knockback;
			public readonly SoundStyle UseSound;
			public readonly SoundStyle EquipSound;
			public readonly float ShootSpeed;
			public readonly float InaccuracyDegrees;
			// Null keeps the projectile selected by the loaded ammo.
			public readonly int? ProjectileTypeOverride;
			public readonly bool UseGoldShortswordProjectile;
			public readonly bool UsesBulletAmmo;
			public readonly int SpriteItemType;
			public readonly Color Tint;

			public GunProfile(
				string nameKey,
				int weight,
				int damage,
				int critChance,
				int useTime,
				int magazineSize,
				float knockback,
				SoundStyle useSound,
				SoundStyle equipSound,
				float shootSpeed,
				float inaccuracyDegrees,
				int? projectileTypeOverride,
				bool useGoldShortswordProjectile,
				bool usesBulletAmmo,
				int spriteItemType,
				Color tint)
			{
				NameKey = nameKey;
				Weight = weight;
				Damage = damage;
				CritChance = critChance;
				UseTime = useTime;
				MagazineSize = magazineSize;
				Knockback = knockback;
				UseSound = useSound;
				EquipSound = equipSound;
				ShootSpeed = shootSpeed;
				InaccuracyDegrees = inaccuracyDegrees;
				ProjectileTypeOverride = projectileTypeOverride;
				UseGoldShortswordProjectile = useGoldShortswordProjectile;
				UsesBulletAmmo = usesBulletAmmo;
				SpriteItemType = spriteItemType;
				Tint = tint;
			}
		}

		private static readonly GunProfile[] Profiles =
		// 37 23 20 17 3 are the weights.
		{
			new GunProfile(
				nameKey: "Glock", weight: 30, damage: 35, critChance: 0, useTime: 13, magazineSize: 8,
				knockback: 3f, useSound: new SoundStyle("Wprint/Sounds/GlockShot"), equipSound: new SoundStyle("Wprint/Sounds/GlockEquip"), shootSpeed: 12f,
				inaccuracyDegrees: 0f, projectileTypeOverride: null, useGoldShortswordProjectile: false,
				usesBulletAmmo: true, spriteItemType: ItemID.Handgun, tint: new Color(75, 105, 255)),
			new GunProfile(
				nameKey: "UMP", weight: 25, damage: 28, critChance: 8, useTime: 4, magazineSize: 24,
				knockback: 2f, useSound: new SoundStyle("Wprint/Sounds/UMPShot"), equipSound: new SoundStyle("Wprint/Sounds/UMPEquip"), shootSpeed: 12f,
				inaccuracyDegrees: 8f, projectileTypeOverride: null, useGoldShortswordProjectile: false,
				usesBulletAmmo: true, spriteItemType: ItemID.Uzi, tint: new Color(136, 71, 255)),
			new GunProfile(
				nameKey: "AWP", weight: 23, damage: 460, critChance: 40, useTime: 90, magazineSize: 3,
				knockback: 10f, useSound: new SoundStyle("Wprint/Sounds/AWPShot"), equipSound: new SoundStyle("Wprint/Sounds/AWPEquip"), shootSpeed: 100f,
				inaccuracyDegrees: 0f, projectileTypeOverride: ProjectileID.BulletHighVelocity,
				useGoldShortswordProjectile: false, usesBulletAmmo: true, spriteItemType: ItemID.Musket, tint: new Color(211, 44, 230)),
			new GunProfile(
				nameKey: "AK-47", weight: 19, damage: 101, critChance: 2, useTime: 6, magazineSize: 24,
				knockback: 4f, useSound: new SoundStyle("Wprint/Sounds/AK47Shot"), equipSound: new SoundStyle("Wprint/Sounds/AK47Equip"), shootSpeed: 18f,
				inaccuracyDegrees: 2f, projectileTypeOverride: null, useGoldShortswordProjectile: false,
				usesBulletAmmo: true, spriteItemType: ItemID.ClockworkAssaultRifle, tint: new Color(235, 75, 75)),
			new GunProfile(
				nameKey: "Knife", weight: 3, damage: 2500, critChance: 0, useTime: 5, magazineSize: 1,
				knockback: 20f, useSound: SoundID.Item11, equipSound: new SoundStyle("Wprint/Sounds/KnifeEquip"), shootSpeed: 30f,
				inaccuracyDegrees: 0f, projectileTypeOverride: null, useGoldShortswordProjectile: true,
				usesBulletAmmo: false, spriteItemType: ItemID.Handgun, tint: new Color(228, 174, 51))
		};

		private int profileIndex = -1;
		private int shotsRemaining;
		private int rollTicksRemaining;
		private int rollResultIndex = -1;
		private int previewProfileIndex = -1;
		private int previewTicksUntilChange;
		private int equipPauseTicksRemaining;
		private int equipNoticeTicksRemaining;
		private int knifeEquipEffectTicks;
		private SlotId? equipSoundSlot;
		private ulong lastRollUpdateTick = ulong.MaxValue;

		public bool IsRolling => rollTicksRemaining > 0;
		public bool IsShowingEquipNotice => equipNoticeTicksRemaining > 0 || IsEquipSoundPlaying();
		public string RollPreviewName => GetProfileName(previewProfileIndex);
		public Color RollPreviewTint => previewProfileIndex >= 0 ? Profiles[previewProfileIndex].Tint : Color.White;
		public string EquippedProfileName => GetProfileName(profileIndex);
		public Color EquippedProfileTint => profileIndex >= 0 ? Profiles[profileIndex].Tint : Color.White;

		public override string Texture => $"Terraria/Images/Item_{ItemID.Handgun}";

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.DamageType = DamageClass.Ranged;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.value = Item.buyPrice(gold: 1);
			Item.rare = ItemRarityID.Orange;
			Item.autoReuse = true;
			Item.noMelee = true;
			Item.useAmmo = AmmoID.Bullet;
			Item.shoot = ProjectileID.Bullet;
			ApplyProfile(Profiles[0]);
		}

		public override bool CanUseItem(Player player)
		{
			if (IsRolling || equipPauseTicksRemaining > 0 || IsEquipSoundPlaying())
			{
				return false;
			}

			if (profileIndex < 0 || shotsRemaining <= 0)
			{
				SelectProfile();
			}

			return true;
		}

		public override bool Shoot(
			Player player,
			Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source,
			Vector2 position,
			Vector2 velocity,
			int type,
			int damage,
			float knockback)
		{
			if (player.whoAmI == Main.myPlayer)
			{
				shotsRemaining--;
				if (shotsRemaining <= 0)
				{
					StartRoll(player);
					player.itemAnimation = EmptyMagazineDisplayTicks;
					player.itemTime = EmptyMagazineDisplayTicks;
				}
			}

			return true;
		}

		public override void ModifyShootStats(
			Player player,
			ref Vector2 position,
			ref Vector2 velocity,
			ref int type,
			ref int damage,
			ref float knockback)
		{
			GunProfile profile = Profiles[profileIndex];
			if (profile.UseGoldShortswordProjectile)
			{
				type = ModContent.ProjectileType<ThrownGoldShortsword>();
			}
			else if (profile.ProjectileTypeOverride.HasValue)
			{
				type = profile.ProjectileTypeOverride.Value;
			}

			float spreadRadians = MathHelper.ToRadians(profile.InaccuracyDegrees);
			if (spreadRadians > 0f)
			{
				velocity = velocity.RotatedBy(Main.rand.NextFloat(-spreadRadians, spreadRadians));
			}
		}

		public override void UpdateInventory(Player player)
		{
			AdvanceRoll(player);
		}

		public override void HoldItem(Player player)
		{
			AdvanceRoll(player);

			if (player.whoAmI == Main.myPlayer
				&& Main.netMode != NetmodeID.Server
				&& profileIndex >= 0
				&& shotsRemaining > 0
				&& Profiles[profileIndex].UseGoldShortswordProjectile
				&& Main.rand.NextBool(3))
			{
				int dustIndex = Dust.NewDust(
					player.position,
					player.width,
					player.height,
					DustID.GoldFlame,
					-player.velocity.X * 0.2f,
					-player.velocity.Y * 0.2f,
					100,
					Color.Gold,
					1f);
				Main.dust[dustIndex].noGravity = true;
			}
		}

		public override void SaveData(TagCompound tag)
		{
			tag["randomHandgunPrefix"] = Item.prefix;
			tag["profileIndex"] = profileIndex;
			tag["shotsRemaining"] = shotsRemaining;
			tag["rollTicksRemaining"] = rollTicksRemaining;
			tag["rollResultIndex"] = rollResultIndex;
			tag["previewProfileIndex"] = previewProfileIndex;
			tag["previewTicksUntilChange"] = previewTicksUntilChange;
			tag["equipPauseTicksRemaining"] = equipPauseTicksRemaining;
			tag["equipNoticeTicksRemaining"] = equipNoticeTicksRemaining;
		}

		public override void LoadData(TagCompound tag)
		{
			if (tag.ContainsKey("randomHandgunPrefix"))
			{
				Item.prefix = tag.GetInt("randomHandgunPrefix");
			}

			profileIndex = tag.GetInt("profileIndex");
			shotsRemaining = tag.GetInt("shotsRemaining");
			rollTicksRemaining = tag.ContainsKey("rollTicksRemaining") ? tag.GetInt("rollTicksRemaining") : 0;
			rollResultIndex = tag.ContainsKey("rollResultIndex") ? tag.GetInt("rollResultIndex") : -1;
			previewProfileIndex = tag.ContainsKey("previewProfileIndex") ? tag.GetInt("previewProfileIndex") : -1;
			previewTicksUntilChange = tag.ContainsKey("previewTicksUntilChange")
				? tag.GetInt("previewTicksUntilChange")
				: FastestPreviewChangeInterval;
			equipPauseTicksRemaining = tag.ContainsKey("equipPauseTicksRemaining") ? tag.GetInt("equipPauseTicksRemaining") : 0;
			equipNoticeTicksRemaining = tag.ContainsKey("equipNoticeTicksRemaining") ? tag.GetInt("equipNoticeTicksRemaining") : 0;

			if (profileIndex < 0 || profileIndex >= Profiles.Length
				|| shotsRemaining < 0 || shotsRemaining > Profiles[profileIndex].MagazineSize
				|| rollTicksRemaining < 0 || rollTicksRemaining > RollDurationTicks
				|| (rollTicksRemaining > 0 && (rollResultIndex < 0 || rollResultIndex >= Profiles.Length))
				|| (previewProfileIndex < -1 || previewProfileIndex >= Profiles.Length)
				|| previewTicksUntilChange < 0 || previewTicksUntilChange > SlowestPreviewChangeInterval
				|| equipPauseTicksRemaining < 0 || equipPauseTicksRemaining > EquipPauseTicks
				|| equipNoticeTicksRemaining < 0 || equipNoticeTicksRemaining > EquipNoticeDurationTicks)
			{
				profileIndex = -1;
				shotsRemaining = 0;
				rollTicksRemaining = 0;
				rollResultIndex = -1;
				previewProfileIndex = -1;
				previewTicksUntilChange = 0;
				equipPauseTicksRemaining = 0;
				equipNoticeTicksRemaining = 0;
			}

			if (profileIndex >= 0)
			{
				ApplyProfile();
			}
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write((sbyte)profileIndex);
			writer.Write((byte)shotsRemaining);
			writer.Write((byte)rollTicksRemaining);
			writer.Write((sbyte)rollResultIndex);
			writer.Write((sbyte)previewProfileIndex);
			writer.Write((byte)previewTicksUntilChange);
			writer.Write((byte)equipPauseTicksRemaining);
			writer.Write((byte)equipNoticeTicksRemaining);
		}

		public override void NetReceive(BinaryReader reader)
		{
			profileIndex = reader.ReadSByte();
			shotsRemaining = reader.ReadByte();
			rollTicksRemaining = reader.ReadByte();
			rollResultIndex = reader.ReadSByte();
			previewProfileIndex = reader.ReadSByte();
			previewTicksUntilChange = reader.ReadByte();
			equipPauseTicksRemaining = reader.ReadByte();
			equipNoticeTicksRemaining = reader.ReadByte();

			if (profileIndex < 0 || profileIndex >= Profiles.Length
				|| shotsRemaining > Profiles[profileIndex].MagazineSize
				|| rollTicksRemaining > RollDurationTicks
				|| (rollTicksRemaining > 0 && (rollResultIndex < 0 || rollResultIndex >= Profiles.Length))
				|| (previewProfileIndex < -1 || previewProfileIndex >= Profiles.Length)
				|| previewTicksUntilChange > SlowestPreviewChangeInterval
				|| equipPauseTicksRemaining > EquipPauseTicks
				|| equipNoticeTicksRemaining > EquipNoticeDurationTicks)
			{
				profileIndex = -1;
				shotsRemaining = 0;
				rollTicksRemaining = 0;
				rollResultIndex = -1;
				previewProfileIndex = -1;
				previewTicksUntilChange = 0;
				equipPauseTicksRemaining = 0;
				equipNoticeTicksRemaining = 0;
			}

			if (profileIndex >= 0)
			{
				ApplyProfile();
			}
		}

		public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
		{
			if (IsRolling)
			{
				string previewName = GetProfileName(previewProfileIndex);
				tooltips.Add(new TooltipLine(
					Mod,
					"Rolling",
					Language.GetTextValue("Mods.Wprint.Items.RandomHandgun.Rolling", previewName)));
				return;
			}

			if (profileIndex < 0)
			{
				return;
			}

			string profileName = Language.GetTextValue($"Mods.Wprint.Items.RandomHandgun.{Profiles[profileIndex].NameKey}");
			string status = Language.GetTextValue(
				"Mods.Wprint.Items.RandomHandgun.CurrentWeapon",
				profileName,
				shotsRemaining);
			tooltips.Add(new TooltipLine(Mod, "CurrentWeapon", status));
		}

		public override bool PreDrawInInventory(
			SpriteBatch spriteBatch,
			Vector2 position,
			Rectangle frame,
			Color drawColor,
			Color itemColor,
			Vector2 origin,
			float scale)
		{
			Color tint = profileIndex >= 0 ? Profiles[profileIndex].Tint : Color.White;
			Color color = Color.Lerp(drawColor, tint, 0.45f);
			Main.GetItemDrawFrame(GetProfileSpriteItemType(), out Texture2D texture, out Rectangle spriteFrame);
			float textureScale = scale * Math.Min(1f, 32f / Math.Max(spriteFrame.Width, spriteFrame.Height));
			spriteBatch.Draw(
				texture,
				position,
				spriteFrame,
				color,
				0f,
				spriteFrame.Size() / 2f,
				textureScale,
				SpriteEffects.None,
				0f);
			return false;
		}

		public override bool ModifyItemDraw(
			ref PlayerDrawSet drawInfo,
			ref DrawData drawData,
			ref DrawData? itemColorDrawData,
			ref DrawData? itemGlowDrawData)
		{
			if (profileIndex >= 0
				&& shotsRemaining <= 0
				&& (!IsRolling || rollTicksRemaining <= RollDurationTicks - EmptyMagazineDisplayTicks))
			{
				return false;
			}

			int spriteItemType = GetProfileSpriteItemType();
			Main.GetItemDrawFrame(spriteItemType, out Texture2D texture, out Rectangle frame);
			drawData.texture = texture;
			drawData.sourceRect = frame;

			if (itemColorDrawData.HasValue)
			{
				DrawData itemColorData = itemColorDrawData.Value;
				itemColorData.texture = texture;
				itemColorData.sourceRect = frame;
				itemColorDrawData = itemColorData;
			}

			return true;
		}

		public override Color? GetAlpha(Color lightColor)
		{
			return profileIndex >= 0
				? Color.Lerp(lightColor, Profiles[profileIndex].Tint, 0.45f)
				: null;
		}

		private void SelectProfile()
		{
			profileIndex = RollProfile(profileIndex);
			shotsRemaining = Profiles[profileIndex].MagazineSize;
			ApplyProfile();
		}

		private void StartRoll(Player player)
		{
			rollResultIndex = RollProfile(profileIndex);
			previewProfileIndex = RollNextPreviewProfile();
			previewTicksUntilChange = FastestPreviewChangeInterval;
			rollTicksRemaining = RollDurationTicks;
			lastRollUpdateTick = ulong.MaxValue;
			PlayRollTickSound(player);
		}

		private void AdvanceRoll(Player player)
		{
			if (lastRollUpdateTick == Main.GameUpdateCount)
			{
				return;
			}

			lastRollUpdateTick = Main.GameUpdateCount;

			if (IsRolling)
			{
				rollTicksRemaining--;
				int previousPreviewProfileIndex = previewProfileIndex;

				if (rollTicksRemaining <= PreviewSettleDurationTicks)
				{
					previewProfileIndex = rollResultIndex;
				}
				else
				{
					previewTicksUntilChange--;
					if (previewTicksUntilChange <= 0)
					{
						previewProfileIndex = RollNextPreviewProfile();
						float progress = 1f - rollTicksRemaining / (float)RollDurationTicks;
						previewTicksUntilChange = (int)Math.Round(
							FastestPreviewChangeInterval
							+ (SlowestPreviewChangeInterval - FastestPreviewChangeInterval) * progress);
					}
				}

				if (previewProfileIndex != previousPreviewProfileIndex)
				{
					PlayRollTickSound(player);
				}

				if (rollTicksRemaining == 0)
				{
					profileIndex = rollResultIndex;
					shotsRemaining = Profiles[profileIndex].MagazineSize;
					rollResultIndex = -1;
					previewProfileIndex = -1;
					previewTicksUntilChange = 0;
					equipPauseTicksRemaining = EquipPauseTicks;
					equipNoticeTicksRemaining = EquipPauseTicks;
					ApplyProfile();

					if (player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server)
					{
						equipSoundSlot = SoundEngine.PlaySound(Profiles[profileIndex].EquipSound, player.Center);
					}
				}
			}
			else
			{
				if (equipPauseTicksRemaining > 0)
				{
					equipPauseTicksRemaining--;
				}

				if (equipNoticeTicksRemaining > 0)
				{
					equipNoticeTicksRemaining--;
				}
			}

			UpdateKnifeEquipEffect(player);
		}

		private static void PlayRollTickSound(Player player)
		{
			if (player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server)
			{
				SoundEngine.PlaySound(RollTickSound, player.Center);
			}
		}

		private bool IsEquipSoundPlaying()
		{
			if (!equipSoundSlot.HasValue)
			{
				return false;
			}

			if (SoundEngine.TryGetActiveSound(equipSoundSlot.Value, out _))
			{
				return true;
			}

			equipSoundSlot = null;
			return false;
		}

		private void UpdateKnifeEquipEffect(Player player)
		{
			if (player.whoAmI != Main.myPlayer
				|| Main.netMode == NetmodeID.Server
				|| profileIndex < 0
				|| !Profiles[profileIndex].UseGoldShortswordProjectile
				|| shotsRemaining <= 0)
			{
				knifeEquipEffectTicks = 0;
				return;
			}

			int effectTick = knifeEquipEffectTicks++;
			Vector2 center = player.Center;
			Lighting.AddLight(center, 0.9f, 0.65f, 0.2f);

			if (effectTick == 0)
			{
				SpawnInwardKnifeFlames(center, 96f, 24, 5f);
			}
			else if (effectTick < KnifeEquipInwardDurationTicks && effectTick % 3 == 0)
			{
				float progress = effectTick / (float)KnifeEquipInwardDurationTicks;
				float radius = MathHelper.Lerp(88f, 28f, progress);
				SpawnInwardKnifeFlames(center, radius, 6, 3f + progress * 2f);
			}
			else if (effectTick == KnifeEquipInwardDurationTicks)
			{
				SpawnKnifeFlameExplosion(center);
			}
			else if (effectTick > KnifeEquipInwardDurationTicks && effectTick % 5 == 0)
			{
				SpawnOutwardKnifeFlames(center, 5);
			}
		}

		private static void SpawnInwardKnifeFlames(Vector2 center, float radius, int count, float speed)
		{
			for (int i = 0; i < count; i++)
			{
				float angle = MathHelper.TwoPi * i / count;
				Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
				Vector2 tangent = new Vector2(-direction.Y, direction.X);
				Vector2 position = center + direction * radius;
				Vector2 velocity = -direction * speed + tangent * 1.5f;
				SpawnKnifeFlame(position, velocity, 1.3f);
			}
		}

		private static void SpawnKnifeFlameExplosion(Vector2 center)
		{
			for (int i = 0; i < KnifeEquipExplosionParticleCount; i++)
			{
				float angle = MathHelper.TwoPi * i / KnifeEquipExplosionParticleCount
					+ Main.rand.NextFloat(-0.08f, 0.08f);
				Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
				Vector2 velocity = direction * Main.rand.NextFloat(5f, 10f);
				SpawnKnifeFlame(center + direction * 4f, velocity, Main.rand.NextFloat(1.1f, 1.7f));
			}
		}

		private static void SpawnOutwardKnifeFlames(Vector2 center, int count)
		{
			for (int i = 0; i < count; i++)
			{
				float angle = Main.rand.NextFloat(MathHelper.TwoPi);
				Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
				Vector2 velocity = direction * Main.rand.NextFloat(2f, 6f);
				SpawnKnifeFlame(center + direction * Main.rand.NextFloat(0f, 12f), velocity, 1.1f);
			}
		}

		private static void SpawnKnifeFlame(Vector2 position, Vector2 velocity, float scale)
		{
			int dustIndex = Dust.NewDust(
				position,
				1,
				1,
				DustID.GoldFlame,
				velocity.X,
				velocity.Y,
				100,
				Color.Gold,
				scale);
			Dust dust = Main.dust[dustIndex];
			dust.position = position;
			dust.velocity = velocity;
			dust.noGravity = true;
		}

		private int RollNextPreviewProfile()
		{
			int nextProfileIndex;
			do
			{
				nextProfileIndex = Main.rand.Next(Profiles.Length);
			}
			while (nextProfileIndex == previewProfileIndex || nextProfileIndex == rollResultIndex);

			return nextProfileIndex;
		}

		private static int RollProfile(int excludedProfileIndex)
		{
			int totalWeight = 0;
			for (int i = 0; i < Profiles.Length; i++)
			{
				if (i != excludedProfileIndex)
				{
					totalWeight += Profiles[i].Weight;
				}
			}

			int roll = Main.rand.Next(totalWeight);
			for (int i = 0; i < Profiles.Length; i++)
			{
				if (i == excludedProfileIndex)
				{
					continue;
				}

				roll -= Profiles[i].Weight;
				if (roll < 0)
				{
					return i;
				}
			}

			throw new InvalidOperationException("No random handgun profile could be selected.");
		}

		private static string GetProfileName(int index)
		{
			return index >= 0 && index < Profiles.Length
				? Language.GetTextValue($"Mods.Wprint.Items.RandomHandgun.{Profiles[index].NameKey}")
				: string.Empty;
		}

		private void ApplyProfile()
		{
			ApplyProfile(Profiles[profileIndex]);
		}

		private void ApplyProfile(GunProfile profile)
		{
			int prefix = Item.prefix;
			Item.damage = profile.Damage;
			Item.crit = profile.CritChance;
			Item.useTime = profile.UseTime;
			Item.useAnimation = profile.UseTime;
			Item.knockBack = profile.Knockback;
			Item.value = Item.buyPrice(gold: 1);
			Item.scale = 1f;
			Item.UseSound = profile.UseSound;
			Item.shootSpeed = profile.ShootSpeed;
			Item.useAmmo = profile.UsesBulletAmmo ? AmmoID.Bullet : AmmoID.None;

			if (prefix > 0)
			{
				ApplyPrefixModifiers(prefix, profile);
			}
		}

		private void ApplyPrefixModifiers(int prefix, GunProfile profile)
		{
			const int ReferenceStat = 100;
			Item prefixTemplate = Item.Clone();
			prefixTemplate.prefix = 0;
			prefixTemplate.damage = ReferenceStat;
			prefixTemplate.crit = ReferenceStat;
			prefixTemplate.useTime = ReferenceStat;
			prefixTemplate.useAnimation = ReferenceStat;
			prefixTemplate.knockBack = ReferenceStat;
			prefixTemplate.shootSpeed = ReferenceStat;
			prefixTemplate.scale = 1f;
			prefixTemplate.value = Item.buyPrice(gold: 1);

			if (!prefixTemplate.Prefix(prefix))
			{
				throw new InvalidOperationException($"Could not reapply prefix {prefix} to the random handgun profile.");
			}

			Item.damage = Math.Max(1, (int)(profile.Damage * (prefixTemplate.damage / (float)ReferenceStat)));
			Item.crit = profile.CritChance + prefixTemplate.crit - ReferenceStat;
			Item.useTime = Math.Max(1, (int)(profile.UseTime * (prefixTemplate.useTime / (float)ReferenceStat)));
			Item.useAnimation = Math.Max(1, (int)(profile.UseTime * (prefixTemplate.useAnimation / (float)ReferenceStat)));
			Item.knockBack = profile.Knockback * (prefixTemplate.knockBack / ReferenceStat);
			Item.shootSpeed = profile.ShootSpeed * (prefixTemplate.shootSpeed / ReferenceStat);
			Item.scale = prefixTemplate.scale;
			Item.value = prefixTemplate.value;
		}

		private int GetProfileSpriteItemType()
		{
			return profileIndex >= 0
				? Profiles[profileIndex].SpriteItemType
				: ItemID.Handgun;
		}

		public override void AddRecipes()
		{
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(ItemID.ChestLock, 1);
			recipe.AddTile(TileID.MythrilAnvil);
			recipe.Register();
		}
	}
}
