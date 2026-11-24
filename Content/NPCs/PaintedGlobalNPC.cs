using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Wprint.Content.NPCs
{
	// Enemies hit by a paintball are tinted the paint's color, fading back to normal over PaintTicks.
	// Purely visual. Hits happen on the shooter's game, so Paint() syncs the color to everyone else.
	public class PaintedGlobalNPC : GlobalNPC
	{
		public const int PaintTicks = 5 * 60;   // fade time (5 seconds)
		const bool OverrideColor = true;       // false = blend the paint over the enemy, true = fully replace its colors
		const float BlendStrength = 0.8f;       // how strongly the paint shows when blending (0-1), right after a hit

		public override bool InstancePerEntity => true;

		Color paintColor;
		int paintTimer;

		// Paint this enemy (or refresh/repaint it). Call from the shooter's side; it syncs itself.
		public static void Paint(NPC npc, Color color)
		{
			npc.GetGlobalNPC<PaintedGlobalNPC>().SetPaint(color);
			if (Main.netMode != NetmodeID.SinglePlayer)
				SendPaint(npc.whoAmI, color, -1, Main.myPlayer);
		}

		internal void SetPaint(Color color)
		{
			paintColor = color;
			paintTimer = PaintTicks;
		}

		// Client -> server, or server -> other clients
		internal static void SendPaint(int npcIndex, Color color, int toWho, int ignoreWho)
		{
			ModPacket packet = ModContent.GetInstance<global::Wprint.Wprint>().GetPacket();
			packet.Write((byte)global::Wprint.WprintPacketType.PaintNPC);
			packet.Write((short)npcIndex);
			packet.Write(color.R);
			packet.Write(color.G);
			packet.Write(color.B);
			packet.Send(toWho, ignoreWho);
		}

		public override void PostAI(NPC npc)
		{
			if (paintTimer > 0)
				paintTimer--;
		}

		public override void DrawEffects(NPC npc, ref Color drawColor)
		{
			if (paintTimer <= 0)
				return;

			float fade = (float)paintTimer / PaintTicks;   // 1 right after the hit -> 0 when it wears off

			// Light the paint the same as the enemy, so painted enemies still darken in the dark
			float light = drawColor.ToVector3().Length() / Vector3.One.Length();
			Color litPaint = new Color(paintColor.ToVector3() * light) { A = drawColor.A };

			float strength = OverrideColor ? fade : fade * BlendStrength;
			drawColor = Color.Lerp(drawColor, litPaint, strength);
		}
	}
}
