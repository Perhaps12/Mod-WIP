using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.Projectiles
{
	public class TentacleSentryProjectile : ModProjectile
	{
		private const float Rotation = -MathHelper.Pi * 140f / 180f;
		private const int HitCooldown = 30;
		private const float GroundSpriteOffset = 8f;

		public override string Texture => $"Terraria/Images/Item_{ItemID.TentacleSpike}";

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = true;
			Projectile.sentry = true;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.timeLeft = Projectile.SentryLifeTime;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = HitCooldown;
		}

		public override void AI()
		{
			Projectile.rotation = Rotation;
			Projectile.velocity = Vector2.Zero;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = TextureAssets.Projectile[Type].Value;
			Vector2 drawPosition = Projectile.Center - Main.screenPosition + new Vector2(0f, GroundSpriteOffset);
			Main.EntitySpriteDraw(
				texture,
				drawPosition,
				null,
				lightColor,
				Projectile.rotation,
				texture.Size() / 2f,
				Projectile.scale,
				SpriteEffects.None);
			return false;
		}
	}
}
