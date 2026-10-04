using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace UltimateStarGatling.BepInEx
{
			internal static class UltimateStarGatlingCombat
	{
		internal static bool HasPendingTorch
		{
			get
			{
				return UltimateStarGatlingCombat.PendingTorchByBullet.Count > 0;
			}
		}

		internal static void ResetLevelState()
		{
			UltimateStarGatlingCombat.DirectShotBullets.Clear();
			UltimateStarGatlingCombat.DirectShotSpawnByBullet.Clear();
			UltimateStarGatlingCombat.PendingTorchByBullet.Clear();
			UltimateStarGatlingCombat.BulletRendererCache.Clear();
			UltimateStarGatlingCombat._directShotSpreadIndex = 0;
			UltimateStarGatlingCombat._gatlingBoardCacheFrame = -1;
			UltimateStarGatlingCombat._travelBuffCacheFrame = -1;
			UltimateStarGatlingPatches.ResetLevelTorchMarks();
			UltimateStarGatlingTorch.ResetHypnoPending();
			UltimateStarGatlingSupernova.ClearTintGpuState();
		}

				internal static void RegisterDirectShotBullet(Bullet bullet)
		{
			if (bullet == null)
			{
				return;
			}
			try
			{
				IntPtr pointer = bullet.Pointer;
				UltimateStarGatlingCombat.DirectShotBullets.Add(pointer);
				try
				{
					float num = (float)(UltimateStarGatlingCombat._directShotSpreadIndex++ % 3 - 1) * 0.14f;
					Transform transform = bullet.transform;
					if (transform != null)
					{
						transform.position += new Vector3(0f, num, 0f);
					}
				}
				catch
				{
				}
				Vector3 position = bullet.transform.position;
				UltimateStarGatlingCombat.DirectShotSpawnByBullet[pointer] = new UltimateStarGatlingCombat.DirectShotSpawnInfo(new Vector2(position.x, position.y), Time.time);
			}
			catch
			{
			}
		}

				internal static void UnregisterDirectShotBullet(Bullet bullet)
		{
			if (bullet == null)
			{
				return;
			}
			try
			{
				IntPtr pointer = bullet.Pointer;
				UltimateStarGatlingCombat.DirectShotBullets.Remove(pointer);
				UltimateStarGatlingCombat.DirectShotSpawnByBullet.Remove(pointer);
				UltimateStarGatlingCombat.PendingTorchByBullet.Remove(pointer);
				UltimateStarGatlingCombat.BulletRendererCache.Remove(pointer);
			}
			catch
			{
			}
		}

				internal static bool HasPassedShootPoint(Bullet bullet, Plant torch = null)
		{
			if (bullet == null)
			{
				return false;
			}
			bool result;
			try
			{
				UltimateStarGatlingCombat.DirectShotSpawnInfo directShotSpawnInfo;
				if (!UltimateStarGatlingCombat.IsDirectShotBullet(bullet))
				{
					result = true;
				}
				else if (!UltimateStarGatlingCombat.DirectShotSpawnByBullet.TryGetValue(bullet.Pointer, out directShotSpawnInfo))
				{
					result = false;
				}
				else if (Time.time - directShotSpawnInfo.SpawnTime < 0.05f)
				{
					result = false;
				}
				else
				{
					Vector3 position = bullet.transform.position;
					Vector2 vector = new Vector2(position.x, position.y) - directShotSpawnInfo.Position;
					if (torch != null && UltimateStarGatlingCombat.IsPointBlankTorchPass(bullet, torch, directShotSpawnInfo, position))
					{
						result = true;
					}
					else if (vector.sqrMagnitude < 0.36f)
					{
						result = false;
					}
					else
					{
						result = (vector.x >= 0.18f);
					}
				}
			}
			catch
			{
				result = false;
			}
			return result;
		}

		private static bool IsPointBlankTorchPass(Bullet bullet, Plant torch, UltimateStarGatlingCombat.DirectShotSpawnInfo info, Vector2 pos)
		{
			bool result;
			try
			{
				if (bullet.from == null)
				{
					result = false;
				}
				else
				{
					int thePlantColumn = bullet.from.thePlantColumn;
					int thePlantColumn2 = torch.thePlantColumn;
					if (thePlantColumn >= 0 && thePlantColumn2 >= 0 && (thePlantColumn2 < thePlantColumn || thePlantColumn2 > thePlantColumn + 2))
					{
						result = false;
					}
					else
					{
						float num = (torch.axis != null) ? torch.axis.position.x : torch.transform.position.x;
						if (pos.x < num - 0.4f)
						{
							result = false;
						}
						else
						{
							result = (Time.time - info.SpawnTime >= 0.05f);
						}
					}
				}
			}
			catch
			{
				result = false;
			}
			return result;
		}

		internal static void QueueTorchRetry(Bullet bullet, Plant torch)
		{
			if (bullet == null || torch == null)
			{
				return;
			}
			try
			{
				UltimateStarGatlingCombat.PendingTorchByBullet[bullet.Pointer] = torch;
			}
			catch
			{
			}
		}

				internal static void ProcessPendingTorch(Bullet bullet)
		{
			if (bullet == null || UltimateStarGatlingCombat.PendingTorchByBullet.Count == 0)
			{
				return;
			}
			try
			{
				Plant plant;
				if (UltimateStarGatlingCombat.PendingTorchByBullet.TryGetValue(bullet.Pointer, out plant) && !(plant == null))
				{
					if (UltimateStarGatlingTorch.TryEnhanceThroughTorch(plant, bullet))
					{
						UltimateStarGatlingCombat.PendingTorchByBullet.Remove(bullet.Pointer);
					}
				}
			}
			catch
			{
			}
		}

				internal static void ResetBulletTorchState(Bullet bullet)
		{
			if (bullet == null)
			{
				return;
			}
			try
			{
				bullet.torchWood = null;
			}
			catch
			{
			}
			UltimateStarGatlingPatches.ClearTorchMark(bullet);
			UltimateStarGatlingTorch.ResetBulletTint(bullet);
		}

				internal static bool IsDirectShotBullet(Bullet bullet)
		{
			if (bullet == null)
			{
				return false;
			}
			bool result;
			try
			{
				result = UltimateStarGatlingCombat.DirectShotBullets.Contains(bullet.Pointer);
			}
			catch
			{
				result = false;
			}
			return result;
		}

				internal static bool IsOurPlant(Plant plant)
		{
			if (plant == null)
			{
				return false;
			}
			bool result;
			try
			{
				result = (plant.thePlantType ==(PlantType)5166);
			}
			catch
			{
				result = false;
			}
			return result;
		}

				internal static bool IsOurBullet(Bullet bullet)
		{
			if (bullet == null)
			{
				return false;
			}
			bool result;
			try
			{
				if (bullet.fromType == (PlantType)5166)
				{
					result = true;
				}
				else
				{
					result = (bullet.from != null && UltimateStarGatlingCombat.IsOurPlant(bullet.from));
				}
			}
			catch
			{
				result = false;
			}
			return result;
		}

				internal static bool IsUltimateStarBullet(Bullet bullet)
		{
			if (bullet == null)
			{
				return false;
			}
			bool result;
			try
			{
				result = (bullet.theBulletType == (BulletType)49);
			}
			catch
			{
				result = false;
			}
			return result;
		}

		internal static bool IsAnyGatlingOnBoard()
		{
			bool result;
			try
			{
				int frameCount = Time.frameCount;
				if (UltimateStarGatlingCombat._gatlingBoardCacheFrame == frameCount)
				{
					result = UltimateStarGatlingCombat._gatlingOnBoardCached;
				}
				else
				{
					UltimateStarGatlingCombat._gatlingBoardCacheFrame = frameCount;
					UltimateStarGatlingCombat._gatlingOnBoardCached = (Board.Instance != null && Lawnf.GetPlantCount((PlantType)5166, Board.Instance) > 0);
					result = UltimateStarGatlingCombat._gatlingOnBoardCached;
				}
			}
			catch
			{
				result = false;
			}
			return result;
		}

		private static void RefreshTravelBuffCacheIfNeeded()
		{
			int frameCount = Time.frameCount;
			if (UltimateStarGatlingCombat._travelBuffCacheFrame == frameCount)
			{
				return;
			}
			UltimateStarGatlingCombat._travelBuffCacheFrame = frameCount;
			try
			{
				UltimateStarGatlingCombat._meteorShowerBuffCached = Lawnf.TravelUltimate((UltiBuff)8);
				UltimateStarGatlingCombat._starPowerLevelCached = Lawnf.TravelUltimateLevel((UltiBuff)9);
			}
			catch
			{
				UltimateStarGatlingCombat._meteorShowerBuffCached = false;
				UltimateStarGatlingCombat._starPowerLevelCached = 0;
			}
		}

		internal static bool HasMeteorShowerBuff()
		{
			UltimateStarGatlingCombat.RefreshTravelBuffCacheIfNeeded();
			return UltimateStarGatlingCombat._meteorShowerBuffCached;
		}

		internal static int GetStarPowerLevel()
		{
			UltimateStarGatlingCombat.RefreshTravelBuffCacheIfNeeded();
			return UltimateStarGatlingCombat._starPowerLevelCached;
		}

		internal static SpriteRenderer[] GetBulletRenderers(Bullet bullet)
		{
			if (bullet == null)
			{
				return Array.Empty<SpriteRenderer>();
			}
			SpriteRenderer[] result;
			try
			{
				IntPtr pointer = bullet.Pointer;
				SpriteRenderer[] array;
				if (UltimateStarGatlingCombat.BulletRendererCache.TryGetValue(pointer, out array) && array != null)
				{
					result = array;
				}
				else
				{
					Il2CppArrayBase<SpriteRenderer> componentsInChildren = bullet.GetComponentsInChildren<SpriteRenderer>(true);
					UltimateStarGatlingCombat.BulletRendererCache[pointer] = componentsInChildren;
					result = componentsInChildren;
				}
			}
			catch
			{
				result = Array.Empty<SpriteRenderer>();
			}
			return result;
		}

				internal static void InvalidateBulletRenderers(Bullet bullet)
		{
			if (bullet == null)
			{
				return;
			}
			try
			{
				UltimateStarGatlingCombat.BulletRendererCache.Remove(bullet.Pointer);
			}
			catch
			{
			}
		}

		internal static int ComputeBulletDamage(Plant plant, int baseDamage)
		{
			if (plant == null)
			{
				return baseDamage;
			}
			try
			{
				Board board = plant.board ?? Board.Instance;
				if (board != null && board.boardTag.rogueShooting)
				{
					return baseDamage;
				}
			}
			catch
			{
			}
			float num = (float)baseDamage;
			try
			{
				if (plant.currentLightLevel > 0)
				{
					num += (float)plant.currentLightLevel * 20f;
				}
			}
			catch
			{
			}
			try
			{
				num *= (float)plant.magnetCount * 0.05f + 1f;
			}
			catch
			{
			}
			int starPowerLevel = UltimateStarGatlingCombat.GetStarPowerLevel();
			if (starPowerLevel == 1)
			{
				num *= 2f;
				num = Mathf.Min(num, 3000f);
			}
			else if (starPowerLevel >= 2)
			{
				num *= 3f;
			}
			else
			{
				num = Mathf.Min(num, 300f);
			}
			return Mathf.Max(1, Mathf.RoundToInt(num));
		}

		internal static void ConfigureStarBullet(Bullet bullet, SuperSnowGatling plant, int baseDamage)
		{
			if (bullet == null || plant == null)
			{
				return;
			}
			UltimateStarGatlingCombat.ResetBulletTorchState(bullet);
			// 大帝子弹合并：这一枚承接此前被压制的攻击力
			int damage = UltimateStarGatlingCombat.ComputeBulletDamage(plant, baseDamage) + StarBulletMerge.TakePending(plant);
			try
			{
				bullet.Damage = damage;
				bullet.from = plant;
				bullet.fromType = (PlantType)5166;
			}
			catch
			{
			}
			UltimateStarGatlingCombat.RegisterDirectShotBullet(bullet);
		}

		internal static void TrySpawnUltimateMeteor()
		{
			try
			{
				if (!(Board.Instance == null))
				{
					Board.Instance.CreateUltimateMateorite();
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
						bepInExWarningLogInterpolatedStringHandler.AppendLiteral("[UltimateStarGatling] 召唤究极流星失败：");
						bepInExWarningLogInterpolatedStringHandler.AppendFormatted<string>(ex.Message);
					}
					pluginLogger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
				}
			}
		}

		internal static void TriggerSuperShootTimer(SuperSnowGatling plant)
		{
			if (plant == null)
			{
				return;
			}
			try
			{
				FieldInfo field = typeof(SuperSnowGatling).GetField("timer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null)
				{
					field.SetValue(plant, 5f);
				}
				if (plant.anim != null)
				{
					plant.anim.SetTrigger("supershoot");
				}
			}
			catch
			{
			}
		}

		private static readonly System.Collections.Generic.HashSet<IntPtr> DirectShotBullets = new System.Collections.Generic.HashSet<IntPtr>();

		private static readonly System.Collections.Generic.Dictionary<IntPtr, UltimateStarGatlingCombat.DirectShotSpawnInfo> DirectShotSpawnByBullet = new System.Collections.Generic.Dictionary<IntPtr, UltimateStarGatlingCombat.DirectShotSpawnInfo>();

		private const float MinTravelBeforeTorchSqr = 0.36f;

		private const float MinExistTimeBeforeTorch = 0.05f;

		private const float MinForwardTravelBeforeTorch = 0.18f;

		private const float DirectShotSpreadStepY = 0.14f;

		private const float PointBlankTorchXTolerance = 0.4f;

		private static int _directShotSpreadIndex;

		private static readonly System.Collections.Generic.Dictionary<IntPtr, Plant> PendingTorchByBullet = new System.Collections.Generic.Dictionary<IntPtr, Plant>();

		private static int _gatlingBoardCacheFrame = -1;

		private static bool _gatlingOnBoardCached;

		private static int _travelBuffCacheFrame = -1;

		private static bool _meteorShowerBuffCached;

		private static int _starPowerLevelCached;

		private static readonly System.Collections.Generic.Dictionary<IntPtr, SpriteRenderer[]> BulletRendererCache = new System.Collections.Generic.Dictionary<IntPtr, SpriteRenderer[]>();

				private readonly struct DirectShotSpawnInfo
		{
			internal DirectShotSpawnInfo(Vector2 position, float spawnTime)
			{
				this.Position = position;
				this.SpawnTime = spawnTime;
			}

			internal readonly Vector2 Position;

			internal readonly float SpawnTime;
		}
	}
}
