using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
		private const int PreviewChangeInterval = 6;
		private const int EquipPauseTicks = 40;
		private const int EquipNoticeDurationTicks = 60;
		private const int EmptyMagazineDisplayTicks = 10;

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
				nameKey: "Glock", weight: 37, damage: 31, critChance: 8, useTime: 12, magazineSize: 8,
				knockback: 3f, useSound: SoundID.Item41, equipSound: SoundID.Item149, shootSpeed: 12f,
				inaccuracyDegrees: 0f, projectileTypeOverride: null, useGoldShortswordProjectile: false,
				usesBulletAmmo: true, spriteItemType: ItemID.Handgun, tint: Color.Blue),
			new GunProfile(
				nameKey: "UMP", weight: 23, damage: 24, critChance: 7, useTime: 6, magazineSize: 20,
				knockback: 2f, useSound: SoundID.Item11, equipSound: SoundID.Item149, shootSpeed: 12f,
				inaccuracyDegrees: 8f, projectileTypeOverride: null, useGoldShortswordProjectile: false,
				usesBulletAmmo: true, spriteItemType: ItemID.Uzi, tint: Color.Purple),
			new GunProfile(
				nameKey: "AWP", weight: 20, damage: 325, critChance: 40, useTime: 80, magazineSize: 3,
				knockback: 10f, useSound: SoundID.Item40, equipSound: SoundID.Item149, shootSpeed: 100f,
				inaccuracyDegrees: 0f, projectileTypeOverride: ProjectileID.BulletHighVelocity,
				useGoldShortswordProjectile: false, usesBulletAmmo: true, spriteItemType: ItemID.Musket, tint: Color.Pink),
			new GunProfile(
				nameKey: "AK-47", weight: 17, damage: 47, critChance: 8, useTime: 7, magazineSize: 20,
				knockback: 4f, useSound: SoundID.Item45, equipSound: SoundID.Item149, shootSpeed: 18f,
				inaccuracyDegrees: 2f, projectileTypeOverride: null, useGoldShortswordProjectile: false,
				usesBulletAmmo: true, spriteItemType: ItemID.ClockworkAssaultRifle, tint: Color.Red),
			new GunProfile(
				nameKey: "Knife", weight: 3, damage: 2000, critChance: 0, useTime: 5, magazineSize: 1,
				knockback: 20f, useSound: SoundID.Item11, equipSound: SoundID.Item149, shootSpeed: 30f,
				inaccuracyDegrees: 0f, projectileTypeOverride: null, useGoldShortswordProjectile: true,
				usesBulletAmmo: false, spriteItemType: ItemID.Handgun, tint: Color.Gold)
		};

		private int profileIndex = -1;
		private int shotsRemaining;
		private int rollTicksRemaining;
		private int rollResultIndex = -1;
		private int previewProfileIndex = -1;
		private int equipPauseTicksRemaining;
		private int equipNoticeTicksRemaining;
		private ulong lastRollUpdateTick = ulong.MaxValue;

		public bool IsRolling => rollTicksRemaining > 0;
		public bool IsShowingEquipNotice => equipNoticeTicksRemaining > 0;
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
			if (IsRolling || equipPauseTicksRemaining > 0)
			{
				return false;
			}

			if (profileIndex < 0 || shotsRemaining <= 0)
			{
				SelectProfile();
			}

			ApplyProfile();
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
					StartRoll();
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
			tag["profileIndex"] = profileIndex;
			tag["shotsRemaining"] = shotsRemaining;
			tag["rollTicksRemaining"] = rollTicksRemaining;
			tag["rollResultIndex"] = rollResultIndex;
			tag["previewProfileIndex"] = previewProfileIndex;
			tag["equipPauseTicksRemaining"] = equipPauseTicksRemaining;
			tag["equipNoticeTicksRemaining"] = equipNoticeTicksRemaining;
		}

		public override void LoadData(TagCompound tag)
		{
			profileIndex = tag.GetInt("profileIndex");
			shotsRemaining = tag.GetInt("shotsRemaining");
			rollTicksRemaining = tag.ContainsKey("rollTicksRemaining") ? tag.GetInt("rollTicksRemaining") : 0;
			rollResultIndex = tag.ContainsKey("rollResultIndex") ? tag.GetInt("rollResultIndex") : -1;
			previewProfileIndex = tag.ContainsKey("previewProfileIndex") ? tag.GetInt("previewProfileIndex") : -1;
			equipPauseTicksRemaining = tag.ContainsKey("equipPauseTicksRemaining") ? tag.GetInt("equipPauseTicksRemaining") : 0;
			equipNoticeTicksRemaining = tag.ContainsKey("equipNoticeTicksRemaining") ? tag.GetInt("equipNoticeTicksRemaining") : 0;

			if (profileIndex < 0 || profileIndex >= Profiles.Length
				|| shotsRemaining < 0 || shotsRemaining > Profiles[profileIndex].MagazineSize
				|| rollTicksRemaining < 0 || rollTicksRemaining > RollDurationTicks
				|| (rollTicksRemaining > 0 && (rollResultIndex < 0 || rollResultIndex >= Profiles.Length))
				|| (previewProfileIndex < -1 || previewProfileIndex >= Profiles.Length)
				|| equipPauseTicksRemaining < 0 || equipPauseTicksRemaining > EquipPauseTicks
				|| equipNoticeTicksRemaining < 0 || equipNoticeTicksRemaining > EquipNoticeDurationTicks)
			{
				profileIndex = -1;
				shotsRemaining = 0;
				rollTicksRemaining = 0;
				rollResultIndex = -1;
				previewProfileIndex = -1;
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
			equipPauseTicksRemaining = reader.ReadByte();
			equipNoticeTicksRemaining = reader.ReadByte();

			if (profileIndex < 0 || profileIndex >= Profiles.Length
				|| shotsRemaining > Profiles[profileIndex].MagazineSize
				|| rollTicksRemaining > RollDurationTicks
				|| (rollTicksRemaining > 0 && (rollResultIndex < 0 || rollResultIndex >= Profiles.Length))
				|| (previewProfileIndex < -1 || previewProfileIndex >= Profiles.Length)
				|| equipPauseTicksRemaining > EquipPauseTicks
				|| equipNoticeTicksRemaining > EquipNoticeDurationTicks)
			{
				profileIndex = -1;
				shotsRemaining = 0;
				rollTicksRemaining = 0;
				rollResultIndex = -1;
				previewProfileIndex = -1;
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
			Texture2D texture = GetProfileTexture();
			float textureScale = scale * Math.Min(1f, 32f / Math.Max(texture.Width, texture.Height));
			spriteBatch.Draw(
				texture,
				position,
				null,
				color,
				0f,
				texture.Size() / 2f,
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

			Texture2D texture = GetProfileTexture();
			drawData.texture = texture;
			drawData.sourceRect = null;

			if (itemColorDrawData.HasValue)
			{
				DrawData itemColorData = itemColorDrawData.Value;
				itemColorData.texture = texture;
				itemColorData.sourceRect = null;
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

		private void StartRoll()
		{
			rollResultIndex = RollProfile(profileIndex);
			previewProfileIndex = Main.rand.Next(Profiles.Length);
			rollTicksRemaining = RollDurationTicks;
			lastRollUpdateTick = ulong.MaxValue;
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

				if (rollTicksRemaining <= PreviewChangeInterval * 2)
				{
					previewProfileIndex = rollResultIndex;
				}
				else if (rollTicksRemaining % PreviewChangeInterval == 0)
				{
					previewProfileIndex = Main.rand.Next(Profiles.Length);
				}

				if (rollTicksRemaining == 0)
				{
					profileIndex = rollResultIndex;
					shotsRemaining = Profiles[profileIndex].MagazineSize;
					rollResultIndex = -1;
					previewProfileIndex = -1;
					equipPauseTicksRemaining = EquipPauseTicks;
					equipNoticeTicksRemaining = EquipNoticeDurationTicks;
					ApplyProfile();

					if (player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server)
					{
						SoundEngine.PlaySound(Profiles[profileIndex].EquipSound, player.Center);
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
			Item.damage = profile.Damage;
			Item.crit = profile.CritChance;
			Item.useTime = profile.UseTime;
			Item.useAnimation = profile.UseTime;
			Item.knockBack = profile.Knockback;
			Item.UseSound = profile.UseSound;
			Item.shootSpeed = profile.ShootSpeed;
			Item.useAmmo = profile.UsesBulletAmmo ? AmmoID.Bullet : AmmoID.None;
		}

		private Texture2D GetProfileTexture()
		{
			int spriteItemType = profileIndex >= 0
				? Profiles[profileIndex].SpriteItemType
				: ItemID.Handgun;
			return TextureAssets.Item[spriteItemType].Value;
		}
	}
}
