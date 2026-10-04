using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using MengxiLib.BepInEx;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>终焉·撰星极帝机枪射手（UltimateHolyStarGatling）—— 注册与常量。</summary>
	internal static class HolyStarGatling
	{
		internal const int PlantId = 8003;

		internal const int MaterialHolyStar = 311;        // 究极星辰大帝
		internal const int MaterialSuperGatling = 1168;   // 超级机枪射手
		internal const int MaterialMeteorGatling = 5166;  // 终极流星大帝机枪射手（流星大哥）
		/// <summary>究极杨桃大帝（909）。</summary>
		internal const int MaterialStarFruitEmperor = 909;

		internal const int BaseDamage = 300;              // 每发基础伤害
		internal const float AttackInterval = 1.5f;       // 攻击间隔（6 发/轮）
		internal const int MaxHealth = 300;               // 韧性
		internal const float CardCooldown = 30f;
		internal const int SunCost = 300;

		internal const int BulletId = 249;                // Bullet_star_holy

		internal const float MeteorProcChance = 0.03f;    // ④ 命中 3% 召唤究极流星
		internal const float SatelliteInterval = 5f;      // ① 每 5 秒一颗卫星
		internal const float SatelliteLifetime = 4f;      // 卫星 4 秒后在正前方爆炸

		internal const float SupernovaChargeCost = 1000f; // ⑥ 点击消耗 1000 充能
		internal const float SupernovaShield = 4000f;     // ⑥ 全场 4000 护盾
		internal const float SupernovaDuration = 5f;      // ⑥ 防碾压/护盾持续
		internal const int SupernovaHits = 6;             // ⑥ 旧版"单发伤害 ×6"（已被原版湮灭流程取代，保留备用）

		// ⑥ 超新星 = 原版究极路灯花过载湮灭同款流程
		internal const float SupernovaAbsorbSeconds = 3f;   // 相位A：吸走全屏非领袖僵尸
		internal const int SupernovaWaveCount = 100;        // 相位B：100 波毁灭打击
		internal const int SupernovaDamage = 1000000;       // 每波每只僵尸的伤害
		internal const float SupernovaWaveInterval = 0.02f; // 每波间隔
		/// <summary>同时处于被吸走动画中的僵尸上限。</summary>
		internal const int SupernovaSuctionMax = 24;
		/// <summary>单个僵尸被吸走的表现时长（秒）。</summary>
		internal const float SupernovaSuctionSeconds = 0.35f;

		/// <summary>⑥ 超充能阶段阈值：充能 ≥ 1 万进入超充能。</summary>
		internal const float SuperChargeThreshold = 10000f;
		/// <summary>过载阈值。</summary>
		internal const float SuperChargeOverload = 100000f;
		/// <summary>超充能节拍。</summary>
		internal const float SuperChargeTick = 0.02f;
		/// <summary>超充能范围打击半径。</summary>
		internal const float SuperChargeRadius = 2.5f;
		/// <summary>每拍自增 = 当前充能 / 该除数。</summary>
		internal const float SuperChargeGainDivisor = 2000f;
		/// <summary>超充能音效/粒子节流。</summary>
		internal const float SuperChargeFxInterval = 0.25f;
		/// <summary>超充能亮度强度上限。</summary>
		internal const float SuperChargeMaxBrightness = 4f;

		internal const float MeteorShowerAutoInterval = 10f; // ③ 流星雨：每 10 秒自动大招
		internal const int DefaultDamageCap = 1800;       // 无词条时的伤害上限
		internal const int StarPowerCapLv1 = 3000;        // 众星之力 1 级上限

		internal const int LightDamagePerLevel = 20;      // ③ 每级光照 +20
		internal const float MagnetDamagePerLink = 0.05f;  // ③ 每点磁力链接 +5%

		/// <summary>卫星子树回退缩放（取不到 311 预制体根缩放时用）。</summary>
		internal const float SatelliteFallbackScale = 0.32f;

		internal const float SatelliteOrbitRadius = 4.05f; // 半径 3 格（列宽 1.35）
		/// <summary>环绕角速度（度/秒）。</summary>
		internal const float SatelliteOrbitSpeed = 180f;
		/// <summary>轨迹纵向系数：1 = 正圆。</summary>
		internal const float SatelliteOrbitVerticalFactor = 1f;
		/// <summary>中心贴图自转（度/秒）。</summary>
		internal const float SatelliteCenterSpinSpeed = 60f;
		/// <summary>外围星星自转（度/秒）。</summary>
		internal const float SatelliteRingSpinSpeed = -200f;

		/// <summary>① 持续攻击的起始延迟：种下后 4 秒内不强制全场开火。</summary>
		internal const float AttackWarmupSeconds = 4f;

		/// <summary>卫星出现时的淡入时长（秒）。</summary>
		internal const float SatelliteFadeInSeconds = 0.4f;

		/// <summary>充能文本顶部相对美术包围盒顶部的下移量。</summary>
		internal const float ChargeTextDropFromArtTop = 0.45f;
		/// <summary>文本顶部相对血量数字中心的下移量（世界单位）。</summary>
		internal const float ChargeTextDropBelowNumber = 0.35f;
		/// <summary>取不到美术包围盒时的兜底美术顶部高度。</summary>
		internal const float ChargeTextFallbackArtTop = 2.0f;
		/// <summary>传给前置库 RegisterText 的框尺寸（仅占位）。</summary>
		internal static readonly Vector2 ChargeTextBoxSize = new Vector2(200f, 50f);

		/// <summary>充能文本字号（世界单位）。</summary>
		internal const float DefaultChargeTextWorldFontSize = 2.5f;

		/// <summary>⑦ 复活次数上限。</summary>
		internal const int MaxRevives = 1;

		/// <summary>「充能文本字号（世界单位）」配置项（由 Core.Load 注入）。</summary>
		internal static ConfigEntry<float> ChargeTextWorldFontSizeConfig;
		/// <summary>「充能文本上移」配置项。</summary>
		internal static ConfigEntry<float> ChargeTextWorldLiftConfig;
		/// <summary>「充能文本横向微调」配置项。</summary>
		internal static ConfigEntry<float> ChargeTextWorldShiftXConfig;

		/// <summary>当前生效的充能文本字号（世界单位）。</summary>
		internal static float ChargeTextWorldFontSize
		{
			get
			{
				try
				{
					float v = ChargeTextWorldFontSizeConfig == null ? DefaultChargeTextWorldFontSize : ChargeTextWorldFontSizeConfig.Value;
					return v > 0.05f ? v : DefaultChargeTextWorldFontSize;
				}
				catch { return DefaultChargeTextWorldFontSize; }
			}
		}

		/// <summary>当前生效的上移量（世界单位）。</summary>
		internal static float ChargeTextWorldLift
		{
			get
			{
				try { return ChargeTextWorldLiftConfig == null ? 0f : ChargeTextWorldLiftConfig.Value; }
				catch { return 0f; }
			}
		}

		/// <summary>当前生效的横向微调（世界单位）。</summary>
		internal static float ChargeTextWorldShiftX
		{
			get
			{
				try { return ChargeTextWorldShiftXConfig == null ? 0f : ChargeTextWorldShiftXConfig.Value; }
				catch { return 0f; }
			}
		}

		/// <summary>「显示充能文本」配置项。</summary>
		internal static ConfigEntry<bool> ShowChargeTextConfig;

		/// <summary>充能文本是否显示。</summary>
		internal static bool ShowChargeTextEnabled
		{
			get
			{
				try { return ShowChargeTextConfig == null || ShowChargeTextConfig.Value; }
				catch { return true; }
			}
		}

		/// <summary>一格列宽（世界单位）。</summary>
		internal const float CellWidth = 1.35f;
		/// <summary>卫星亡语爆炸相对植物中心的横向偏移（格）。</summary>
		internal const float SatelliteExplodeOffsetCells = 3f;

		/// <summary>卫星诊断日志总开关（必须 static readonly，const false 会造成 CS0162）。</summary>
		internal static readonly bool VerboseSatellite = false;
		internal const float SatelliteContactRadius = 1.0f;
		internal const float SatelliteContactInterval = 0.02f;
		internal const float SatelliteContactRatio = 0.3f;  // 接触伤害 = 攻击力 30%
		internal const float SatelliteExplodeRadius = 2.0f;
		internal const int SatelliteSplitBullets = 5;       // 分裂 5 发 1 倍攻击力

		internal const UltiBuff BuffMeteorShower = (UltiBuff)8; // 流星雨
		internal const UltiBuff BuffStarPower = (UltiBuff)9;    // 众星之力

		/// <summary>④/⑦ 召唤究极流星时的日志标签。</summary>
		internal const string LogTag = "[终焉·撰星极帝机枪射手]";

		internal static void LogInfo(string msg)
		{
			try { Core.PluginLogger?.LogInfo(msg); } catch { }
		}

		internal static void LogWarn(string msg)
		{
			try { Core.PluginLogger?.LogWarning(msg); } catch { }
		}

		/// <summary>运行期诊断日志总开关（默认关；必须 static readonly，const false 会造成 CS0162）。</summary>
		internal static readonly bool VerboseRuntimeLog = false;

		/// <summary>运行期诊断日志。</summary>
		internal static void LogVerbose(string msg)
		{
			if (!VerboseRuntimeLog)
			{
				return;
			}
			LogInfo(msg);
		}

		/// <summary>在 Core.Load 里拿到 AB 后调用。</summary>
		internal static void Install(AssetBundle ab)
		{
			if (ab == null)
			{
				LogWarn(LogTag + " 无法加载 AssetBundle：ultimateholystargatling");
				return;
			}

			GameObject prefab = Extensions.GetAsset<GameObject>(ab, "UltimateHolyStarGatlingPrefab");
			GameObject preview = Extensions.GetAsset<GameObject>(ab, "UltimateHolyStarGatlingPreview");
			if (prefab == null || preview == null)
			{
				LogWarn(LogTag + " AssetBundle 需包含 UltimateHolyStarGatlingPrefab / UltimateHolyStarGatlingPreview");
				return;
			}

			EnsureAxis(prefab);

			// 融合配方：311 + 1168，或 5166 + 909（正反序都写）
			var fusions = new List<(int, int)>
			{
				(MaterialHolyStar, MaterialSuperGatling),              // 311 + 1168
				(MaterialSuperGatling, MaterialHolyStar),              // 1168 + 311
				(MaterialMeteorGatling, MaterialStarFruitEmperor),     // 5166 + 909
				(MaterialStarFruitEmperor, MaterialMeteorGatling)      // 909 + 5166
			};

			try
			{
				CustomCore.RegisterCustomPlant<SuperSnowGatling, HolyStarGatlingBehaviour>(
					PlantId, prefab, preview, fusions,
					AttackInterval, 0f, BaseDamage, MaxHealth, CardCooldown, SunCost);
			}
			catch (Exception ex)
			{
				LogWarn(LogTag + " 注册植物失败：" + ex.Message);
				return;
			}

			RegisterMagnet();
			RegisterAlmanac();
			BindTravelBuffs();

			LogInfo(LogTag + " 已加载 PlantID=" + PlantId + "，子弹=" + BulletId + "（Bullet_star_holy）");
		}

		/// <summary>③ 磁力系统：让本植物能入磁力链。</summary>
		private static void RegisterMagnet()
		{
			try
			{
				if (!CustomCore.TypeMgrExtra.IsMagnetPlants.Contains((PlantType)PlantId))
				{
					CustomCore.TypeMgrExtra.IsMagnetPlants.Add((PlantType)PlantId);
				}
			}
			catch (Exception ex)
			{
				LogWarn(LogTag + " 注册磁力植物失败：" + ex.Message);
			}
		}

		/// <summary>③ 词条：与 5166 共用「流星雨」「众星之力」两条旅行究极词条。</summary>
		private static void BindTravelBuffs()
		{
			try
			{
				PlantType plantId = (PlantType)PlantId;
				CustomCore.AddUltimatePlant(plantId);

				object buff1 = (UltiBuff)8;
				object buff2 = (UltiBuff)9;
				PlantType source = (PlantType)MaterialMeteorGatling;
				Il2CppSystem.ValueTuple<Il2CppSystem.Nullable<PlantType>, Il2CppSystem.Object, Il2CppSystem.Object, bool> tuple;
				if (TravelDictionary.PlantInfo.TryGetValue(source, out tuple) && tuple.Item2 != null && tuple.Item3 != null)
				{
					buff1 = tuple.Item2;
					buff2 = tuple.Item3;
				}

				EnsurePlantInfo(plantId, plantId, buff1, buff2);
				AddCustomPlantInfoBuff(plantId, (BuffType)2, 8);
				AddCustomPlantInfoBuff(plantId, (BuffType)2, 9);
			}
			catch (Exception ex)
			{
				LogWarn(LogTag + " 绑定旅行究极词条失败：" + ex.Message);
			}
		}

		private static void EnsurePlantInfo(PlantType plantType, PlantType subType, object buff1, object buff2)
		{
			if (!TravelDictionary.PlantInfo.ContainsKey(plantType))
			{
				var tuple = new Il2CppSystem.ValueTuple<Il2CppSystem.Nullable<PlantType>, Il2CppSystem.Object, Il2CppSystem.Object, bool>();
				tuple.Item1 = new Il2CppSystem.Nullable<PlantType>(subType);
				TravelDictionary.PlantInfo.Add(plantType, tuple);
			}

			var entry = TravelDictionary.PlantInfo[plantType];
			if (buff1 != null)
			{
				entry.Item2 = CoerceUltiBuff(buff1);
			}
			if (buff2 != null)
			{
				entry.Item3 = CoerceUltiBuff(buff2);
			}
			entry.Item4 = true;
			TravelDictionary.PlantInfo[plantType] = entry;

			if (!TravelDictionary.allStrongUltimtePlant.Contains(plantType))
			{
				TravelDictionary.allStrongUltimtePlant.Add(plantType);
			}
		}

		private static Il2CppSystem.Object CoerceUltiBuff(object buff)
		{
			if (buff is Il2CppSystem.Object il2Obj)
			{
				return il2Obj;
			}
			int num = buff is UltiBuff ultiBuff ? (int)ultiBuff : Convert.ToInt32(buff);
			return Il2CppSystem.Enum.Parse(Il2CppType.From(typeof(UltiBuff)), num.ToString());
		}

		private static void AddCustomPlantInfoBuff(PlantType plantType, BuffType buffType, int buffId)
		{
			var item = new ValueTuple<BuffType, int>(buffType, buffId);
			List<ValueTuple<BuffType, int>> list;
			if (CustomCore.CustomPlantInfo.TryGetValue(plantType, out list))
			{
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i].Item1 == buffType && list[i].Item2 == buffId)
					{
						return;
					}
				}
				list.Add(item);
				return;
			}
			CustomCore.CustomPlantInfo.Add(plantType, new List<ValueTuple<BuffType, int>> { item });
		}

		/// <summary>Entity.Awake 会 transform.Find("axis")，找不到就抛异常；预制体缺它，加载期补一个空节点。</summary>
		private static void EnsureAxis(GameObject root)
		{
			try
			{
				if (root == null || root.transform.Find("axis") != null)
				{
					return;
				}
				var axis = new GameObject("axis");
				axis.transform.SetParent(root.transform, false);
				axis.transform.localPosition = Vector3.zero;
				LogInfo(LogTag + " 预制体缺少 axis 子物体，已补建空节点");
			}
			catch (Exception ex)
			{
				LogWarn(LogTag + " 补建 axis 失败：" + ex.Message);
			}
		}

		private static void RegisterAlmanac()
		{
			string text =
				"<color=#3D1400>作者：</color><color=red>梧萱梦汐X、橙源是咸鱼</color>\n" +
				"<color=#3D1400>融合配方：</color><color=red>究极星辰大帝 + 超级机枪射手 / 终极流星大帝机枪射手 + 究极杨桃大帝</color>\n" +
				"<color=#3D1400>阳光：</color><color=red>" + SunCost + "</color>\n" +
				"<color=#3D1400>卡槽冷却：</color><color=red>" + CardCooldown + " 秒</color>\n" +
				"<color=#3D1400>韧性：</color><color=red>" + MaxHealth + "</color>\n" +
				"<color=#3D1400>伤害：</color><color=red>" + BaseDamage + "×6/" + AttackInterval + "秒</color>\n" +
				"<color=#3D1400>索敌范围：</color><color=red>全场</color>\n\n" +
				"<color=#3D1400>特点：</color>\n" +
				"<color=red>①无论场上是否有敌人都会持续攻击（无敌人的情况下不会触发大招）；每 " + SatelliteInterval +
				" 秒在自身位置召唤一颗究极星辰大帝同款卫星：卫星绕本体半径 3 格环绕自转，接触敌人造成 30% 攻击力的持续伤害" +
				"并分裂 5 发星星，4 秒后在前方 3 格爆炸（爆炸伤害 = 期间累积的接触伤害）。</color>\n" +
				"<color=red>②每次攻击有 2% 概率触发大招：5 秒内每 0.02 秒散射 3 发伤害 " + BaseDamage +
				" 的极帝星星。</color>\n" +
				"<color=red>③与究极星辰大帝一样受光照与磁力系统加强（每级光照 +" + LightDamagePerLevel +
				"、每点磁力链接 +" + (int)(MagnetDamagePerLink * 100) + "%），无词条时伤害上限 " + DefaultDamageCap +
				"；词条「众星之力」1 级：伤害×2 且上限 " + StarPowerCapLv1 + "；2 级及以上：伤害×3 并取消上限；" +
				"词条「流星雨」：每 " + MeteorShowerAutoInterval + " 秒自动开启一次大招。</color>\n" +
				"<color=red>④攻击命中敌人时有 " + (int)(MeteorProcChance * 100) + "% 概率召唤究极流星并释放一颗卫星。</color>\n" +
				"<color=red>⑤在场时，大帝星星与极帝星星穿过火炬会获得加强（每颗子弹仅过火一次）：</color>\n" +
				"<color=red>毁灭火炬：命中赋予余烬并造成 3×3 范围溅射；寒冰火炬：命中赋予寒冷与冰冻值并附带当前生命 3% 伤害；" +
				"三线火炬：伤害×3；樱桃火炬：命中触发樱桃爆炸；普通火炬：伤害×2；" +
				"火爆树桩：伤害×2 并赋予辣椒红温；超级火炬树桩：伤害×4 并赋予辣椒红温；" +
				"究极火爆树桩：伤害×6 并赋予辣椒红温、子弹转为樱桃窝瓜子弹；" +
				"魅惑火炬树桩：击杀生成魅惑僵尸；蒜毒树桩：命中赋予 5 层蒜毒并附加蒜毒值×100 伤害。</color>\n" +
				"<color=red>⑥命中敌人积累 1 点充能（无上限）；点击本体消耗 " + (int)SupernovaChargeCost +
				" 点充能释放一次超新星爆炸，期间为全场植物提供防碾压与 " + (int)SupernovaShield + " 护盾；充能达到 " +
				(int)SuperChargeThreshold + " 时进入超充能（本体亮度提升、每 0.02 秒对周围 " + SuperChargeRadius +
				" 格内同/邻行僵尸造成一次伤害，且每 0.02 秒额外获得当前充能的 1/2000），充能达到 " +
				(int)SuperChargeOverload + " 时自动释放一次同样的过载湮灭并清空充能。</color>\n" +
				"<color=red>⑦第 1 次死亡（非铲除）后化作一颗究极流星重返战场，并重置全部状态。</color>";

			try
			{
				CustomCore.AddPlantAlmanacStrings(PlantId, "终焉·撰星极帝机枪射手", text);
			}
			catch (Exception ex)
			{
				LogWarn(LogTag + " 写入图鉴失败：" + ex.Message);
			}
		}
	}
}
