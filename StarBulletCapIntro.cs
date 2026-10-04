using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>开场选择的配置与写入层。</summary>
	internal static class StarBulletCapSettings
	{
		internal static ConfigEntry<bool> ShowIntroConfig;
		internal static ConfigEntry<string> OptionValuesConfig;

		/// <summary>开场面板整体缩放。</summary>
		internal static ConfigEntry<float> PanelScaleConfig;

		internal const string DefaultOptionValues = "100,200,300,500,800,1500,4000";
		internal const int MinCap = 16;
		internal const int MaxCap = 4000;

		/// <summary>「下次不再询问」落点</summary>
		internal static bool ShowIntro
		{
			get
			{
				try { return ShowIntroConfig == null || ShowIntroConfig.Value; }
				catch { return true; }
			}
		}

		internal static void SetShowIntro(bool on)
		{
			try
			{
				if (ShowIntroConfig != null)
				{
					ShowIntroConfig.Value = on;
				}
			}
			catch { }
		}

		/// <summary>面板缩放的合法区间。</summary>
		internal const float MinPanelScale = 1f;
		internal const float MaxPanelScale = 1.7f;

		/// <summary>取面板缩放</summary>
		internal static float PanelScale()
		{
			float k = 1.45f;
			try
			{
				if (PanelScaleConfig != null)
				{
					k = PanelScaleConfig.Value;
				}
			}
			catch { }
			if (k < MinPanelScale)
			{
				k = MinPanelScale;
			}
			else if (k > MaxPanelScale)
			{
				k = MaxPanelScale;
			}
			return k;
		}

		/// <summary>当前生效的上限</summary>
		internal static int CurrentCap()
		{
			try { return StarBulletMerge.MaxLive; }
			catch { return StarBulletMerge.DefaultMaxLive; }
		}

		/// <summary>把玩家选的值写回 [性能] 大帝子弹场上上限</summary>
		internal static void ApplyCap(int value)
		{
			try
			{
				if (value < MinCap)
				{
					value = MinCap;
				}
				else if (value > MaxCap)
				{
					value = MaxCap;
				}
				if (StarBulletMerge.MaxLiveConfig != null)
				{
					StarBulletMerge.MaxLiveConfig.Value = value;
				}
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 写入大帝子弹上限失败：" + ex.Message);
			}
		}

		/// <summary>解析「上限可选值」</summary>
		internal static List<int> OptionValues()
		{
			var result = new List<int>();
			try
			{
				string raw = OptionValuesConfig == null ? DefaultOptionValues : OptionValuesConfig.Value;
				if (!string.IsNullOrEmpty(raw))
				{
					string[] parts = raw.Split(new char[] { ',', '，', ';', '；', ' ' }, StringSplitOptions.RemoveEmptyEntries);
					for (int i = 0; i < parts.Length; i++)
					{
						int v;
						if (!int.TryParse(parts[i].Trim(), out v))
						{
							continue;
						}
						if (v < MinCap)
						{
							v = MinCap;
						}
						else if (v > MaxCap)
						{
							v = MaxCap;
						}
						if (!result.Contains(v))
						{
							result.Add(v);
						}
					}
				}
			}
			catch { }
			if (result.Count == 0)
			{
				string[] fallback = DefaultOptionValues.Split(',');
				for (int i = 0; i < fallback.Length; i++)
				{
					int v;
					if (int.TryParse(fallback[i], out v))
					{
						result.Add(v);
					}
				}
			}
			result.Sort();
			return result;
		}

		/// <summary>解释。</summary>
		internal static string HintFor(int value)
		{
			if (value <= 150)
			{
				return "极致流畅：场上星星很少，伤害并入下一枚";
			}
			if (value <= 350)
			{
				return "流畅优先：明显压制星星数量";
			}
			if (value <= 700)
			{
				return "推荐 · 均衡：只在刷屏时介入";
			}
			if (value <= 1600)
			{
				return "接近原版手感：几乎不触发合并";
			}
			return "几乎不限制：卡顿时再回来调小";
		}
	}

	/// <summary>开场选择引导器</summary>
	internal static class StarBulletCapIntro
	{
		/// <summary>整局只弹一次</summary>
		private static bool _shown;

		/// <summary>已有驱动器在等真空窗</summary>
		private static bool _pending;

		/// <summary>HengMingModsLib 是否装载</summary>
		internal static bool HengMingInstalled;

		/// <summary>时间被冻住的起始 realtime 时刻（&lt; 0 = 当前没冻）。</summary>
		private static float _frozenSince = -1f;

		/// <summary>连续冻住超过这么久就判定不是别人在弹面板，不再据此让位。</summary>
		internal const float FrozenGraceSeconds = 10f;

		/// <summary>每帧调一次（用 realtime 记时，幂等）。</summary>
		internal static void NoteFrame()
		{
			try
			{
				if (Time.timeScale <= 0.0001f)
				{
					if (_frozenSince < 0f)
					{
						_frozenSince = Time.realtimeSinceStartup;
					}
				}
				else
				{
					_frozenSince = -1f;
				}
			}
			catch { }
		}

		/// <summary>时间正被冻住且还在宽限期内（= 值得让位）。</summary>
		internal static bool TimeFrozenHard()
		{
			try
			{
				if (Time.timeScale > 0.0001f)
				{
					return false;
				}
				return _frozenSince < 0f || Time.realtimeSinceStartup - _frozenSince < FrozenGraceSeconds;
			}
			catch { return false; }
		}

		/// <summary>冻结已超宽限。</summary>
		internal static bool FrozenGraceExceeded()
		{
			try
			{
				return Time.timeScale <= 0.0001f && _frozenSince >= 0f &&
					Time.realtimeSinceStartup - _frozenSince >= FrozenGraceSeconds;
			}
			catch { return false; }
		}

		/// <summary>给面板用的「该让位了」判据。</summary>
		internal static bool RivalOverlayUp()
		{
			try
			{
				// ① 时间被冻住
				if (Time.timeScale <= 0.0001f)
				{
					return true;
				}
				// 只在对手模组真的装了时才按名查
				if (!HengMingInstalled)
				{
					return false;
				}
				// ② 它的面板对象在场上
				if (GameObject.Find("LoadingPanel") != null)
				{
					return true;
				}
				if (GameObject.Find("SpeedSelectPanel") != null)
				{
					return true;
				}
				// ③ 它的加载收尾标志
				return HengMingLoading();
			}
			catch { return false; }
		}

		/// <summary>游戏自己有没有弹窗盖在主菜单之上（UI 栈层数 &gt; 1 ⇔ 还盖着）。</summary>
		internal static bool GamePopupUp()
		{
			try
			{
				UIResourcesLoader ui = GameAPP.UIManager;
				if (ui == null)
				{
					return false;
				}
				var stack = ui.menuStack;
				if (stack == null)
				{
					return false;
				}
				return stack.Count > 1;
			}
			catch { return false; }
		}

		/// <summary>顶层窗口的描述（只用于日志）。</summary>
		internal static string TopMenuDesc()
		{
			try
			{
				UIResourcesLoader ui = GameAPP.UIManager;
				if (ui == null)
				{
					return "UIManager=null";
				}
				var stack = ui.menuStack;
				if (stack == null)
				{
					return "menuStack=null";
				}
				int layers = stack.Count;
				if (layers <= 0)
				{
					return "层数=" + layers;
				}
				var top = stack.Peek();
				if (top == null || top.Count <= 0)
				{
					return "层数=" + layers + "，顶层为空";
				}
				BaseMenu menu = top[0];
				if (menu == null)
				{
					return "层数=" + layers + "，顶层[0]=null";
				}
				return "层数=" + layers + "，顶层 UIType=" + menu.uIType + "(" + (int)menu.uIType + ")";
			}
			catch (Exception ex)
			{
				return "读取失败（" + ex.GetType().Name + "）";
			}
		}

		private static FieldInfo _hengMingInitEnd;
		private static bool _hengMingProbed;

		/// <summary>HengMingModsLib 是否还在加载（InitEnd == false）。没装 ⇒ false。</summary>
		private static bool HengMingLoading()
		{
			try
			{
				if (!_hengMingProbed)
				{
					_hengMingProbed = true;
					Type type = Type.GetType("HengMingModsLib.InitMods, HengMingModsLib", false);
					if (type == null)
					{
						Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
						for (int i = 0; i < assemblies.Length; i++)
						{
							try
							{
								if (assemblies[i].GetName().Name == "HengMingModsLib")
								{
									type = assemblies[i].GetType("HengMingModsLib.InitMods", false);
									break;
								}
							}
							catch { }
						}
					}
					if (type != null)
					{
						_hengMingInitEnd = type.GetField("InitEnd", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
					}
					HengMingInstalled = type != null;
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 开场选择：对手模组 HengMingModsLib = " +
						(HengMingInstalled ? "已安装（会按它的面板名让位）" : "未安装（不做按名让位，只保留时间闸）"));
				}
				if (_hengMingInitEnd == null)
				{
					return false;
				}
				object value = _hengMingInitEnd.GetValue(null);
				return value is bool && !(bool)value;
			}
			catch { return false; }
		}

		internal static void Boot()
		{
			if (_shown)
			{
				return;
			}
			try
			{
				if (!StarBulletCapSettings.ShowIntro)
				{
					_shown = true;
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 开场选择：配置「显示子弹上限选择界面」= false，本次会话不再询问");
					return;
				}
				if (_pending || StarBulletCapPanel.Current != null)
				{
					return;
				}
				_pending = true;
				ProbeHengMing();
				var host = new GameObject("HolyStarCapIntro");
				UnityEngine.Object.DontDestroyOnLoad(host);
				host.AddComponent<StarBulletCapIntroDriver>();
				HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 开场选择：已触发（timeScale=" +
					Time.timeScale.ToString("0.###") + "，游戏状态=" + GameAPP.theGameStatus + "）");
			}
			catch (Exception ex)
			{
				_pending = false;
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 开场选择引导启动失败：" + ex.Message);
			}
		}

		/// <summary>驱动器收工回报：弹出过就锁死整局只弹一次。</summary>
		internal static void NoteDriverFinished(bool showed)
		{
			_pending = false;
			if (showed)
			{
				_shown = true;
			}
		}

		/// <summary>触发一次对手模组探测。</summary>
		private static void ProbeHengMing()
		{
			if (!_hengMingProbed)
			{
				HengMingLoading();
			}
		}

		/// <summary>进主菜单的钩子。</summary>
		[HarmonyPatch(typeof(UIMgr), "EnterMainMenu")]
		internal static class UIMgr_EnterMainMenu_Intro
		{
			[HarmonyPostfix]
			private static void Postfix()
			{
				Boot();
			}
		}
	}

	/// <summary>门口看门的小驱动器：条件满足就挂出面板，然后自我销毁。</summary>
	internal sealed class StarBulletCapIntroDriver : MonoBehaviour
	{
		public StarBulletCapIntroDriver() : base(ClassInjector.DerivedConstructorPointer<StarBulletCapIntroDriver>())
		{
			ClassInjector.DerivedConstructorBody(this);
		}

		public StarBulletCapIntroDriver(IntPtr pointer) : base(pointer)
		{
		}

		/// <summary>等不到出场时机的兜底上限（秒）；超时放弃本次引导。</summary>
		private const float GiveUpAfter = 300f;
		/// <summary>游戏弹窗还没关时的绝对上限（秒）；超过就放弃本次引导。</summary>
		private const float PopupHardCapSeconds = 900f;

		private float _elapsed;
		private bool _done;
		private float _rivalT = 999f;
		private bool _rivalCached;
		private bool _frozenGraceLogged;
		private bool _loggedFirstFrame;
		/// <summary>驱动器启动时刻（realtime）。</summary>
		private float _startRealtime = -1f;
		/// <summary>是否已打过「正在等游戏公告」。</summary>
		private bool _popupLogged;
		/// <summary>是否已打过「游戏公告已关闭」。</summary>
		private bool _popupClosedLogged;

		private void Update()
		{
			if (_done)
			{
				return;
			}
			try
			{
				float dt = Time.unscaledDeltaTime;
				if (dt <= 0f || dt > 0.5f)
				{
					dt = 1f / 60f;
				}
				if (_startRealtime < 0f)
				{
					_startRealtime = Time.realtimeSinceStartup;
				}
				_elapsed += dt;

				// 时间闸记账
				StarBulletCapIntro.NoteFrame();

				if (!_loggedFirstFrame)
				{
					_loggedFirstFrame = true;
					// 这几行是排查现场，默认不打
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 开场选择：驱动器首帧（timeScale=" +
						Time.timeScale.ToString("0.###") + "，游戏状态=" + GameAPP.theGameStatus +
						"，Board=" + (Board.Instance != null ? "已存在" : "null") + "，" +
						StarBulletCapIntro.TopMenuDesc() + "）");
				}

				// 已进关卡就不再弹（判据必须是「状态 InGame 且 Board 存在」）
				if (GameAPP.theGameStatus == GameStatus.InGame && Board.Instance != null)
				{
					Finish(false, "玩家已进入关卡（Board 已存在）");
					return;
				}

				if (!_frozenGraceLogged && StarBulletCapIntro.FrozenGraceExceeded())
				{
					_frozenGraceLogged = true;
					HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 开场选择：时间已被冻住超过 " +
						StarBulletCapIntro.FrozenGraceSeconds.ToString("0") + " 秒，判定为不是别人在弹面板，继续弹出");
				}

				// 游戏弹窗（开局公告）要等玩家关掉再弹；这段等待不计入 GiveUpAfter
				if (StarBulletCapIntro.GamePopupUp())
				{
					_elapsed = 0f;
					if (!_popupLogged)
					{
						_popupLogged = true;
						HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 开场选择：正在等玩家关闭游戏公告（" +
							StarBulletCapIntro.TopMenuDesc() + "）");
					}
					if (Time.realtimeSinceStartup - _startRealtime >= PopupHardCapSeconds)
					{
						Finish(false, "游戏公告一直没关（超过 " + PopupHardCapSeconds.ToString("0") + " 秒）");
					}
					return;
				}
				if (_popupLogged && !_popupClosedLogged)
				{
					_popupClosedLogged = true;
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 开场选择：游戏公告已关闭（已让台 " +
						(Time.realtimeSinceStartup - _startRealtime).ToString("0.0") + " 秒），立即弹出");
				}

				// timeScale 每帧查（带 10 秒宽限）；RivalOverlayUp 的两次按名查找只在对手装了时才执行
				_rivalT += dt;
				if (_rivalT >= 0.2f)
				{
					_rivalT = 0f;
					_rivalCached = StarBulletCapIntro.RivalOverlayUp();
				}
				bool rival = _rivalCached || StarBulletCapIntro.TimeFrozenHard();

				if (rival)
				{
					if (_elapsed >= GiveUpAfter)
					{
						Finish(false, "等不到出场时机（别人面板一直占着？）");
					}
					return;
				}

				// 四道闸全放行 ⇒ 当帧就弹
				StarBulletCapPanel.Show();
				Finish(true, null);
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 开场选择轮询异常：" + ex.Message);
				Finish(false, null);
			}
		}

		/// <summary>自触发起的总时长（秒）。</summary>
		private float TotalSeconds()
		{
			if (_startRealtime < 0f)
			{
				return 0f;
			}
			try { return Time.realtimeSinceStartup - _startRealtime; }
			catch { return 0f; }
		}

		private void Finish(bool showed, string why)
		{
			_done = true;
			StarBulletCapIntro.NoteDriverFinished(showed);
			// 每次会话最多一行
			if (showed)
			{
				HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 开场选择：已弹出（自触发起共 " +
					TotalSeconds().ToString("0.0") + " 秒）");
			}
			else
			{
				// 失败路径保留 Info
				HolyStarGatling.LogInfo(HolyStarGatling.LogTag + " 开场选择：本次不弹出（" + (why ?? "未知") +
					"，自触发起共 " + TotalSeconds().ToString("0.0") + " 秒）");
			}
			try { UnityEngine.Object.Destroy(gameObject); }
			catch { }
		}
	}
}
