using System;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>⑤ 过火系统：本植物在场时，大帝/极帝星星穿过火炬获得加强。</summary>
	internal static class HolyStarTorch
	{
		internal static bool ShouldHandle(Bullet bullet)
		{
			if (bullet == null)
			{
				return false;
			}
			try
			{
				if (bullet.shootByZombie)
				{
					return false;
				}
				if (bullet.theBulletType != (BulletType)HolyStarGatling.BulletId)
				{
					return false;
				}
				return HolyStarCombat.IsHolyGatlingOnBoard();
			}
			catch { return false; }
		}

		/// <summary>接管的火炬集合 </summary>
		internal static bool IsSupportedTorch(Plant torch)
		{
			if (torch == null)
			{
				return false;
			}
			try
			{
				UltimateStarGatlingTorch.EnhanceKind kind;
				float multiplier;
				return UltimateStarGatlingTorch.TryResolve(torch, out kind, out multiplier);
			}
			catch { return false; }
		}

		internal static bool TryResolveSupported(Plant torch, out UltimateStarGatlingTorch.EnhanceKind kind, out float damageMultiplier)
		{
			return UltimateStarGatlingTorch.TryResolve(torch, out kind, out damageMultiplier);
		}

		internal static bool ShouldSkipVanillaTorchHandler(Plant torch, Collider2D collision)
		{
			if (torch == null || collision == null)
			{
				return false;
			}
			try
			{
				Bullet bullet;
				if (!collision.TryGetComponent<Bullet>(out bullet) || !ShouldHandle(bullet))
				{
					return false;
				}
				return IsSupportedTorch(torch);
			}
			catch { return false; }
		}

		internal static void OnTorchTriggerEnter(Plant torch, Collider2D collision)
		{
			if (torch == null || collision == null)
			{
				return;
			}
			try
			{
				Bullet bullet;
				if (!collision.TryGetComponent<Bullet>(out bullet) || bullet == null)
				{
					return;
				}
				TryEnhance(torch, bullet);
			}
			catch { }
		}

		internal static bool TryEnhance(Plant torch, Bullet bullet)
		{
			if (torch == null || bullet == null)
			{
				return false;
			}
			try
			{
				if (!ShouldHandle(bullet) || !IsSupportedTorch(torch))
				{
					return false;
				}
				if (UltimateStarGatlingPatches.GetTorchMark(bullet) != UltimateStarGatlingTorch.EnhanceKind.None)
				{
					return false;
				}
				if (!HolyStarCombat.HasPassedShootPoint(bullet, torch))
				{
					// 贴脸树桩排队重试
					UltimateStarGatlingCombat.QueueTorchRetry(bullet, torch);
					return false;
				}
				UltimateStarGatlingTorch.EnhanceKind kind;
				float multiplier;
				if (!TryResolveSupported(torch, out kind, out multiplier))
				{
					UltimateStarGatlingTorch.LogTorchUnresolved(torch);
					return false;
				}

				if (torch is UltimateTorch)
				{
					// 究极火爆树桩：换成樱桃窝瓜子弹
					return UltimateStarGatlingTorch.TryEnhanceUltimateTorch(torch, bullet, kind, multiplier);
				}

				bullet.torchWood = torch;
				if (multiplier > 1.001f)
				{
					bullet.Damage = Mathf.Max(1, Mathf.RoundToInt(bullet.Damage * multiplier));
				}
				UltimateStarGatlingPatches.SetTorchMark(bullet, kind);
				UltimateStarGatlingTorch.ApplyBulletTint(bullet, kind);
				GameAPP.PlaySound(61, 0.5f, 1f);
				UltimateStarGatlingTorch.LogTorchDiag(torch, "√ 极帝子弹已过火 " + kind.ToString() + "（伤害倍率 " + multiplier.ToString("0.##") + "，已染色）");
				return true;
			}
			catch { return false; }
		}

		internal static void ApplyHitEffect(Bullet bullet, Zombie zombie, UltimateStarGatlingTorch.EnhanceKind kind)
		{
			try { UltimateStarGatlingTorch.ApplyHitEffect(bullet, zombie, kind); }
			catch { }
		}

		internal static void ResetBulletState(Bullet bullet)
		{
			if (bullet == null)
			{
				return;
			}
			try { bullet.torchWood = null; }
			catch { }
			UltimateStarGatlingPatches.ClearTorchMark(bullet);
			UltimateStarGatlingTorch.ResetBulletTint(bullet);
		}

		internal static void ClearMark(Bullet bullet)
		{
			UltimateStarGatlingPatches.ClearTorchMark(bullet);
		}

		internal static void ResetLevelState()
		{
		}
	}
}
