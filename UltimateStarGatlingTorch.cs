using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using UnityEngine;

namespace UltimateStarGatling.BepInEx
{
			internal static class UltimateStarGatlingTorch
	{
		internal static bool HasHypnoPending
		{
			get
			{
				return UltimateStarGatlingTorch.HypnoPendingZombies.Count > 0;
			}
		}

		internal static void ResetHypnoPending()
		{
			UltimateStarGatlingTorch.HypnoPendingZombies.Clear();
		}

		internal static bool TryResolve(Plant torch, out UltimateStarGatlingTorch.EnhanceKind kind, out float damageMultiplier)
		{
			kind = UltimateStarGatlingTorch.EnhanceKind.None;
			damageMultiplier = 1f;
			if (torch == null)
			{
				return false;
			}
			if (torch is ThreeTorch)
			{
				kind = UltimateStarGatlingTorch.EnhanceKind.Triple;
				damageMultiplier = 3f;
				return true;
			}
			if (torch is CherryTorch)
			{
				kind = UltimateStarGatlingTorch.EnhanceKind.CherryExplosion;
				return true;
			}
			if (torch is IceTorch)
			{
				kind = UltimateStarGatlingTorch.EnhanceKind.ColdOnHit;
				return true;
			}
			if (torch is DoomTorch)
			{
				kind = UltimateStarGatlingTorch.EnhanceKind.EmberOnHit;
				return true;
			}
			if (torch is UltimateTorch)
			{
				kind = UltimateStarGatlingTorch.EnhanceKind.JalaOnHit;
				damageMultiplier = 6f;
				return true;
			}
			if (torch is SuperTorch)
			{
				kind = UltimateStarGatlingTorch.EnhanceKind.JalaOnHit;
				damageMultiplier = 4f;
				return true;
			}
			if (torch is JalaTorch)
			{
				kind = UltimateStarGatlingTorch.EnhanceKind.JalaOnHit;
				damageMultiplier = 2f;
				return true;
			}
			if (torch is HypnoTorch)
			{
				kind = UltimateStarGatlingTorch.EnhanceKind.HypnoOnKill;
				return true;
			}
			if (torch is GarlicTorch)
			{
				kind = UltimateStarGatlingTorch.EnhanceKind.GarlicPoisonOnHit;
				return true;
			}
			if (UltimateStarGatlingTorch.IsPlainTorchWood(torch))
			{
				kind = UltimateStarGatlingTorch.EnhanceKind.NormalDouble;
				damageMultiplier = 2f;
				return true;
			}
			return false;
		}

		private static bool IsPlainTorchWood(Plant torch)
		{
			return torch is TorchWood && (!(torch is IceTorch) && !(torch is DoomTorch) && !(torch is JalaTorch) && !(torch is SquashTorch) && !(torch is HypnoTorch) && !(torch is GarlicTorch) && !(torch is NutTorch) && !(torch is StarTorch) && !(torch is TorchFume)) && !(torch is GoldThreeTorch);
		}

		/// <summary>把子弹颜色还原成出厂态（不动 Shadow，保留 alpha），并清掉染色记录。</summary>
		internal static void ResetBulletTint(Bullet bullet)
		{
			if (bullet == null)
			{
				return;
			}
			UltimateStarGatlingTorch.ClearTintRecord(bullet);
			try
			{
				foreach (SpriteRenderer spriteRenderer in UltimateStarGatlingCombat.GetBulletRenderers(bullet))
				{
					if (!(spriteRenderer == null) && !UltimateStarGatlingTorch.IsBulletShadow(spriteRenderer))
					{
						Color color = spriteRenderer.color;
						if (color.r < 0.999f || color.g < 0.999f || color.b < 0.999f)
						{
							spriteRenderer.color = new Color(1f, 1f, 1f, color.a);
						}
					}
				}
			}
			catch
			{
			}
		}

		internal static Color GetTint(UltimateStarGatlingTorch.EnhanceKind kind)
		{
			Color result;
			switch (kind)
			{
			case UltimateStarGatlingTorch.EnhanceKind.NormalDouble:
				result = UltimateStarGatlingTorch.TintNormal;
				break;
			case UltimateStarGatlingTorch.EnhanceKind.Triple:
				result = UltimateStarGatlingTorch.TintTriple;
				break;
			case UltimateStarGatlingTorch.EnhanceKind.CherryExplosion:
				result = UltimateStarGatlingTorch.TintCherry;
				break;
			case UltimateStarGatlingTorch.EnhanceKind.EmberOnHit:
				result = UltimateStarGatlingTorch.TintEmber;
				break;
			case UltimateStarGatlingTorch.EnhanceKind.ColdOnHit:
				result = UltimateStarGatlingTorch.TintCold;
				break;
			case UltimateStarGatlingTorch.EnhanceKind.JalaOnHit:
				result = UltimateStarGatlingTorch.TintJala;
				break;
			case UltimateStarGatlingTorch.EnhanceKind.HypnoOnKill:
				result = UltimateStarGatlingTorch.TintHypno;
				break;
			case UltimateStarGatlingTorch.EnhanceKind.GarlicPoisonOnHit:
				result = UltimateStarGatlingTorch.TintGarlic;
				break;
			default:
				result = Color.white;
				break;
			}
			return result;
		}

		/// <summary>给子弹染色（只染本体不染 Shadow，保留出厂 alpha，并记下应在颜色供重申）。</summary>
		internal static void ApplyBulletTint(Bullet bullet, UltimateStarGatlingTorch.EnhanceKind kind)
		{
			if (bullet == null || kind == UltimateStarGatlingTorch.EnhanceKind.None)
			{
				return;
			}
			Color tint = UltimateStarGatlingTorch.GetTint(kind);
			try
			{
				foreach (SpriteRenderer spriteRenderer in UltimateStarGatlingCombat.GetBulletRenderers(bullet))
				{
					if (spriteRenderer == null || UltimateStarGatlingTorch.IsBulletShadow(spriteRenderer))
					{
						continue;
					}
					Color old = spriteRenderer.color;
					spriteRenderer.color = new Color(tint.r, tint.g, tint.b, old.a);
				}
			}
			catch
			{
			}
			UltimateStarGatlingTorch.RecordTint(bullet, tint);
		}

		/// <summary>该渲染器是不是子弹的影子（影子不参与染色）。</summary>
		private static bool IsBulletShadow(SpriteRenderer spriteRenderer)
		{
			bool result;
			try
			{
				GameObject gameObject = spriteRenderer.gameObject;
				string name = (gameObject != null) ? gameObject.name : "";
				if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains("shadow"))
				{
					result = true;
				}
				else
				{
					Sprite sprite = spriteRenderer.sprite;
					string spriteName = (sprite != null) ? sprite.name : "";
					result = (!string.IsNullOrEmpty(spriteName) && spriteName.ToLowerInvariant().Contains("shadow"));
				}
			}
			catch
			{
				result = false;
			}
			return result;
		}

		/// <summary>记下这枚子弹应该是什么颜色，供 ReassertTint 重申。</summary>
		private static void RecordTint(Bullet bullet, Color tint)
		{
			try
			{
				IntPtr pointer = bullet.Pointer;
				UltimateStarGatlingTorch.ActiveTints[pointer] = tint;
				UltimateStarGatlingTorch.TintDeadlines[pointer] = Time.time + UltimateStarGatlingTorch.TintReassertSeconds;
			}
			catch
			{
			}
		}

		/// <summary>过火状态被清掉时一并忘掉染色记录。</summary>
		internal static void ClearTintRecord(Bullet bullet)
		{
			if (bullet == null)
			{
				return;
			}
			try
			{
				IntPtr pointer = bullet.Pointer;
				UltimateStarGatlingTorch.ActiveTints.Remove(pointer);
				UltimateStarGatlingTorch.TintDeadlines.Remove(pointer);
			}
			catch
			{
			}
		}

		/// <summary>重申染色（每帧由 Bullet_star.Update 维护补丁调用）。</summary>
		internal static void ReassertTint(Bullet bullet)
		{
			if (bullet == null || UltimateStarGatlingTorch.ActiveTints.Count == 0)
			{
				return;
			}
			try
			{
				IntPtr pointer = bullet.Pointer;
				Color tint;
				if (!UltimateStarGatlingTorch.ActiveTints.TryGetValue(pointer, out tint))
				{
					return;
				}
				float deadline;
				if (UltimateStarGatlingTorch.TintDeadlines.TryGetValue(pointer, out deadline) && Time.time > deadline)
				{
					UltimateStarGatlingTorch.ClearTintRecord(bullet);
					return;
				}
				foreach (SpriteRenderer spriteRenderer in UltimateStarGatlingCombat.GetBulletRenderers(bullet))
				{
					if (spriteRenderer == null || UltimateStarGatlingTorch.IsBulletShadow(spriteRenderer))
					{
						continue;
					}
					Color now = spriteRenderer.color;
					if (now.r != tint.r || now.g != tint.g || now.b != tint.b)
					{
						spriteRenderer.color = new Color(tint.r, tint.g, tint.b, now.a);
					}
				}
			}
			catch
			{
			}
		}

		/// <summary>换关清场：染色记录与诊断去重表一起清。</summary>
		internal static void ResetTintRecords()
		{
			UltimateStarGatlingTorch.ActiveTints.Clear();
			UltimateStarGatlingTorch.TintDeadlines.Clear();
			UltimateStarGatlingTorch.TorchResolveDiagLogged.Clear();
		}

		/// <summary>过火诊断（默认静默，受 VerboseRuntimeLog 控制；去重键只用火炬类名）。</summary>
		internal static void LogTorchDiag(Plant torch, string detail)
		{
			if (!HolyStarGatling.VerboseRuntimeLog)
			{
				return;
			}
			string torchName = (torch != null) ? torch.GetType().Name : "null";
			UltimateStarGatlingTorch.LogTorchDiagInternal(torchName, torchName, detail);
		}

		private static void LogTorchDiagInternal(string key, string torchName, string detail)
		{
			try
			{
				if (!UltimateStarGatlingTorch.TorchResolveDiagLogged.Add(key))
				{
					return;
				}
				HolyStarGatling.LogVerbose("[UltimateStarGatling] 过火诊断：" + torchName + " → " + detail);
			}
			catch
			{
			}
		}

		/// <summary>某个火炬没能被识别时的诊断（每类一次）。</summary>
		internal static void LogTorchUnresolved(Plant torch)
		{
			UltimateStarGatlingTorch.LogTorchDiag(torch, "不在支持列表（TryResolve=false ⇒ 不会染色、也不加伤）");
		}

		internal static void ApplyHitEffect(Bullet bullet, Zombie zombie, UltimateStarGatlingTorch.EnhanceKind kind)
		{
			if (bullet == null || zombie == null || zombie.beforeDying || zombie.isMindControlled)
			{
				return;
			}
			try
			{
				switch (kind)
				{
				case UltimateStarGatlingTorch.EnhanceKind.CherryExplosion:
					UltimateStarGatlingTorch.TryCreateCherryExplodeAtZombie(bullet, zombie);
					break;
				case UltimateStarGatlingTorch.EnhanceKind.EmberOnHit:
					zombie.SetEmbered(false);
					UltimateStarGatlingTorch.ApplyAreaDamageAroundZombie(bullet, zombie, bullet.Damage);
					break;
				case UltimateStarGatlingTorch.EnhanceKind.ColdOnHit:
					UltimateStarGatlingTorch.ApplyColdHitEffect(zombie);
					break;
				case UltimateStarGatlingTorch.EnhanceKind.JalaOnHit:
					zombie.SetJalaed();
					break;
				case UltimateStarGatlingTorch.EnhanceKind.GarlicPoisonOnHit:
					UltimateStarGatlingTorch.ApplyGarlicPoisonHitEffect(zombie);
					break;
				}
			}
			catch
			{
			}
		}

		private static void ApplyColdHitEffect(Zombie zombie)
		{
			if (zombie == null)
			{
				return;
			}
			try
			{
				zombie.SetCold(3f, 100, false);
			}
			catch
			{
			}
			try
			{
				zombie.AddfreezeLevel(100);
			}
			catch
			{
			}
			try
			{
				int num = Mathf.Max(1, Mathf.RoundToInt((float)zombie.theHealth * 0.03f));
				zombie.TakeDamage(num, (IDamageMaker)null, (DamageType)2, (PlantType)(-1), false);
			}
			catch
			{
			}
		}

		private static void ApplyGarlicPoisonHitEffect(Zombie zombie)
		{
			if (zombie == null)
			{
				return;
			}
			UltimateStarGatlingTorch.ApplyGarlicPoison(zombie);
			try
			{
				int poisonLevel = zombie.poisonLevel;
				if (poisonLevel > 0)
				{
					int num = poisonLevel * 100;
					if (num > 0)
					{
						zombie.TakeDamage(num, (IDamageMaker)null, (DamageType)0, (PlantType)(-1), false);
					}
				}
			}
			catch
			{
			}
		}

		private static void ApplyAreaDamageAroundZombie(Bullet bullet, Zombie center, int damage)
		{
			if (center == null || damage <= 0)
			{
				return;
			}
			Board board = ((bullet != null) ? bullet.board : null) ?? Board.Instance;
			if (((board != null) ? board.zombieArray : null) == null)
			{
				return;
			}
			int column = center.Column;
			int theZombieRow = center.theZombieRow;
			PlantType fromType = (bullet != null && bullet.fromType >= (PlantType)0) ? bullet.fromType : (PlantType)5166;
			UltimateStarGatlingTorch.ForEachZombieIn3x3(board, column, theZombieRow, delegate(Zombie z)
			{
				if (z == null || z.beforeDying || z.isMindControlled)
				{
					return;
				}
				try
				{
					z.TakeDamage(damage, (IDamageMaker)null, (DamageType)10, fromType, false);
				}
				catch
				{
					try
					{
						z.TakeDamage(damage, (IDamageMaker)null, (DamageType)10, (PlantType)(-1), false);
					}
					catch
					{
					}
				}
			});
		}

		private static void ForEachZombieIn3x3(Board board, int centerCol, int centerRow, Action<Zombie> action)
		{
			if (((board != null) ? board.zombieArray : null) == null || action == null)
			{
				return;
			}
			for (int i = 0; i < board.zombieArray.Count; i++)
			{
				Zombie zombie = board.zombieArray[i];
				if (!(zombie == null))
				{
					try
					{
						if (!zombie.beforeDying)
						{
							if (Mathf.Abs(zombie.theZombieRow - centerRow) <= 1)
							{
								if (Mathf.Abs(zombie.Column - centerCol) <= 1)
								{
									action(zombie);
								}
							}
						}
					}
					catch
					{
					}
				}
			}
		}

		internal static void MarkHypnoPending(Zombie zombie)
		{
			if (zombie == null || zombie.isMindControlled || zombie.beforeDying)
			{
				return;
			}
			if (!UltimateStarGatlingTorch.CanHypnoZombie(zombie))
			{
				return;
			}
			try
			{
				UltimateStarGatlingTorch.HypnoPendingZombies.Add(zombie.Pointer);
			}
			catch
			{
			}
		}

		internal static void TrySpawnHypnoCloneOnDeath(Zombie zombie)
		{
			if (zombie == null)
			{
				return;
			}
			try
			{
				if (!UltimateStarGatlingTorch.HypnoPendingZombies.Remove(zombie.Pointer))
				{
					return;
				}
			}
			catch
			{
				return;
			}
			if (zombie.isMindControlled || !UltimateStarGatlingTorch.CanHypnoZombie(zombie))
			{
				return;
			}
			if (CreateZombie.Instance == null)
			{
				return;
			}
			try
			{
				Vector3 vector = (zombie.axis != null) ? zombie.axis.position : zombie.transform.position;
				CreateZombie.Instance.SetZombieWithMindControl(zombie.theZombieRow, zombie.theZombieType, vector.x, true);
			}
			catch
			{
			}
		}

		private static bool CanHypnoZombie(Zombie zombie)
		{
			try
			{
				if (TypeMgr.IsBossZombie(zombie.theZombieType))
				{
					return false;
				}
				if (TypeMgr.IsLeaderZombie(zombie.theZombieType))
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			return true;
		}

		private static void ApplyGarlicPoison(Zombie zombie)
		{
			if (zombie == null)
			{
				return;
			}
			try
			{
				for (int i = 0; i < 5; i++)
				{
					zombie.AddPoisonLevel();
				}
			}
			catch
			{
				try
				{
					zombie.poisonLevel += 5;
				}
				catch
				{
				}
			}
		}

		private static void TryCreateCherryExplodeAtZombie(Bullet bullet, Zombie zombie)
		{
			if (bullet == null || zombie == null)
			{
				return;
			}
			Board board = bullet.board ?? Board.Instance;
			if (((board != null) ? board.boardAction : null) == null)
			{
				return;
			}
			Vector2 pos = (zombie.axis != null) ? zombie.axis.position : zombie.transform.position;
			int theZombieRow = zombie.theZombieRow;
			int damage = Mathf.Max(1, bullet.Damage);
			PlantType plantType = bullet.fromType;
			if (plantType < (PlantType)0)
			{
				plantType = (PlantType)5166;
			}
			if (UltimateStarGatlingTorch.CreateCherryExplode(board, pos, theZombieRow, damage, plantType))
			{
				try
				{
					GameAPP.PlaySound(40, 0.2f, 1f);
				}
				catch
				{
				}
			}
		}

		private static bool CreateCherryExplode(Board board, Vector2 pos, int row, int damage, PlantType fromType)
		{
			try
			{
				board.boardAction.CreateCherryExplode(pos, row, (CherryBombType)2, damage, fromType, null, true);
				return true;
			}
			catch
			{
			}
			try
			{
				board.boardAction.CreateCherryExplode(pos, row, (CherryBombType)0, damage, fromType, null, true);
				return true;
			}
			catch
			{
			}
			try
			{
				board.boardAction.CreateCherryExplode(pos, row, (CherryBombType)0, damage, fromType, null, true);
				return true;
			}
			catch
			{
			}
			bool result;
			try
			{
				CreateParticle.SetParticle(2, pos, row, true);
				result = true;
			}
			catch
			{
				result = false;
			}
			return result;
		}

				internal static bool ShouldHandleUltimateStarTorch(Bullet bullet)
		{
			return !(bullet == null) && !bullet.shootByZombie && UltimateStarGatlingCombat.IsUltimateStarBullet(bullet) && UltimateStarGatlingCombat.IsAnyGatlingOnBoard();
		}

				internal static bool ShouldSkipVanillaTorchHandler(Collider2D collision)
		{
			if (collision == null || !UltimateStarGatlingCombat.IsAnyGatlingOnBoard())
			{
				return false;
			}
			bool result;
			try
			{
				Bullet bullet;
				result = (collision.TryGetComponent<Bullet>(out bullet) && UltimateStarGatlingTorch.ShouldHandleUltimateStarTorch(bullet));
			}
			catch
			{
				result = false;
			}
			return result;
		}

		internal static void OnTorchTriggerEnter(Plant torch, Collider2D collision)
		{
			if (torch == null || collision == null)
			{
				return;
			}
			Bullet bullet;
			if (!collision.TryGetComponent<Bullet>(out bullet) || bullet == null)
			{
				return;
			}
			UltimateStarGatlingTorch.TryEnhanceThroughTorch(torch, bullet);
		}

		internal static bool TryEnhanceThroughTorch(Plant torch, Bullet bullet)
		{
			if (torch == null || bullet == null || bullet.shootByZombie)
			{
				return false;
			}
			if (!UltimateStarGatlingTorch.ShouldHandleUltimateStarTorch(bullet))
			{
				return false;
			}
			if (!UltimateStarGatlingCombat.HasPassedShootPoint(bullet, torch))
			{
				UltimateStarGatlingCombat.QueueTorchRetry(bullet, torch);
				return false;
			}
			if (UltimateStarGatlingPatches.GetTorchMark(bullet) != UltimateStarGatlingTorch.EnhanceKind.None)
			{
				return false;
			}
			UltimateStarGatlingTorch.EnhanceKind kind;
			float num;
			if (!UltimateStarGatlingTorch.TryResolve(torch, out kind, out num))
			{
				UltimateStarGatlingTorch.LogTorchUnresolved(torch);
				return false;
			}
			bool result;
			try
			{
				if (torch is UltimateTorch)
				{
					result = UltimateStarGatlingTorch.TryEnhanceUltimateTorch(torch, bullet, kind, num);
				}
				else
				{
					bullet.torchWood = torch;
					if (num > 1.001f)
					{
						int damage = Mathf.Max(1, Mathf.RoundToInt((float)bullet.Damage * num));
						bullet.Damage = damage;
					}
					UltimateStarGatlingPatches.SetTorchMark(bullet, kind);
					UltimateStarGatlingTorch.ApplyBulletTint(bullet, kind);
					GameAPP.PlaySound(61, 0.5f, 1f);
					UltimateStarGatlingTorch.LogTorchDiag(torch, "√ 已过火 " + kind.ToString() + "（伤害倍率 " + num.ToString("0.##") + "，已染色）");
					result = true;
				}
			}
			catch
			{
				result = false;
			}
			return result;
		}

		/// <summary>究极火爆树桩专用：把这发星星换成樱桃窝瓜子弹再放出去。</summary>
		internal static bool TryEnhanceUltimateTorch(Plant torch, Bullet bullet, UltimateStarGatlingTorch.EnhanceKind kind, float damageMultiplier)
		{
			Board board;
			if ((board = torch.board) == null)
			{
				board = (bullet.board ?? Board.Instance);
			}
			Board board2 = board;
			if (((board2 != null) ? board2.boardAction : null) == null)
			{
				return false;
			}
			int num = Mathf.Max(1, Mathf.RoundToInt((float)bullet.Damage * damageMultiplier));
			bool result;
			try
			{
				Bullet bullet2 = board2.boardAction.FirePeas(bullet, torch, num, (BulletType)36);
				if (bullet2 == null)
				{
					result = false;
				}
				else
				{
					bullet2.torchWood = torch;
					bullet2.from = bullet.from;
					bullet2.fromType = ((bullet.fromType >= (PlantType)0) ? bullet.fromType : (PlantType)5166);
					UltimateStarGatlingPatches.SetTorchMark(bullet2, kind);
					UltimateStarGatlingTorch.ApplyBulletTint(bullet2, kind);
					GameAPP.PlaySound(61, 0.5f, 1f);
					result = true;
				}
			}
			catch (Exception ex)
			{
				ManualLogSource pluginLogger = Core.PluginLogger;
				if (pluginLogger != null)
				{
										BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(31, 1, out bool flag);
					if (flag)
					{
						bepInExWarningLogInterpolatedStringHandler.AppendLiteral("[UltimateStarGatling] 究极火爆过火失败：");
						bepInExWarningLogInterpolatedStringHandler.AppendFormatted<string>(ex.Message);
					}
					pluginLogger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
				}
				result = false;
			}
			return result;
		}

		private const float IceColdDurationSeconds = 3f;

		private const int IceColdLevel = 100;

		private const int IceFreezeLevel = 100;

		private const float IceHpPercentDamage = 0.03f;

		private const int GarlicPoisonLayers = 5;

		private const int GarlicPoisonDamagePerLevel = 100;

		private static readonly Color TintNormal = new Color(1f, 0.95f, 0.35f, 1f);

		private static readonly Color TintTriple = new Color(1f, 0.82f, 0.12f, 1f);

		private static readonly Color TintCherry = new Color(0.88f, 0.12f, 0.12f, 1f);

		private static readonly Color TintEmber = new Color(0.78f, 0.32f, 0.98f, 1f);

		private static readonly Color TintCold = new Color(0.42f, 0.72f, 1f, 1f);

		private static readonly Color TintJala = new Color(1f, 0.28f, 0.18f, 1f);

		private static readonly Color TintHypno = new Color(1f, 0.62f, 0.82f, 1f);

		private static readonly Color TintGarlic = new Color(0.72f, 0.72f, 0.72f, 1f);

		private static readonly System.Collections.Generic.HashSet<IntPtr> HypnoPendingZombies = new System.Collections.Generic.HashSet<IntPtr>();

		/// <summary>过火后这枚子弹应该是什么颜色（key = Bullet.Pointer），供每帧重申。</summary>
		private static readonly System.Collections.Generic.Dictionary<IntPtr, Color> ActiveTints = new System.Collections.Generic.Dictionary<IntPtr, Color>();

		/// <summary>重申截止时刻。</summary>
		private static readonly System.Collections.Generic.Dictionary<IntPtr, float> TintDeadlines = new System.Collections.Generic.Dictionary<IntPtr, float>();

		/// <summary>过火后重申染色的时长（秒）。</summary>
		private const float TintReassertSeconds = 1.5f;

		/// <summary>诊断去重表（key = 火炬类名）。</summary>
		private static readonly System.Collections.Generic.HashSet<string> TorchResolveDiagLogged = new System.Collections.Generic.HashSet<string>();

				internal enum EnhanceKind
		{
			None,
			NormalDouble,
			Triple,
			CherryExplosion,
			EmberOnHit,
			ColdOnHit,
			JalaOnHit,
			HypnoOnKill,
			GarlicPoisonOnHit
		}
	}
}
