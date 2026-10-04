using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using MengxiLib.BepInEx;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using Il2CppObject = Il2CppSystem.Object;

namespace UltimateStarGatling.BepInEx
{
			[BepInPlugin("inf75.ultimatestargatling", "UltimateStarGatling", "1.0.0")]
	public class Core : BasePlugin
	{
		public static ManualLogSource PluginLogger { get; private set; }

		public override void Load()
		{
			Core.PluginLogger = base.Log;
			Harmony harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
			try
			{
				HolyStarGatling.ShowChargeTextConfig = Config.Bind("界面", "显示充能文本", true, "是否在植物身上显示充能数字（充能:N/1000）。改成 false 并重开游戏即可关闭。★ 游戏内的「显示植物血量」开关关闭时，充能文本会自动一起隐藏（无需改这里）。");
				HolyStarGatling.ChargeTextWorldFontSizeConfig = Config.Bind("界面", "充能文本字号（世界单位）", HolyStarGatling.DefaultChargeTextWorldFontSize,
					"充能文本的字号，单位=世界单位（关卡可视高度=10 世界单位，1 格=1.35）。默认 2.5。改完重开游戏生效。");
				HolyStarGatling.ChargeTextWorldLiftConfig = Config.Bind("界面", "充能文本上移", 0f,
					"在「紧贴血量数字下沿」的基础上再上移的世界单位数（正数向上，1 格 = 1.35）。默认 0。");
				HolyStarGatling.ChargeTextWorldShiftXConfig = Config.Bind("界面", "充能文本横向微调", 0f,
					"横向微调（世界单位，正数向右；1 格 = 1.35）。默认 0 = 对齐血量数字。");
			}
			catch { }
			try
			{
				// 性能：大帝系子弹合并（见 StarBulletMerge）
				StarBulletMerge.EnabledConfig = Config.Bind("性能", "大帝子弹合并", true,
					"场上「大帝系」子弹太多时会卡。开启后，超过上限时新子弹不再生成，攻击力改为叠加到本植物下一枚放出的子弹上（伤害不丢、场上子弹数封顶）。");
				StarBulletMerge.MaxLiveConfig = Config.Bind("性能", "大帝子弹场上上限", StarBulletMerge.DefaultMaxLive,
					"场上大帝系子弹的数量上限（两种子弹合计）。默认 500，范围 16~4000。");
				StarBulletMerge.MaxHoldSecondsConfig = Config.Bind("性能", "大帝子弹合并最长压制（秒）", StarBulletMerge.DefaultMaxHoldSeconds,
					"同一株植物被连续压制这么久后强制放出一发（带着累积的全部伤害）。范围 0.2~10。");
			}
			catch { }
			try
			{
				StarBulletCapSettings.ShowIntroConfig = Config.Bind("开场选择", "显示子弹上限选择界面", true,
					"进入主菜单后弹出一次「大帝子弹上限」选择面板（7 档数字任选）。" +
					"选择结果直接写入 [性能] 大帝子弹场上上限，立刻生效、无需重启。" +
					"面板只在别人没有全屏面板、时间没有被冻住的真空窗里出现；" +
					"面板里勾了「下次不再询问」会自动把这一项写成 false。");
				StarBulletCapSettings.OptionValuesConfig = Config.Bind("开场选择", "上限可选值", StarBulletCapSettings.DefaultOptionValues,
					"面板上摆哪几个数字（英文逗号分隔，会自动去重、升序、并夹在 16~4000 之间）。默认 100,200,300,500,800,1500,4000。");
				StarBulletCapSettings.PanelScaleConfig = Config.Bind("开场选择", "面板缩放", 1.45f,
					"开场面板整体大小（1 = 旧版，默认 1.45 = 放大 45%）。" +
					"范围 1~1.7（再大卡片两侧的植物立绘会被屏幕边缘切掉）。");
			}
			catch { }
			try
			{
				int patchedCount = 0;
				bool mouseClickPatched = false;
				foreach (MethodBase patchedMethod in harmony.GetPatchedMethods())
				{
					patchedCount++;
					string fullName = (patchedMethod.DeclaringType == null ? "?" : patchedMethod.DeclaringType.Name) + "." + patchedMethod.Name;
					if (fullName.Contains("Mouse") && fullName.Contains("LeftClick"))
					{
						mouseClickPatched = true;
					}
				}
				base.Log.LogInfo("[终焉·撰星极帝机枪射手] Harmony 补丁已注册，共 " + patchedCount +
					" 个方法（Mouse.LeftClickWithNothing 是否已挂钩：" + mouseClickPatched + "）");
			}
			catch { }
			RegisterInjectedType<UltimateStarGatlingBehaviour>();
			RegisterInjectedType<HolyStarGatlingBehaviour>();
			RegisterInjectedType<HolyStarSatelliteBehaviour>();
			RegisterInjectedType<HolyStarSupernovaSequence>();
			RegisterInjectedType<ChargeTextFollower>();
			RegisterInjectedType<StarBulletCapIntroDriver>();
			RegisterInjectedType<StarBulletCapPanel>();
			try
			{
				AssetBundle holyStarBundle = CustomCore.GetAssetBundle(Assembly.GetExecutingAssembly(), "ultimateholystargatling" + (Application.platform == RuntimePlatform.Android ? ".android" : ""));
				HolyStarGatling.Install(holyStarBundle);
				StarIntroArtwork.AttachBundles(holyStarBundle, null);
			}
			catch (Exception ex)
			{
				base.Log.LogWarning("[UltimateStarGatling] 终焉·撰星极帝机枪射手安装失败：" + ex.Message);
			}
			AssetBundle assetBundle = CustomCore.GetAssetBundle(Assembly.GetExecutingAssembly(), "ultimatestargatling" + (Application.platform == RuntimePlatform.Android ? ".android" : ""));
			if (assetBundle == null)
			{
				base.Log.LogError("[UltimateStarGatling] 无法加载 AssetBundle：ultimatestargatling");
				return;
			}
			StarIntroArtwork.AttachBundles(null, assetBundle);
			GameObject asset = Extensions.GetAsset<GameObject>(assetBundle, "UltimateStarGatlingPrefab");
			GameObject asset2 = Extensions.GetAsset<GameObject>(assetBundle, "UltimateStarGatlingPreview");
			if (asset == null || asset2 == null)
			{
				base.Log.LogError("[UltimateStarGatling] AssetBundle 需包含 UltimateStarGatlingPrefab / UltimateStarGatlingPreview");
				return;
			}
			CustomCore.RegisterCustomPlant<SuperSnowGatling, UltimateStarGatlingBehaviour>(5166, asset, asset2, new System.Collections.Generic.List<(int, int)>
			{
				(909, 1168),
				(1168, 909)
			}, 1.5f, 0f, 50, 300, 50f, 300);
			try
			{
				if (!CustomCore.TypeMgrExtra.IsMagnetPlants.Contains((PlantType)5166))
				{
					CustomCore.TypeMgrExtra.IsMagnetPlants.Add((PlantType)5166);
				}
			}
			catch (Exception ex)
			{
				ManualLogSource log = base.Log;
				BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(31, 1, out bool logFlag);
				if (logFlag)
				{
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral("[UltimateStarGatling] 注册磁力植物失败：");
					bepInExWarningLogInterpolatedStringHandler.AppendFormatted<string>(ex.Message);
				}
				log.LogWarning(bepInExWarningLogInterpolatedStringHandler);
			}
			this.BindExistingTravelUltimateBuffs();
			CustomCore.AddPlantAlmanacStrings(5166, "终极流星大帝机枪射手", "<color=#3D1400>作者：</color><color=red>梧萱梦汐X、橙源不是咸鱼</color>\n<color=#3D1400>融合配方：</color><color=red>究极杨桃大帝 + 超级机枪射手</color>\n<color=#3D1400>韧性：</color><color=red>300</color>\n<color=#3D1400>价格：</color><color=red>300</color>\n<color=#3D1400>冷却：</color><color=red>50秒</color>\n<color=#3D1400>攻击：</color><color=red>50</color>\n<color=#3D1400>攻击间隔：</color><color=red>1.5秒</color>\n\n<color=#3D1400>特点：</color>\n<color=red>①发射究极星星，每次攻击2%概率触发5秒大招，期间每0.02秒散射3发基础伤害50的究极星星；</color>\n\n<color=red>②伤害计算：（每级光照+20）×(1+磁力链接数×5%)，上限300；</color>\n\n<color=red>③直射子弹命中敌人0.5%概率召唤究极流星；</color>\n\n<color=red>④在场时，场上究极星星子弹穿过对应火炬后获得变色与加强（每颗子弹仅过火一次）：</color>\n<color=red>毁灭火炬：子弹命中赋予余烬，造成3×3范围溅射伤害，子弹偏紫。</color>\n<color=red>寒冰火炬：命中赋予寒冷状态+100寒冷值，附加3%当前生命值伤害，子弹偏蓝。</color>\n<color=red>三线火炬：伤害×3，子弹偏深黄。</color>\n<color=red>樱桃火炬：命中触发樱桃爆炸，子弹偏深红。</color>\n<color=red>普通火炬：伤害×2，子弹偏黄。</color>\n<color=red>火爆树桩：伤害×2，命中赋予辣椒红温，子弹偏红。</color>\n<color=red>超级火炬树桩：伤害×4，命中赋予辣椒红温，子弹偏红。</color>\n<color=red>究极火爆树桩：伤害×6，子弹转为樱桃窝瓜子弹。</color>\n<color=red>魅惑火炬树桩：击杀生成魅惑僵尸，子弹偏粉。</color>\n<color=red>蒜毒树桩：命中赋予5层蒜毒值，附加蒜毒值×100伤害，子弹偏灰。</color>\n\n<color=red>⑤子弹命中敌人+1充能（星澜期间不积累），满30充能点击开启20秒星澜：每次攻击连射3发，期间免疫碾压；星澜期间再点击且能量≥2000时消耗2000能量触发超新星坍缩（全场毁灭打击）。</color>\n\n<color=#3D1400>词条：</color>\n<color=red>「众星之力」1级：伤害×2且上限3000；2级：伤害×3且取消上限</color>\n<color=red>「流星雨」：攻击间隔降至0.5秒，每10秒自动开启一次大招</color>");
			ManualLogSource log2 = base.Log;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(34, 1, out bool infoLogFlag);
			if (infoLogFlag)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("[UltimateStarGatling] 已加载 PlantID=");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted<int>(5166);
			}
			log2.LogInfo(bepInExInfoLogInterpolatedStringHandler);
		}

		private static void RegisterInjectedType<T>() where T : class
		{
			try
			{
				ClassInjector.RegisterTypeInIl2Cpp<T>(new RegisterTypeOptions { LogSuccess = false });
			}
			catch (Exception ex)
			{
				Core.PluginLogger?.LogWarning("[UltimateStarGatling] 注入类型失败 " + typeof(T).Name + "：" + ex.Message);
			}
		}

		private void BindExistingTravelUltimateBuffs()
		{
			try
			{
				PlantType plantId = (PlantType)5166;
				CustomCore.AddUltimatePlant(plantId);
				object buff = (UltiBuff)8;
				object buff2 = (UltiBuff)9;
				PlantType plantType = (PlantType)909;
				if (TravelDictionary.PlantInfo.TryGetValue(plantType, out Il2CppSystem.ValueTuple<Il2CppSystem.Nullable<PlantType>, Il2CppSystem.Object, Il2CppSystem.Object, bool> valueTuple) && valueTuple.Item2 != null && valueTuple.Item3 != null)
				{
					buff = valueTuple.Item2;
					buff2 = valueTuple.Item3;
				}
				Core.EnsurePlantInfo(plantId, plantId, buff, buff2, true);
				Core.AddCustomPlantInfoBuff(plantId, (BuffType)2, 8);
				Core.AddCustomPlantInfoBuff(plantId, (BuffType)2, 9);
			}
			catch (Exception ex)
			{
				ManualLogSource log = base.Log;
								BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(33, 1, out bool bindLogFlag);
				if (bindLogFlag)
				{
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral("[UltimateStarGatling] 绑定旅行究极词条失败：");
					bepInExWarningLogInterpolatedStringHandler.AppendFormatted<string>(ex.Message);
				}
				log.LogWarning(bepInExWarningLogInterpolatedStringHandler);
			}
		}

		private static void EnsurePlantInfo(PlantType plantType, PlantType subType, object buff1, object buff2, bool isStrongUltimate)
		{
			if (!TravelDictionary.PlantInfo.ContainsKey(plantType))
			{
				Il2CppSystem.ValueTuple<Il2CppSystem.Nullable<PlantType>, Il2CppSystem.Object, Il2CppSystem.Object, bool> valueTuple = new Il2CppSystem.ValueTuple<Il2CppSystem.Nullable<PlantType>, Il2CppSystem.Object, Il2CppSystem.Object, bool>();
				valueTuple.Item1 = new Il2CppSystem.Nullable<PlantType>(subType);
				valueTuple.Item4 = isStrongUltimate;
				TravelDictionary.PlantInfo.Add(plantType, valueTuple);
			}
			Il2CppSystem.ValueTuple<Il2CppSystem.Nullable<PlantType>, Il2CppSystem.Object, Il2CppSystem.Object, bool> valueTuple2 = TravelDictionary.PlantInfo[plantType];
			if (buff1 != null)
			{
				valueTuple2.Item2 = Core.CoerceUltiBuffObject(buff1);
			}
			if (buff2 != null)
			{
				valueTuple2.Item3 = Core.CoerceUltiBuffObject(buff2);
			}
			valueTuple2.Item4 = isStrongUltimate;
			TravelDictionary.PlantInfo[plantType] = valueTuple2;
			if (!TravelDictionary.allStrongUltimtePlant.Contains(plantType))
			{
				TravelDictionary.allStrongUltimtePlant.Add(plantType);
			}
		}

				private static Il2CppSystem.Object CoerceUltiBuffObject(object buff)
		{
			if (buff is Il2CppObject il2Obj)
			{
				return il2Obj;
			}
			int num = buff is UltiBuff ultiBuff ? (int)ultiBuff : System.Convert.ToInt32(buff);
			return Il2CppSystem.Enum.Parse(Il2CppType.From(typeof(UltiBuff)), num.ToString());
		}

		private static void AddCustomPlantInfoBuff(PlantType plantType, BuffType buffType, int buffId)
		{
			System.ValueTuple<BuffType, int> item = new System.ValueTuple<BuffType, int>(buffType, buffId);
			System.Collections.Generic.List<System.ValueTuple<BuffType, int>> list;
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
			CustomCore.CustomPlantInfo.Add(plantType, new System.Collections.Generic.List<System.ValueTuple<BuffType, int>>
			{
				item
			});
		}

		public const int PlantId = 5166;

		public const int RecipeUltimateStar = 909;

		public const int RecipeSuperPeaGatling = 1168;

		public const int BaseAttackDamage = 50;

		public const int SuperShootBaseDamage = 50;

		public const int MaxBulletDamageCap = 300;

		public const int MaxBulletDamageCapWithBuffLv1 = 3000;

		public const float ChargeCostStarMode = 30f;

		public const float StarModeDurationSeconds = 20f;

		public const float SupernovaEnergyCost = 2000f;

		public static readonly Color SupernovaTintColor = new Color(0.82f, 0.28f, 1f, 1f);

		public const float SupernovaBrightness = 1.75f;

		public const float MeteorProcChance = 0.005f;

		public const float MeteorShowerAutoUltInterval = 10f;

		public const int TravelBuffSourcePlantId = 909;

		public const UltiBuff TravelBuffMeteorShower = (UltiBuff)8;

		public const UltiBuff TravelBuffStarPower = (UltiBuff)9;
	}
}
