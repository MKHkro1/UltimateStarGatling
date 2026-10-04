using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>大帝系子弹合并（性能）</summary>
	internal static class StarBulletMerge
	{
		/// <summary>249 Bullet_star_holy（8003 与 311 同款）。</summary>
		internal const int HolyBulletId = HolyStarGatling.BulletId;

		/// <summary>49 Bullet_ultimateStar（5166）。</summary>
		internal const int MeteorBulletId = 49;

		/// <summary>场上大帝系子弹的默认上限</summary>
		internal const int DefaultMaxLive = 500;
		internal const float DefaultMaxHoldSeconds = 1.2f;

		private const int MinMaxLive = 16;
		private const int MaxMaxLive = 4000;
		private const float MinMaxHoldSeconds = 0.2f;
		private const float MaxMaxHoldSeconds = 10f;
		/// <summary>待发池条目的陈旧时长</summary>
		private const float PendingStaleSeconds = 20f;

		internal static ConfigEntry<bool> EnabledConfig;
		internal static ConfigEntry<int> MaxLiveConfig;
		internal static ConfigEntry<float> MaxHoldSecondsConfig;

		/// <summary>某株植物的待发伤害池</summary>
		private sealed class Pending
		{
			/// <summary>累积的攻击力</summary>
			internal int Damage;
			/// <summary>归属植物的类型</summary>
			internal int PlantType;
			/// <summary>已压制的子弹数</summary>
			internal int Suppressed;
			/// <summary>本窗口的起始时刻（保险阀计时）</summary>
			internal float Since;
			/// <summary>最后被触碰的时刻</summary>
			internal float LastTouch;
		}

		private static readonly Dictionary<IntPtr, Pending> PendingByPlant = new Dictionary<IntPtr, Pending>();

		private static int _countFrame = -1;
		private static int _countCached;
		private static bool _engagedLogged;
		private static bool _poolReadFailedLogged;

		// ---------------------------------------------------------------- 配置

		internal static bool Enabled
		{
			get
			{
				try { return EnabledConfig == null || EnabledConfig.Value; }
				catch { return true; }
			}
		}

		internal static int MaxLive
		{
			get
			{
				try
				{
					int v = MaxLiveConfig == null ? DefaultMaxLive : MaxLiveConfig.Value;
					if (v < MinMaxLive)
					{
						return MinMaxLive;
					}
					return v > MaxMaxLive ? MaxMaxLive : v;
				}
				catch { return DefaultMaxLive; }
			}
		}

		internal static float MaxHoldSeconds
		{
			get
			{
				try
				{
					float v = MaxHoldSecondsConfig == null ? DefaultMaxHoldSeconds : MaxHoldSecondsConfig.Value;
					if (v < MinMaxHoldSeconds)
					{
						return MinMaxHoldSeconds;
					}
					return v > MaxMaxHoldSeconds ? MaxMaxHoldSeconds : v;
				}
				catch { return DefaultMaxHoldSeconds; }
			}
		}

		// ---------------------------------------------------------------- 判定

		internal static bool IsWatched(BulletType type)
		{
			return type == (BulletType)HolyBulletId || type == (BulletType)MeteorBulletId;
		}

		/// <summary>只被压制</summary>
		internal static bool IsOurGatling(Plant plant)
		{
			if (plant == null)
			{
				return false;
			}
			try
			{
				PlantType type = plant.thePlantType;
				return type == (PlantType)HolyStarGatling.PlantId || type == (PlantType)Core.PlantId;
			}
			catch { return false; }
		}

		/// <summary>这一发本该打出的攻击力</summary>
		internal static int ShotDamage(Plant plant)
		{
			try
			{
				if (plant == null)
				{
					return 0;
				}
				if (plant.thePlantType == (PlantType)Core.PlantId)
				{
					return UltimateStarGatlingCombat.ComputeBulletDamage(plant, Core.BaseAttackDamage);
				}
				return HolyStarCombat.ComputeDamage(plant, HolyStarGatling.BaseDamage);
			}
			catch { return 0; }
		}

		// ---------------------------------------------------------------- 计数

		/// <summary>场上「大帝系」子弹数量</summary>
		internal static int CountLive()
		{
			try
			{
				int frame = Time.frameCount;
				if (_countFrame == frame)
				{
					return _countCached;
				}
				_countFrame = frame;

				int pooled = CountFromPools();
				if (pooled >= 0 && pooled < MaxLive)
				{
					_countCached = pooled;
					return pooled;
				}

				// 到上限
				int fromList = CountFromBulletArray();
				if (pooled < 0)
				{
					_countCached = fromList;
					return fromList;
				}
				int count = pooled < fromList ? pooled : fromList;
				_countCached = count;
				return count;
			}
			catch
			{
				_countCached = 0;
				return 0;
			}
		}

		/// <summary>走对象池的 CountActive（O(1)）</summary>
		private static int CountFromPools()
		{
			BulletPoolManager manager = BulletPoolManager.Instance;
			if (manager == null)
			{
				return -1;
			}
			Il2CppSystem.Collections.Generic.Dictionary<BulletType, UnityEngine.Pool.ObjectPool<Bullet>> pools = manager.pools;
			if (pools == null)
			{
				return -1;
			}
			int holy = CountActiveOf(pools, (BulletType)HolyBulletId);
			if (holy < 0)
			{
				return -1;
			}
			int meteor = CountActiveOf(pools, (BulletType)MeteorBulletId);
			if (meteor < 0)
			{
				return -1;
			}
			return holy + meteor;
		}

		private static int CountActiveOf(
			Il2CppSystem.Collections.Generic.Dictionary<BulletType, UnityEngine.Pool.ObjectPool<Bullet>> pools,
			BulletType type)
		{
			try
			{
				UnityEngine.Pool.ObjectPool<Bullet> pool;
				if (pools.TryGetValue(type, out pool) && pool != null)
				{
					return pool.CountActive;
				}
				return 0;   // 池还没建
			}
			catch (Exception ex)
			{
				// 只报一次
				if (!_poolReadFailedLogged)
				{
					_poolReadFailedLogged = true;
					HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 子弹池计数不可用（改用子弹列表回退）：" + ex.Message);
				}
				return -1;
			}
		}

		/// <summary>回退口径</summary>
		private static int CountFromBulletArray()
		{
			int count = 0;
			Board board = Board.Instance;
			BoardEntity entity = board == null ? null : board.boardEntity;
			Il2CppSystem.Collections.Generic.List<Bullet> list = entity == null ? null : entity.bulletArray;
			if (list == null)
			{
				return 0;
			}
			int n = list.Count;
			for (int i = 0; i < n; i++)
			{
				Bullet bullet = list[i];
				if (bullet == null)
				{
					continue;
				}
				if (IsWatched(bullet.theBulletType))
				{
					count++;
				}
			}
			return count;
		}

		// ---------------------------------------------------------------- 闸门

		internal static bool AllowSpawn(Plant plant, int damage)
		{
			if (!Enabled || plant == null)
			{
				return true;
			}
			try
			{
				int live = CountLive();
				if (live < MaxLive)
				{
					return true;
				}

				IntPtr key = plant.Pointer;
				Pending pending;
				if (!PendingByPlant.TryGetValue(key, out pending))
				{
					pending = new Pending();
					pending.PlantType = (int)plant.thePlantType;
					pending.Since = Time.time;
					PendingByPlant[key] = pending;
				}

				if (damage > 0)
				{
					pending.Damage += damage;
				}
				pending.Suppressed++;
				pending.LastTouch = Time.time;

				NoteEngaged(live);

				// 保险阀
				if (Time.time - pending.Since >= MaxHoldSeconds)
				{
					pending.Since = Time.time;   
					PrunePending(Time.time);
					return true;
				}

				PrunePending(Time.time);
				return false;
			}
			catch { return true; }  
		}

		/// <summary>取走并清空该植物的待发伤害（0 = 没有）</summary>
		internal static int TakePending(Plant plant)
		{
			if (plant == null)
			{
				return 0;
			}
			try
			{
				IntPtr key = plant.Pointer;
				Pending pending;
				if (!PendingByPlant.TryGetValue(key, out pending))
				{
					return 0;
				}
				PendingByPlant.Remove(key);

				// 指针可能已被回收
				if (pending.PlantType != (int)plant.thePlantType)
				{
					return 0;
				}

				if (pending.Damage > 0)
				{
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 大帝子弹合并：这一枚承接了累积攻击力 +" +
						pending.Damage + "（此前压制 " + pending.Suppressed + " 发，场上大帝子弹 " + CountLive() + "）");
				}
				return pending.Damage;
			}
			catch { return 0; }
		}

		/// <summary>清理长时间没被触碰的条目</summary>
		private static void PrunePending(float now)
		{
			if (PendingByPlant.Count <= 8)
			{
				return;
			}
			List<IntPtr> stale = null;
			foreach (KeyValuePair<IntPtr, Pending> pair in PendingByPlant)
			{
				if (now - pair.Value.LastTouch > PendingStaleSeconds)
				{
					if (stale == null)
					{
						stale = new List<IntPtr>();
					}
					stale.Add(pair.Key);
				}
			}
			if (stale == null)
			{
				return;
			}
			for (int i = 0; i < stale.Count; i++)
			{
				PendingByPlant.Remove(stale[i]);
			}
		}

		/// <summary>换关清理</summary>
		internal static void ResetLevelState()
		{
			PendingByPlant.Clear();
			_countFrame = -1;
			_countCached = 0;
			_engagedLogged = false;
		}

		/// <summary>首次在本关触发合并时留一行</summary>
		private static void NoteEngaged(int live)
		{
			if (_engagedLogged)
			{
				return;
			}
			_engagedLogged = true;
			HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 大帝子弹合并已生效：场上大帝子弹 " + live + " 发已达上限 " +
				MaxLive + "，新子弹不再生成，其攻击力并入本植物下一枚子弹（配置「性能」可关闭或调整上限）");
		}

		// ---------------------------------------------------------------- Harmony 补丁

		/// <summary> 直射闸门：压制时置 __result = null</summary>
		[HarmonyPatch(typeof(SuperSnowGatling), "Shoot1")]
		internal static class SuperSnowGatling_Shoot1_MergeGate
		{
			[HarmonyPrefix]
			private static bool Prefix(SuperSnowGatling __instance, ref Bullet __result)
			{
				if (!Enabled || !IsOurGatling(__instance))
				{
					return true;
				}
				try
				{
					if (AllowSpawn(__instance, ShotDamage(__instance)))
					{
						return true;
					}
					__result = null;
					return false;
				}
				catch { return true; }
			}
		}

		/// <summary>换关清空待发池</summary>
		[HarmonyPatch(typeof(Board), "Start")]
		internal static class Board_Start_MergeReset
		{
			[HarmonyPostfix]
			private static void Postfix()
			{
				ResetLevelState();
			}
		}
	}
}
