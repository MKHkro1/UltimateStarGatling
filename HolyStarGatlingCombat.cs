using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering; // SortingGroup

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>终焉·撰星极帝机枪射手公共逻辑</summary>
	internal static class HolyStarCombat
	{
		private static GameObject _satelliteProto;
		private static bool _satelliteProtoFailed;
		private static Vector3 _satelliteScale = Vector3.one;
		private static bool _satelliteScaleResolved;

		private static int _boardCacheFrame = -1;
		private static bool _boardCached;

		private static readonly Dictionary<IntPtr, float> BulletSpawnTime = new Dictionary<IntPtr, float>();
		private static readonly Dictionary<IntPtr, Vector2> BulletSpawnPosition = new Dictionary<IntPtr, Vector2>();

		/// <summary>⑥ 超新星窗口结束时刻</summary>
		internal static float SupernovaEndTime;

		internal static bool SupernovaActive
		{
			get
			{
				try { return SupernovaEndTime > Time.time; }
				catch { return false; }
			}
		}

		// ---------------------------------------------------------------- 判定

		internal static bool IsOurPlant(Plant plant)
		{
			if (plant == null)
			{
				return false;
			}
			try { return plant.thePlantType == (PlantType)HolyStarGatling.PlantId; }
			catch { return false; }
		}

		internal static bool IsOurBullet(Bullet bullet)
		{
			if (bullet == null)
			{
				return false;
			}
			try
			{
				if (bullet.theBulletType != (BulletType)HolyStarGatling.BulletId)
				{
					return false;
				}
				if (bullet.fromType == (PlantType)HolyStarGatling.PlantId)
				{
					return true;
				}
				return IsOurPlant(bullet.from);
			}
			catch { return false; }
		}

		/// <summary>场上是否有本植物</summary>
		internal static bool IsHolyGatlingOnBoard()
		{
			try
			{
				int frame = Time.frameCount;
				if (_boardCacheFrame == frame)
				{
					return _boardCached;
				}
				_boardCacheFrame = frame;
				Board board = Board.Instance;
				_boardCached = board != null && Lawnf.GetPlantCount((PlantType)HolyStarGatling.PlantId, board) > 0;
				return _boardCached;
			}
			catch { return false; }
		}

		internal static bool TryGetBehaviour(Plant plant, out HolyStarGatlingBehaviour behaviour)
		{
			behaviour = null;
			if (!IsOurPlant(plant))
			{
				return false;
			}
			behaviour = HolyStarGatlingBehaviour.TryGetBehaviour(plant);
			return behaviour != null;
		}

		// ---------------------------------------------------------------- 伤害

		internal static int ComputeDamage(Plant plant, int baseDamage)
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
			catch { }

			float value = baseDamage;

			try
			{
				if (plant.currentLightLevel > 0)
				{
					value += plant.currentLightLevel * HolyStarGatling.LightDamagePerLevel;
				}
			}
			catch { }

			try
			{
				value *= 1f + plant.magnetCount * HolyStarGatling.MagnetDamagePerLink;
			}
			catch { }

			int starPower = GetStarPowerLevel();
			if (starPower >= 2)
			{
				value *= 3f;
			}
			else if (starPower == 1)
			{
				value *= 2f;
				value = Mathf.Min(value, HolyStarGatling.StarPowerCapLv1);
			}
			else
			{
				value = Mathf.Min(value, HolyStarGatling.DefaultDamageCap);
			}

			return Mathf.Max(1, Mathf.RoundToInt(value));
		}

		public static int GetStarPowerLevel()
		{
			try { return Lawnf.TravelUltimateLevel(HolyStarGatling.BuffStarPower); }
			catch { return 0; }
		}

		public static bool HasMeteorShowerBuff()
		{
			try { return Lawnf.TravelUltimate(HolyStarGatling.BuffMeteorShower); }
			catch { return false; }
		}

		// ---------------------------------------------------------------- 子弹

		/// <summary>配置一发本植物的星星子弹</summary>
		internal static void ConfigureBullet(Bullet bullet, SuperSnowGatling plant, int baseDamage)
		{
			if (bullet == null || plant == null)
			{
				return;
			}
			try
			{
				HolyStarTorch.ResetBulletState(bullet);
				// 大帝子弹合并：这一枚承接此前被压制的攻击力
				bullet.Damage = ComputeDamage(plant, baseDamage) + StarBulletMerge.TakePending(plant);
				bullet.from = plant;
				bullet.fromType = (PlantType)HolyStarGatling.PlantId;
			}
			catch { }

			TrackBullet(bullet);
		}

		internal static void TrackBullet(Bullet bullet)
		{
			if (bullet == null)
			{
				return;
			}
			try
			{
				float now = Time.time;
				IntPtr key = bullet.Pointer;
				BulletSpawnTime[key] = now;
				Vector3 p = bullet.transform.position;
				BulletSpawnPosition[key] = new Vector2(p.x, p.y);
				PruneBulletTrackers(now);
			}
			catch { }
		}

		/// <summary>超过这个条数才做一次清理。</summary>
		private const int BulletTrackPruneThreshold = 256;
		/// <summary>追踪表保留时长。</summary>
		private const float BulletTrackKeepSeconds = 8f;

		/// <summary>回收追踪表里的陈旧条目。</summary>
		private static void PruneBulletTrackers(float now)
		{
			if (BulletSpawnTime.Count < BulletTrackPruneThreshold)
			{
				return;
			}
			try
			{
				List<IntPtr> stale = null;
				foreach (KeyValuePair<IntPtr, float> pair in BulletSpawnTime)
				{
					if (now - pair.Value > BulletTrackKeepSeconds)
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
					BulletSpawnTime.Remove(stale[i]);
					BulletSpawnPosition.Remove(stale[i]);
				}
			}
			catch { }
		}

		internal static void UnregisterBullet(Bullet bullet)
		{
			if (bullet == null)
			{
				return;
			}
			try
			{
				IntPtr key = bullet.Pointer;
				BulletSpawnTime.Remove(key);
				BulletSpawnPosition.Remove(key);
			}
			catch { }
			HolyStarTorch.ClearMark(bullet);
		}

		/// <summary>过火判定</summary>
		internal static bool HasPassedShootPoint(Bullet bullet, Plant torch)
		{
			if (bullet == null)
			{
				return false;
			}
			try
			{
				IntPtr key = bullet.Pointer;
				float spawnTime;
				if (!BulletSpawnTime.TryGetValue(key, out spawnTime))
				{
					return true;
				}
				if (Time.time - spawnTime < 0.05f)
				{
					return false;
				}
				Vector2 spawnPos;
				if (!BulletSpawnPosition.TryGetValue(key, out spawnPos))
				{
					return true;
				}
				Vector3 p = bullet.transform.position;
				Vector2 delta = new Vector2(p.x, p.y) - spawnPos;
				if (delta.sqrMagnitude < 0.36f)
				{
					return false;
				}
				return delta.x >= 0.18f;
			}
			catch { return false; }
		}

		internal static void ResetLevelState()
		{
			BulletSpawnTime.Clear();
			BulletSpawnPosition.Clear();
			_boardCacheFrame = -1;
			SupernovaEndTime = 0f;
			HolyStarTorch.ResetLevelState();
		}

		// ---------------------------------------------------------------- 大招 / 流星 / 卫星

		internal static void TriggerSuperShoot(SuperSnowGatling plant)
		{
			if (plant == null)
			{
				return;
			}
			try { plant.SuperShoot(5f); }
			catch (Exception ex) { HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 触发大招失败：" + ex.Message); }
		}

		/// <summary>④/⑦ 召唤究极流星。</summary>
		internal static void TrySpawnUltimateMeteor()
		{
			try
			{
				Board board = Board.Instance;
				if (board != null)
				{
					board.CreateUltimateMateorite();
				}
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 召唤究极流星失败：" + ex.Message);
			}
		}

		/// <summary>① 在植物自身位置召唤卫星。</summary>
		internal static void SpawnSatellite(Plant owner)
		{
			try
			{
				if (owner == null)
				{
					return;
				}
				Board board = owner.board ?? Board.Instance;
				if (board == null)
				{
					return;
				}
				GameObject proto = GetSatellitePrototype();
				if (proto == null)
				{
					return;
				}
				EnsureSatelliteScale();
				GameObject go = UnityEngine.Object.Instantiate(proto);
				if (go == null)
				{
					return;
				}
				go.name = "HolyStarSatellite";
				go.transform.SetParent(board.transform, false);
				go.SetActive(true);

				ForceVisible(go);

				string anchorSource;
				Transform anchor = ResolveVisualAnchor(go, out anchorSource);

				HolyStarSatelliteBehaviour comp = go.AddComponent<HolyStarSatelliteBehaviour>();
				if (comp != null)
				{
					comp.Init(owner, HolyStarCombat.ComputeDamage(owner, HolyStarGatling.BaseDamage), anchor, _satelliteScale);
					if (HolyStarGatling.VerboseSatellite)
					{
						HolyStarGatling.LogInfo(HolyStarGatling.LogTag + " 卫星生成：缩放=" + _satelliteScale.x +
							" 视觉锚点=" + anchorSource + DescribeAnchor(anchor));
					}
					if (anchor == null)
					{
						HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 卫星层级：" + DumpChildNames(go.transform, 2));
					}
				}
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 召唤卫星失败：" + ex.Message);
			}
		}

		private static GameObject GetSatellitePrototype()
		{
			if (_satelliteProto != null)
			{
				return _satelliteProto;
			}
			if (_satelliteProtoFailed)
			{
				return null;
			}
			try
			{
				ResourcesManager rm = GameAPP.resourcesManager;
				if (rm == null || rm.plantPrefabs == null)
				{
					return null;
				}
				Il2CppSystem.Collections.Generic.Dictionary<PlantType, GameObject> table = rm.plantPrefabs;
				if (!table.ContainsKey((PlantType)HolyStarGatling.MaterialHolyStar))
				{
					_satelliteProtoFailed = true;
					HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 找不到究极星辰大帝预制体，卫星不可用");
					return null;
				}
				GameObject holyPrefab = table[(PlantType)HolyStarGatling.MaterialHolyStar];
				if (holyPrefab == null)
				{
					_satelliteProtoFailed = true;
					return null;
				}

				GameObject starWeapon = null;
				UltimateHolyStar holy = holyPrefab.GetComponent<UltimateHolyStar>();
				if (holy != null)
				{
					starWeapon = holy.starWeapon;
				}
				if (starWeapon == null)
				{
					starWeapon = FindDeep(holyPrefab.transform, "StarWeapon");
				}
				if (starWeapon == null)
				{
					_satelliteProtoFailed = true;
					HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 究极星辰大帝预制体里没有 StarWeapon，卫星不可用");
					return null;
				}
				_satelliteProto = starWeapon;
				if (HolyStarGatling.VerboseSatellite)
				{
					HolyStarGatling.LogInfo(HolyStarGatling.LogTag + " 卫星模板已就绪：" + starWeapon.name);
				}
				return _satelliteProto;
			}
			catch (Exception ex)
			{
				_satelliteProtoFailed = true;
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 取卫星模板失败：" + ex.Message);
				return null;
			}
		}

		private static void EnsureSatelliteScale()
		{
			if (_satelliteScaleResolved)
			{
				return;
			}
			_satelliteScaleResolved = true;
			try
			{
				ResourcesManager rm = GameAPP.resourcesManager;
				if (rm != null && rm.plantPrefabs != null &&
					rm.plantPrefabs.ContainsKey((PlantType)HolyStarGatling.MaterialHolyStar))
				{
					GameObject holy = rm.plantPrefabs[(PlantType)HolyStarGatling.MaterialHolyStar];
					if (holy != null && holy.transform != null)
					{
						Vector3 s = holy.transform.localScale;
						if (s.x > 0.0001f && s.y > 0.0001f)
						{
							_satelliteScale = s;
							if (HolyStarGatling.VerboseSatellite)
							{
								HolyStarGatling.LogInfo(HolyStarGatling.LogTag + " 卫星缩放取自究极星辰大帝预制体根：" + s.x);
							}
							return;
						}
					}
				}
			}
			catch { }
			_satelliteScale = new Vector3(HolyStarGatling.SatelliteFallbackScale,
				HolyStarGatling.SatelliteFallbackScale, HolyStarGatling.SatelliteFallbackScale);
			HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 取不到大帝预制体根缩放，卫星回退到 " +
				HolyStarGatling.SatelliteFallbackScale);
		}

		private static float _superChargeFxAt = -99f;

		internal static void SuperChargeStrike(Plant plant)
		{
			if (plant == null)
			{
				return;
			}
			try
			{
				Vector3 p = plant.transform.position;
				float centerX = p.x;
				float centerY = p.y + 0.5f; // 圆心取 axis + (0, 0.5)
				float r2 = HolyStarGatling.SuperChargeRadius * HolyStarGatling.SuperChargeRadius;
				int damage = ComputeDamage(plant, HolyStarGatling.BaseDamage);
				int row = plant.thePlantRow;
				int hit = 0;
				IDamageMaker maker = plant.TryCast<IDamageMaker>(); // 每拍只转型一次

				// 直接遍历 board.zombieArray（零分配）
				Board board = plant.board ?? Board.Instance;
				Il2CppSystem.Collections.Generic.List<Zombie> zombies = board != null ? board.zombieArray : null;
				if (zombies == null)
				{
					return;
				}
				int n = zombies.Count;
				for (int i = 0; i < n; i++)
				{
					Zombie zombie = zombies[i];
					if (zombie == null)
					{
						continue;
					}
					try
					{
						if (zombie.isMindControlled || zombie.beforeDying)
						{
							continue; // 只打敌方僵尸
						}
						if (Mathf.Abs(zombie.theZombieRow - row) > 1)
						{
							continue; // 只要同/邻行
						}
						Vector3 zp = zombie.transform.position;
						float dx = zp.x - centerX;
						float dy = zp.y - centerY;
						if (dx * dx + dy * dy > r2)
						{
							continue;
						}
						zombie.TakeDamage(damage, maker, DamageType.NormalAll,
							(PlantType)HolyStarGatling.PlantId, false);
						hit++;
					}
					catch { }
				}

				if (hit > 0 && Time.time - _superChargeFxAt >= HolyStarGatling.SuperChargeFxInterval)
				{
					_superChargeFxAt = Time.time;
					try { GameAPP.PlaySound(UnityEngine.Random.Range(0, 3), 0.5f, 1.0f); } catch { }
					try
					{
						ParticleManager manager = ParticleManager.Instance;
						if (manager != null)
						{
							manager.SetParticle((ParticleType)85, new Vector2(centerX, centerY), row, true, 0f);
						}
					}
					catch { }
				}
			}
			catch { }
		}

		/// <summary>⑥ 超新星视觉</summary>
		internal static GameObject SpawnRedDwarf(Vector2 center)
		{
			try
			{
				GameObject proto = GetSatellitePrototype();
				if (proto == null)
				{
					return null;
				}
				EnsureSatelliteScale();
				GameObject go = UnityEngine.Object.Instantiate(proto);
				if (go == null)
				{
					return null;
				}
				go.name = "HolyStarRedDwarf";
				go.transform.position = new Vector3(center.x, center.y, 0f);
				// 起始 0.8 倍，长到 2.0 倍
				go.transform.localScale = _satelliteScale * 0.8f;
				ForceVisible(go);

				// 染成红矮星：压绿蓝、抬红
				foreach (SpriteRenderer renderer in go.GetComponentsInChildren<SpriteRenderer>(true))
				{
					if (renderer == null)
					{
						continue;
					}
					Color c = renderer.color;
					float r = Mathf.Clamp01(c.r * 0.85f + 0.45f);
					renderer.color = new Color(r, c.g * 0.30f, c.b * 0.20f, 1f);
				}

				// 排序层：与植物/粒子同层，压在僵尸之上
				SortingGroup group = go.GetComponent<SortingGroup>();
				if (group == null)
				{
					group = go.GetComponentInChildren<SortingGroup>(true);
				}
				if (group != null)
				{
					group.sortingLayerName = "particle11";
					group.sortingOrder = 9000;
				}
				else
				{
					foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
					{
						if (renderer == null)
						{
							continue;
						}
						renderer.sortingLayerName = "particle11";
						renderer.sortingOrder = 9000;
					}
				}
				return go;
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 红矮星创建失败：" + ex.Message);
				return null;
			}
		}

		/// <summary>打开卫星子树里的渲染器</summary>
		private static void ForceVisible(GameObject root)
		{
			try
			{
				foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
				{
					if (renderer == null)
					{
						continue;
					}
					renderer.enabled = true;
					Color c = renderer.color;
					if (c.a < 0.9f)
					{
						renderer.color = new Color(c.r, c.g, c.b, 1f);
					}
				}
			}
			catch { }
		}

		/// <summary>卫星的视觉</summary>
		private static Transform ResolveVisualAnchor(GameObject root, out string source)
		{
			try
			{
				Transform direct = root.transform.Find("BigStar");
				if (direct != null)
				{
					source = "BigStar(直接子物体)";
					return direct;
				}
			}
			catch { }

			try
			{
				GameObject deep = FindDeep(root.transform, "BigStar");
				if (deep != null)
				{
					source = "BigStar(递归)";
					return deep.transform;
				}
			}
			catch { }

			try
			{
				Transform best = null;
				float bestArea = 0f;
				foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
				{
					if (renderer == null || renderer.sprite == null)
					{
						continue;
					}
					Vector3 size = renderer.bounds.size;
					float area = Mathf.Abs(size.x * size.y);
					if (area > bestArea)
					{
						bestArea = area;
						best = renderer.transform;
					}
				}
				if (best != null)
				{
					source = "最大可见贴图(" + best.name + " " + bestArea.ToString("F2") + ")";
					return best;
				}
			}
			catch { }

			source = "未找到（用原点）";
			return null;
		}

		private static string DescribeAnchor(Transform anchor)
		{
			if (anchor == null)
			{
				return "";
			}
			try
			{
				SpriteRenderer renderer = anchor.GetComponent<SpriteRenderer>();
				if (renderer == null)
				{
					return " [无 SpriteRenderer]";
				}
				Vector3 size = renderer.bounds.size;
				Color c = renderer.color;
				return " [sr=" + (renderer.enabled ? 1 : 0) + " a=" + c.a.ToString("F2") +
					" size=" + size.x.ToString("F2") + "x" + size.y.ToString("F2") +
					" active=" + (anchor.gameObject.activeInHierarchy ? 1 : 0) + "]";
			}
			catch { return ""; }
		}

		private static string DumpChildNames(Transform root, int depth)
		{
			var sb = new System.Text.StringBuilder();
			try { DumpChildNamesCore(root, 0, depth, sb); }
			catch { }
			return sb.ToString();
		}

		private static void DumpChildNamesCore(Transform node, int level, int maxDepth, System.Text.StringBuilder sb)
		{
			if (node == null || level > maxDepth)
			{
				return;
			}
			int count = node.childCount;
			for (int i = 0; i < count; i++)
			{
				Transform child = node.GetChild(i);
				if (child == null)
				{
					continue;
				}
				sb.Append(level == 0 ? "" : "  ").Append(child.name).Append("(a=")
					.Append(child.gameObject.activeSelf ? "1" : "0").Append(") ");
				if (level < maxDepth)
				{
					DumpChildNamesCore(child, level + 1, maxDepth, sb);
				}
			}
		}

		internal static GameObject FindDeep(Transform root, string name)
		{
			if (root == null)
			{
				return null;
			}
			try
			{
				if (root.name == name)
				{
					return root.gameObject;
				}
				int count = root.childCount;
				for (int i = 0; i < count; i++)
				{
					GameObject found = FindDeep(root.GetChild(i), name);
					if (found != null)
					{
						return found;
					}
				}
			}
			catch { }
			return null;
		}

		internal static void SpawnSplitBullets(Plant owner, Vector2 pos, int damage)
		{
			if (owner == null)
			{
				return;
			}
			try
			{
				CreateBullet factory = CreateBullet.Instance;
				if (factory == null)
				{
					return;
				}
				for (int i = 0; i < HolyStarGatling.SatelliteSplitBullets; i++)
				{
					if (!StarBulletMerge.AllowSpawn(owner, damage))
					{
						continue;
					}
					float angle = i * (360f / HolyStarGatling.SatelliteSplitBullets);
					Bullet bullet = factory.SetBullet(pos.x, pos.y, owner.thePlantRow,
						(BulletType)HolyStarGatling.BulletId, (BulletMoveWay)0, false);
					if (bullet == null)
					{
						continue;
					}
					try
					{
						bullet.transform.Rotate(0f, 0f, angle);
						bullet.Damage = damage;
						bullet.from = owner;
						bullet.fromType = (PlantType)HolyStarGatling.PlantId;
					}
					catch { }
					TrackBullet(bullet);
				}
			}
			catch { }
		}

		// ---------------------------------------------------------------- ⑥ 超新星

		internal static void TriggerSupernova(Plant plant)
		{
			if (plant == null)
			{
				return;
			}
			Board board = plant.board ?? Board.Instance;
			if (board == null)
			{
				return;
			}

			Vector3 p = plant.transform.position;
			Vector2 pos = new Vector2(p.x, p.y);

			// 过载湮灭流程
			HolyStarSupernova.Begin(plant, pos, plant.thePlantRow);

			try
			{
				Il2CppSystem.Collections.Generic.List<Plant> plants = Lawnf.GetAllPlants();
				if (plants != null)
				{
					int n = plants.Count;
					for (int i = 0; i < n; i++)
					{
						Plant other = plants[i];
						if (other == null)
						{
							continue;
						}
						try { other.GetShield(HolyStarGatling.SupernovaShield); }
						catch { }
					}
				}
			}
			catch { }

			// 防碾压窗口
			SupernovaEndTime = Time.time + HolyStarGatling.SupernovaDuration;
		}
	}
}
