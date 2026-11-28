using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Wprint.Content.Items;

namespace Wprint.Content.Projectiles
{
	public class EnergySwordCharge : ModProjectile
	{
		public const float MaxSwingScale = 2.75f;

		private const int MaxChargeTicks = 5 * 60;
		private const int ChargeSoundInterval = 30;
		private const int FullChargeShakeTicks = 2 * 60;

		public override string Texture => $"Terraria/Images/Item_{ItemID.Excalibur}";

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 2;
		}

		public override bool? CanDamage() => false;

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			if (!owner.active || owner.dead || owner.HeldItem.type != ModContent.ItemType<EnergySword>())
			{
				Projectile.Kill();
				return;
			}

			if (owner.channel)
			{
				Projectile.timeLeft = 2;
				if (owner.whoAmI == Main.myPlayer)
				{
					owner.ChangeDir(Main.MouseWorld.X >= owner.MountedCenter.X ? 1 : -1);
				}

				int facingDirection = owner.direction;
				if (Projectile.ai[2] != facingDirection)
				{
					Projectile.ai[2] = facingDirection;
					Projectile.netUpdate = true;
				}

				if (Projectile.ai[1] < MaxChargeTicks)
				{
					Projectile.ai[1]++;
					if (Projectile.ai[1] >= MaxChargeTicks)
					{
						if (Projectile.localAI[0] == 0f)
						{
							Projectile.localAI[0] = 1f;
							if (!Main.dedServ)
							{
								SoundEngine.PlaySound(
									SoundID.Item4 with { Volume = 0.8f, Pitch = 0.5f },
									owner.Center);
							}
						}
					}
					else if (Projectile.ai[1] % ChargeSoundInterval == 0f && !Main.dedServ)
					{
						float pitch = MathHelper.Lerp(-0.3f, 0.35f, Projectile.ai[1] / MaxChargeTicks);
						SoundEngine.PlaySound(
							SoundID.Item4 with { Volume = 0.35f, Pitch = pitch },
							owner.Center);
					}
				}
				else
				{
					Projectile.localAI[1]++;
				}

				Projectile.Center = owner.MountedCenter
					+ new Vector2(facingDirection * 12f, -14f);
				float shakeProgress = MathHelper.Clamp(Projectile.localAI[1] / FullChargeShakeTicks, 0f, 1f);
				float shake = Projectile.localAI[1] > 0f ? shakeProgress : 0f;
				Projectile.Center += new Vector2(
					(float)Math.Sin(Projectile.localAI[1] * 1.8f) * 3f * shake,
					(float)Math.Cos(Projectile.localAI[1] * 2.3f) * 2f * shake);
				Projectile.rotation = -MathHelper.PiOver4
					+ (float)Math.Sin(Projectile.localAI[1] * 1.7f) * 0.12f * shake;
				owner.heldProj = Projectile.whoAmI;
				owner.itemTime = 2;
				owner.itemAnimation = 2;
				owner.SetCompositeArmFront(
					true,
					Player.CompositeArmStretchAmount.Full,
					Projectile.rotation - MathHelper.PiOver2);

				if (!Main.dedServ)
				{
					float charge = Projectile.ai[1] / MaxChargeTicks;
					float lightStrength = 0.035f + 1.1f * charge * charge;
					Vector2 lightPosition = Projectile.Center
						+ Projectile.rotation.ToRotationVector2() * (34f + 32f * charge);
					Lighting.AddLight(
						lightPosition,
						0.7f * lightStrength,
						0.48f * lightStrength,
						0.12f * lightStrength);
				}

				if (Projectile.ai[1] < MaxChargeTicks || Projectile.localAI[1] < FullChargeShakeTicks)
				{
					return;
				}
			}

			if (Projectile.localAI[2] != 0f)
			{
				Projectile.Kill();
				return;
			}

			Projectile.localAI[2] = 1f;
			float chargeFraction = Projectile.ai[1] / MaxChargeTicks;
			float swingScale = 0.5f + (MaxSwingScale - 0.5f) * (float)Math.Sqrt(chargeFraction);
			float damageScale = 1f + chargeFraction;
			float sweepDirection = Projectile.ai[2] >= 0f ? 1f : -1f;
			Vector2 swingDirection = new Vector2(sweepDirection, 0f);
			owner.itemTime = Math.Max(owner.itemTime, owner.itemTimeMax);

			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				Projectile.NewProjectile(
					Projectile.GetSource_FromThis(),
					owner.MountedCenter,
					swingDirection,
					ModContent.ProjectileType<EnergySwordSwing>(),
					(int)Math.Round(Projectile.damage * damageScale),
					Projectile.knockBack,
					Projectile.owner,
					sweepDirection,
					Projectile.ai[0],
					swingScale);
			}

			Projectile.Kill();
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Player owner = Main.player[Projectile.owner];
			Texture2D sword = TextureAssets.Item[ItemID.Excalibur].Value;
			int swingDirection = Projectile.ai[2] >= 0f ? 1 : -1;
			Vector2 drawPosition = owner.MountedCenter
				+ new Vector2(swingDirection * 12f, -14f)
				- Main.screenPosition;
			Vector2 origin = new Vector2(0f, sword.Height);
			float charge = MathHelper.Clamp(Projectile.ai[1] / MaxChargeTicks, 0f, 1f);
			float glow = 0.015f + 0.985f * charge * charge;
			float glowScale = 1.02f + 0.9f * charge;
			float shakeProgress = MathHelper.Clamp(Projectile.localAI[1] / FullChargeShakeTicks, 0f, 1f);
			float shake = Projectile.localAI[1] > 0f ? shakeProgress : 0f;
			drawPosition += new Vector2(
				(float)Math.Sin(Projectile.localAI[1] * 1.8f) * 3f * shake,
				(float)Math.Cos(Projectile.localAI[1] * 2.3f) * 2f * shake);
			float swordAngle = Projectile.rotation;
			Color glowColor = Color.Lerp(Color.Yellow, Color.Goldenrod, 0.25f) with { A = 0 };

			Main.EntitySpriteDraw(
				sword,
				drawPosition,
				null,
				glowColor * glow,
				swordAngle,
				origin,
				Projectile.ai[0] * glowScale,
				SpriteEffects.None);
			Main.EntitySpriteDraw(
				sword,
				drawPosition,
				null,
				lightColor,
				swordAngle,
				origin,
				Projectile.ai[0],
				SpriteEffects.None);
			return false;
		}
	}
}
