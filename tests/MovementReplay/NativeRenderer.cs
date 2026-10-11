// Generated verbatim method body from local stable 23.352 source; override removed for standalone harness.
using UnityEngine;
public partial class CharaRenderer {
	public void UpdatePosition(ref Vector3 destPos, RenderParam p)
	{
		bool isPC = owner.IsPC;
		int num = (isPC ? _animeFramePC : ((pccData != null) ? _animeFramePCC : _animeFrame));
		bool flag = isPC || num >= 10;
		if (num == 0 || Scene.skipAnime || first || !data.animate || (owner.IsDeadOrSleeping && pccData != null && !owner.IsPC))
		{
			first = false;
			position = destPos;
			if (isPC)
			{
				EClass.player.position = position;
			}
			movePoint.Set(owner.pos);
			step = 9999999;
			isMoving = false;
			moveTimer = 0f;
			RefreshSprite();
			p.x = position.x;
			p.y = position.y;
			p.z = position.z;
			lastShadowFix = (lastShadowFix2 = p.shadowFix);
			return;
		}
		if (!owner.pos.Equals(movePoint))
		{
			if (RenderObject.gameSpeed > 1f)
			{
				p.shadowFix = (lastShadowFix = 0f);
			}
			else
			{
				p.shadowFix = (lastShadowFix = lastShadowFix2);
			}
			movePoint.Set(owner.pos);
			orgPos = position;
			actTime = owner.actTime;
			if (isPC && EClass.player.renderExtraTime > 0f)
			{
				actTime += EClass.player.renderExtraTime;
			}
			RefreshSprite();
			step = 1;
			if (!isMoving)
			{
				isMoving = true;
				if (!isPC || EClass.player.renderExtraTime <= 0f)
				{
					nextframeTimer = (RenderObject.animeSetting.nextFrameInterval + actTime / 4f) / 2f;
				}
			}
			moveTimer = 0f;
		}
		if (num < 100)
		{
			num = (int)((float)num / 0.3f * actTime);
		}
		float num2 = (actTime + ((!isPC && owner.IsPCParty) ? RenderObject.animeSetting.animeExtraTimeParty : RenderObject.animeSetting.animeExtraTime) * RenderObject.gameSpeed) / (float)num;
		moveTimer += RenderObject.gameDelta;
		if (step >= num)
		{
			if (position == destPos)
			{
				if (isMoving && isPC)
				{
					EClass.player.position = position;
				}
				isMoving = false;
				moveTimer = 0f;
			}
			else if (!flag)
			{
				position = destPos;
			}
			else if (moveTimer >= num2)
			{
				int num3 = (int)(moveTimer / num2);
				moveTimer -= (float)num3 * num2;
				if (Vector3.Distance(position, destPos) < RenderObject.animeSetting.destRadius)
				{
					position = destPos;
					if (isMoving && isPC)
					{
						EClass.player.position = position;
					}
					isMoving = false;
				}
				else
				{
					position.z = destPos.z;
					Vector3 vector = Vector3.MoveTowards(position, destPos, (float)num3 * RenderObject.animeSetting.slowSpeed / (float)step);
					position = vector;
				}
			}
			if (owner.idleTimer > 0f)
			{
				owner.idleTimer -= RenderObject.gameDelta;
				if (flag && owner.idleTimer <= 0f)
				{
					IdleFrame();
					nextframeTimer = 0f;
				}
			}
			p.x = position.x;
			p.y = position.y;
			p.z = position.z;
			lastShadowFix = (lastShadowFix2 = p.shadowFix);
			if (isPC)
			{
				CellDetail detail = EClass.pc.Cell.detail;
				if (detail == null || detail.anime == null)
				{
					position.z = destPos.z;
					Vector3 vector2 = Vector3.MoveTowards(position, destPos, 0.1f);
					position = vector2;
					EClass.player.position = Vector3.Lerp(EClass.player.position, position, 0.6f);
				}
			}
			return;
		}
		if (moveTimer >= num2)
		{
			int num4 = (int)(moveTimer / num2);
			step += num4;
			moveTimer -= (float)num4 * num2;
			if (step >= num)
			{
				step = num;
			}
		}
		owner.idleTimer = RenderObject.animeSetting.idleTime;
		float num5 = (float)step / (float)num;
		p.shadowFix = p.shadowFix * num5 + lastShadowFix * (1f - num5);
		lastShadowFix2 = p.shadowFix;
		Vector3 vector3 = orgPos + (destPos - orgPos) * num5 * ((flag && smoothmove) ? RenderObject.animeSetting.fixedMove : 1f);
		if (destPos.z < orgPos.z)
		{
			vector3.z = orgPos.z + (destPos.z - orgPos.z) * RenderObject.animeSetting.gradientZForward.Evaluate(num5);
		}
		else
		{
			vector3.z = orgPos.z + (destPos.z - orgPos.z) * RenderObject.animeSetting.gradientZBack.Evaluate(num5);
		}
		position = vector3;
		if (hasActor)
		{
			if (EClass.core.config.graphic.spriteFrameMode == 0)
			{
				nextframeTimer += RenderObject.gameDelta * 0.5f;
				float nextFrameInterval = RenderObject.animeSetting.nextFrameInterval;
				if (nextframeTimer > nextFrameInterval)
				{
					if (owner.ai is AI_Trolley)
					{
						actor.IdleFrame();
					}
					else
					{
						actor.NextFrame();
					}
					nextframeTimer -= nextFrameInterval;
				}
			}
			else
			{
				nextframeTimer += RenderObject.gameDelta;
				float num6 = RenderObject.animeSetting.nextFrameInterval + actTime / 4f * (0.5f + RenderObject.gameSpeed / 2f);
				if (nextframeTimer > num6)
				{
					if (owner.ai is AI_Trolley)
					{
						actor.IdleFrame();
					}
					else
					{
						actor.NextFrame();
					}
					nextframeTimer -= num6;
				}
			}
		}
		p.x = position.x;
		p.y = position.y;
		p.z = position.z;
		if (isPC)
		{
			EClass.player.position = vector3;
		}
		else if (!hasActor && num >= 5 && hopCurve != null)
		{
			p.y += hopCurve.Evaluate(num5) * RenderObject.animeSetting.hopStrength;
		}
	}

}