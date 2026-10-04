using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.UI;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>开场选择面板</summary>
	internal sealed class StarBulletCapPanel : MonoBehaviour
	{
		// 注入型 MonoBehaviour 的标准构造对
		public StarBulletCapPanel() : base(ClassInjector.DerivedConstructorPointer<StarBulletCapPanel>())
		{
			ClassInjector.DerivedConstructorBody(this);
		}

		public StarBulletCapPanel(IntPtr pointer) : base(pointer)
		{
		}

		// ------------------------------------------------------------------ 静态入口

		/// <summary>当前活着的面板</summary>
		internal static StarBulletCapPanel Current;

		internal static StarBulletCapPanel Show()
		{
			if (Current != null)
			{
				return Current;
			}
			try
			{
				GameObject host = new GameObject("HolyStarCapPanel");
				UnityEngine.Object.DontDestroyOnLoad(host);
				StarBulletCapPanel panel = host.AddComponent<StarBulletCapPanel>();
				Current = panel;
				return panel;
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 上限选择面板创建失败：" + ex.Message);
				return null;
			}
		}

		// ------------------------------------------------------------------ 尺寸常量

		private const float CardW = 660f;              // 与 StarIntroArt.CardWidth 一致
		private const float CardH = 600f;              // 与 StarIntroArt.CardHeight 一致
		private const int TwinkleStars = 44;           // 背景星场
		private const int MeteorCount = 7;             // 背景流星
		private const int MeterStarCount = 24;         // 上限可视化星条
		private const float OptionW = 84f;
		private const float OptionH = 56f;
		private const float OptionGap = 8f;
		private const float AutoCloseSeconds = 30f;    // 超时自动沿用当前值
		private const float FadeSeconds = 0.45f;       // 整体淡出时长
		private const float RingOuterRadius = 430f;
		private const float RingInnerRadius = 330f;

		// 卡片两侧的植物立绘
		private const float PortraitW = 190f;
		private const float PortraitH = 172f;
		private const float PortraitX = 440f;
		private const float PortraitY = 30f;

		// 原版音效 ID
		private const int SfxOpen = 117;      // Chime：面板出现
		private const int SfxConfirm = 29;    // ButtonClick：选定某一档
		private const int SfxCancel = 28;     // GraveButton：稍后再说 / Esc / 超时
		private const int SfxToggle = 20;     // Tap2：勾选「下次不再询问」

		// ------------------------------------------------------------------ 运行时状态

		private GameObject _canvasGo;
		private RectTransform _card;
		private Image _glowRed;
		private Image _glowGold;
		private RectTransform _ringOuter;
		private RectTransform _ringInner;
		private Image _titleShine;
		private RectTransform _shineRt;
		private Text _hintText;
		private Text _toggleLabel;
		private Image _toggleBox;
		private Image _toggleTick;
		private RectTransform _burstRoot;
		private readonly List<int> _values = new List<int>();

		private readonly List<Graphic> _graphics = new List<Graphic>();
		private readonly List<Color> _fadeBase = new List<Color>();
		private readonly List<Hit> _hits = new List<Hit>();
		private readonly List<StarTwinkle> _stars = new List<StarTwinkle>();
		private readonly List<Meteor> _meteors = new List<Meteor>();
		private readonly List<Burst> _bursts = new List<Burst>();
		private readonly List<Portrait> _portraits = new List<Portrait>();
		private readonly List<Image> _meterStars = new List<Image>();

		private static Sprite _optionSprite;
		private static Sprite _boxSprite;
		private static Sprite _toggleSprite;
		private static Font _font;

		private float _t;              // 动画时间（unscaled）
		private float _openT;          // 存活时间
		private int _hoverValue = -1;  // 鼠标悬停的可选值（-1 = 没有）
		private int _currentValue;     // 打开时的生效值
		private bool _dontAskAgain;    // 「下次不再询问」
		private bool _closing;
		private float _closeT;
		private float _rivalCheckT = 999f;
		private bool _rivalCached;

		/// <summary>一条可点击区域：命中矩形 + 点击/悬停回调。</summary>
		private sealed class Hit
		{
			internal RectTransform Rt;
			internal Action OnClick;
			internal Action<bool> OnHover;
			internal bool Hovered;
		}

		private sealed class StarTwinkle
		{
			internal RectTransform Rt;
			internal Image Img;
			internal float BaseAlpha;
			internal float Speed;
			internal float Phase;
			internal float Spin;
		}

		private sealed class Meteor
		{
			internal RectTransform Rt;
			internal Image Img;
			internal Vector2 Pos;
			internal Vector2 Dir;
			internal float Speed;
		}

		private sealed class Burst
		{
			internal RectTransform Rt;
			internal Image Img;
			internal float T;
		}

		/// <summary>卡片两侧的植物立绘（自带相位，两张错开浮动）。</summary>
		private sealed class Portrait
		{
			internal RectTransform Rt;
			internal float X;
			internal float Phase;
		}

		// ------------------------------------------------------------------ 生命周期

		private void Awake()
		{
			try
			{
				BuildAll();
				Sfx(SfxOpen, 0.55f);
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 上限选择面板构建失败：" + ex.Message);
				FinishDestroy();
			}
		}

		/// <summary>播一个原版音效（整段 try/catch，失败不影响面板逻辑）。</summary>
		private static void Sfx(int soundId, float volume)
		{
			try
			{
				GameAPP.PlaySound(soundId, volume, 1f);
			}
			catch { }
		}

		private void OnDestroy()
		{
			if (Current == this)
			{
				Current = null;
			}
		}

		// ------------------------------------------------------------------ Update：动画 / 命中测试 / 键盘

		private void Update()
		{
			try
			{
				float dt = Time.unscaledDeltaTime;
				if (dt <= 0f || dt > 0.5f)
				{
					dt = 1f / 60f;
				}
				_t += dt;
				_openT += dt;

				// 别人在场就让位（只失活画布，组件仍在跑，对方收工后自己回来）
				StarBulletCapIntro.NoteFrame();
				_rivalCheckT += dt;
				if (_rivalCheckT >= 0.35f)
				{
					_rivalCheckT = 0f;
					_rivalCached = StarBulletCapIntro.RivalOverlayUp();
				}
				bool rival = _rivalCached || StarBulletCapIntro.TimeFrozenHard();
				if (_canvasGo != null && _canvasGo.activeSelf == rival)
				{
					_canvasGo.SetActive(!rival);
				}

				if (_closing)
				{
					StepBursts(dt);
					StepFade(dt);
					return;
				}

				AnimateAmbient(dt);
				StepBursts(dt);
				HandleHover();
				HandleClicks();
				HandleKeys();

				if (_openT >= AutoCloseSeconds)
				{
					BeginClose(false, _currentValue, "超时");
				}
			}
			catch (Exception ex)
			{
				// 面板自身异常绝不能带崩主菜单：记一次 Warning 后收摊
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 上限选择面板异常：" + ex.Message);
				FinishDestroy();
			}
		}

		// ------------------------------------------------------------------ 构建

		private void BuildAll()
		{
			_currentValue = StarBulletCapSettings.CurrentCap();
			List<int> values = StarBulletCapSettings.OptionValues();
			_values.Clear();
			for (int i = 0; i < values.Count; i++)
			{
				_values.Add(values[i]);
			}

			// ---- 画布：ScreenSpaceOverlay + 32766（把最上层让给别人）----
			_canvasGo = new GameObject("HolyStarCapCanvas");
			_canvasGo.transform.SetParent(transform, false);
			Canvas canvas = _canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 32766;
			CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			// 放大 = 按倍率缩小参考分辨率（字号/按钮/星场/光晕一起放大）
			float uiScale = StarBulletCapSettings.PanelScale();
			scaler.referenceResolution = new Vector2(1920f / uiScale, 1080f / uiScale);
			scaler.matchWidthOrHeight = 0.5f;

			// 模态：有 GraphicRaycaster + 全屏 raycastTarget 背板
			_canvasGo.AddComponent<GraphicRaycaster>();

			Transform root = _canvasGo.transform;

			// ---- 背板（模态遮挡 + 压暗背景）----
			RectTransform backdrop = NewUI("Backdrop", root);
			Stretch(backdrop);
			Image backdropImg = backdrop.gameObject.AddComponent<Image>();
			backdropImg.color = new Color(0.035f, 0.014f, 0.018f, 0.78f);
			backdropImg.raycastTarget = true;
			_graphics.Add(backdropImg);

			// ---- 装饰层（星场 / 流星 / 双环 / 光晕）----
			RectTransform deco = NewUI("Decoration", root);
			Stretch(deco);
			BuildGlows(deco);
			BuildRings(deco);
			BuildStarfield(deco);
			BuildMeteors(deco);

			// ---- 卡片 ----
			_card = NewUI("Card", root);
			_card.anchorMin = new Vector2(0.5f, 0.5f);
			_card.anchorMax = new Vector2(0.5f, 0.5f);
			_card.pivot = new Vector2(0.5f, 0.5f);
			_card.sizeDelta = new Vector2(CardW, CardH);
			_card.anchoredPosition = Vector2.zero;

			RectTransform border = NewUI("CardBorder", _card);
			border.sizeDelta = new Vector2(CardW + 8f, CardH + 8f);
			Image borderImg = border.gameObject.AddComponent<Image>();
			borderImg.sprite = StarIntroArt.CardBorder();
			borderImg.color = StarIntroArt.BorderDim;
			borderImg.raycastTarget = false;
			_graphics.Add(borderImg);

			RectTransform fill = NewUI("CardFill", _card);
			fill.sizeDelta = new Vector2(CardW, CardH);
			Image fillImg = fill.gameObject.AddComponent<Image>();
			fillImg.sprite = StarIntroArt.CardFill();
			fillImg.color = StarIntroArt.VoidDark;
			fillImg.raycastTarget = false;
			_graphics.Add(fillImg);

			BuildCardContent(values);

			// 两侧立绘：素材取自两株植物各自的资源包；取不到就整段跳过
			BuildPortraits(root);

			// ---- 星爆层（最后建 ⇒ 画在最上）----
			_burstRoot = NewUI("BurstLayer", _card);
			Stretch(_burstRoot);

			HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 上限选择面板已构建：当前 " + _currentValue +
				"，可选值 " + _values.Count + " 个");
		}

		private void BuildGlows(Transform parent)
		{
			_glowRed = NewUI("GlowRed", parent).gameObject.AddComponent<Image>();
			RectTransform gv = _glowRed.rectTransform;
			gv.sizeDelta = new Vector2(1500f, 1500f);
			gv.anchoredPosition = new Vector2(-120f, 40f);
			_glowRed.sprite = StarIntroArt.Glow();
			_glowRed.color = new Color(0.92f, 0.22f, 0.20f, 0.30f);
			_glowRed.raycastTarget = false;
			_graphics.Add(_glowRed);

			_glowGold = NewUI("GlowGold", parent).gameObject.AddComponent<Image>();
			RectTransform gg = _glowGold.rectTransform;
			gg.sizeDelta = new Vector2(1000f, 1000f);
			gg.anchoredPosition = new Vector2(180f, -60f);
			_glowGold.sprite = StarIntroArt.Glow();
			_glowGold.color = new Color(1f, 0.78f, 0.32f, 0.16f);
			_glowGold.raycastTarget = false;
			_graphics.Add(_glowGold);
		}

		/// <summary>卡后两圈反向旋转的星环：环上角点各放一颗小星，靠它们转出层次。</summary>
		private void BuildRings(Transform parent)
		{
			_ringOuter = NewUI("RingOuter", parent);
			_ringOuter.sizeDelta = new Vector2(RingOuterRadius * 2f, RingOuterRadius * 2f);
			AddRing(_ringOuter, RingOuterRadius, 10, StarIntroArt.Crimson, 0.13f, 15f);

			_ringInner = NewUI("RingInner", parent);
			_ringInner.sizeDelta = new Vector2(RingInnerRadius * 2f, RingInnerRadius * 2f);
			AddRing(_ringInner, RingInnerRadius, 7, StarIntroArt.Gold, 0.12f, 12f);
		}

		private void AddRing(RectTransform host, float radius, int nodes, Color color, float ringAlpha, float nodeSize)
		{
			RectTransform ring = NewUI("RingLine", host);
			ring.sizeDelta = new Vector2(radius * 2f, radius * 2f);
			Image ringImg = ring.gameObject.AddComponent<Image>();
			ringImg.sprite = StarIntroArt.Ring();
			ringImg.color = new Color(color.r, color.g, color.b, ringAlpha);
			ringImg.raycastTarget = false;
			_graphics.Add(ringImg);

			for (int i = 0; i < nodes; i++)
			{
				float ang = (Mathf.PI * 2f / nodes) * i;
				RectTransform node = NewUI("RingStar" + i, host);
				node.sizeDelta = new Vector2(nodeSize, nodeSize);
				node.anchoredPosition = new Vector2(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius);
				Image nodeImg = node.gameObject.AddComponent<Image>();
				nodeImg.sprite = StarIntroArt.Star4();
				nodeImg.color = new Color(color.r, color.g, color.b, 0.55f);
				nodeImg.raycastTarget = false;
				_graphics.Add(nodeImg);
			}
		}

		private void BuildStarfield(Transform parent)
		{
			for (int i = 0; i < TwinkleStars; i++)
			{
				RectTransform rt = NewUI("Star" + i, parent);
				float size = UnityEngine.Random.Range(8f, 26f);
				rt.sizeDelta = new Vector2(size, size);
				rt.anchoredPosition = new Vector2(
					UnityEngine.Random.Range(-1150f, 1150f),
					UnityEngine.Random.Range(-620f, 620f));

				Image img = rt.gameObject.AddComponent<Image>();
				img.sprite = StarIntroArt.Star4();
				bool warm = UnityEngine.Random.value < 0.45f;
				float r = warm ? 1f : 0.82f;
				float g = warm ? 0.85f : 0.72f;
				float b = warm ? 0.42f : 1f;
				float baseAlpha = UnityEngine.Random.Range(0.22f, 0.85f);
				img.color = new Color(r, g, b, baseAlpha);
				img.raycastTarget = false;
				_graphics.Add(img);

				rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
				_stars.Add(new StarTwinkle
				{
					Rt = rt,
					Img = img,
					BaseAlpha = baseAlpha,
					Speed = UnityEngine.Random.Range(0.7f, 2.4f),
					Phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
					Spin = UnityEngine.Random.Range(-16f, 16f)
				});
			}
		}

		private void BuildMeteors(Transform parent)
		{
			for (int i = 0; i < MeteorCount; i++)
			{
				RectTransform rt = NewUI("Meteor" + i, parent);
				float size = UnityEngine.Random.Range(0.5f, 1.15f);
				rt.sizeDelta = new Vector2(220f * size, 26f * size);
				Image img = rt.gameObject.AddComponent<Image>();
				img.sprite = StarIntroArt.Meteor();
				bool warm = UnityEngine.Random.value < 0.6f;
				img.color = warm
					? new Color(1f, 0.86f, 0.45f, UnityEngine.Random.Range(0.45f, 0.85f))
					: new Color(1f, 0.64f, 0.58f, UnityEngine.Random.Range(0.40f, 0.75f));
				img.raycastTarget = false;
				_graphics.Add(img);

				Meteor m = new Meteor { Rt = rt, Img = img };
				RespawnMeteor(m, true);
				_meteors.Add(m);
			}
		}

		private void RespawnMeteor(Meteor m, bool anywhere)
		{
			// 统一朝左下坠落，起点偏右上
			float deg = UnityEngine.Random.Range(205f, 235f);
			float rad = deg * Mathf.Deg2Rad;
			m.Dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
			m.Speed = UnityEngine.Random.Range(240f, 470f);
			m.Pos = anywhere
				? new Vector2(UnityEngine.Random.Range(-1100f, 1300f), UnityEngine.Random.Range(-500f, 700f))
				: new Vector2(UnityEngine.Random.Range(400f, 1400f), UnityEngine.Random.Range(380f, 760f));
			m.Rt.anchoredPosition = m.Pos;
			m.Rt.localRotation = Quaternion.Euler(0f, 0f, deg);
		}

		private void BuildCardContent(List<int> values)
		{
			MakeText(_card, "Title", "终焉·撰星极帝机枪射手", 34, StarIntroArt.TextMain, FontStyle.Bold,
				TextAnchor.MiddleCenter, new Vector2(0f, 248f), new Vector2(CardW - 56f, 46f));

			// 标题流光（建在标题之后 ⇒ 画在文字上面）
			_shineRt = NewUI("TitleShine", _card);
			_shineRt.sizeDelta = new Vector2(160f, 56f);
			_shineRt.anchoredPosition = new Vector2(-330f, 248f);
			_titleShine = _shineRt.gameObject.AddComponent<Image>();
			_titleShine.sprite = StarIntroArt.Shine();
			_titleShine.color = new Color(1f, 0.95f, 0.80f, 0f);
			_titleShine.raycastTarget = false;
			_graphics.Add(_titleShine);

			MakeText(_card, "Subtitle", "大帝子弹上限 · 开场设定", 20, StarIntroArt.TextSub, FontStyle.Normal,
				TextAnchor.MiddleCenter, new Vector2(0f, 208f), new Vector2(CardW - 56f, 30f));

			RectTransform divider = NewUI("Divider", _card);
			divider.sizeDelta = new Vector2(560f, 2f);
			divider.anchoredPosition = new Vector2(0f, 186f);
			Image dividerImg = divider.gameObject.AddComponent<Image>();
			dividerImg.sprite = StarIntroArt.Rounded(560, 2, 1);
			dividerImg.color = new Color(StarIntroArt.BorderBright.r, StarIntroArt.BorderBright.g, StarIntroArt.BorderBright.b, 0.45f);
			dividerImg.raycastTarget = false;
			_graphics.Add(dividerImg);

			// 正文：关闭 BestFit，写死字号 18，三段各起一行，行距 1.10
			MakeText(_card, "Body",
				"大帝系子弹（极帝星星/究极星星）数量对设备性能有要求，子弹数量堆积过多可能会导致游戏卡顿。\n" +
				"新增性能优化机制：场上大帝系子弹数量到达上限时新子弹不再生成，它的伤害会叠加到本植物下一枚子弹上（伤害不丢）。\n" +
				"上限越小越流畅、单发子弹伤害越高，上限越大越接近原版游戏体验。",
				18, StarIntroArt.TextSub, FontStyle.Normal,
				TextAnchor.UpperLeft, new Vector2(0f, 119f), new Vector2(CardW - 24f, 130f)).lineSpacing = 1.10f;

			MakeText(_card, "Section", "大帝子弹场上上限（当前生效）", 17, StarIntroArt.GoldSoft, FontStyle.Normal,
				TextAnchor.MiddleLeft, new Vector2(-6f, 34f), new Vector2(CardW - 76f, 26f));

			BuildOptionRow(values);

			// 上限可视化星条（越亮越长 = 允许的子弹越多）
			float meterTotal = MeterStarCount * 15f;
			float mx = -meterTotal * 0.5f + 7.5f;
			for (int i = 0; i < MeterStarCount; i++)
			{
				RectTransform st = NewUI("MeterStar" + i, _card);
				st.sizeDelta = new Vector2(12f, 12f);
				st.anchoredPosition = new Vector2(mx + i * 15f, -58f);
				Image si = st.gameObject.AddComponent<Image>();
				si.sprite = StarIntroArt.Star4();
				si.color = new Color(1f, 1f, 1f, 0.10f);
				si.raycastTarget = false;
				_graphics.Add(si);
				_meterStars.Add(si);
			}
			RefreshMeter(_currentValue);

			_hintText = MakeText(_card, "Hint", "", 16, StarIntroArt.TextDim, FontStyle.Normal,
				TextAnchor.MiddleCenter, new Vector2(0f, -84f), new Vector2(CardW - 60f, 26f));
			RefreshHint();

			BuildToggle();

			// 关闭按钮（沿用当前值）
			RectTransform close = NewUI("CloseButton", _card);
			close.sizeDelta = new Vector2(252f, 62f);
			close.anchoredPosition = new Vector2(0f, -186f);
			Image closeImg = close.gameObject.AddComponent<Image>();
			closeImg.sprite = StarIntroArt.ButtonSmall();
			closeImg.color = StarIntroArt.ButtonFill;
			closeImg.raycastTarget = false;
			_graphics.Add(closeImg);
			MakeText(close, "CloseLabel", "稍后再说（沿用当前值）", 20, StarIntroArt.TextMain, FontStyle.Normal,
				TextAnchor.MiddleCenter, Vector2.zero, new Vector2(240f, 32f));
			int keepValue = _currentValue;
			AddHit(close, delegate { BeginClose(false, keepValue, "玩家选择稍后再选"); },
				delegate (bool on)
				{
					if (closeImg != null)
					{
						closeImg.color = on ? StarIntroArt.ButtonFillHot : StarIntroArt.ButtonFill;
					}
				});

			MakeText(_card, "Footer",
				"左键点击数字即生效 · 1~9 键快速选择 · Esc / 30 秒后沿用当前值",
				14, StarIntroArt.TextDim, FontStyle.Normal,
				TextAnchor.MiddleCenter, new Vector2(0f, -248f), new Vector2(CardW - 40f, 22f));
		}

		private void BuildOptionRow(List<int> values)
		{
			if (values == null || values.Count == 0)
			{
				return;
			}
			float total = values.Count * OptionW + (values.Count - 1) * OptionGap;
			float startX = -total * 0.5f + OptionW * 0.5f;

			for (int i = 0; i < values.Count; i++)
			{
				int value = values[i];
				bool isCurrent = value == _currentValue;
				float x = startX + i * (OptionW + OptionGap);

				RectTransform rt = NewUI("Option" + value, _card);
				rt.sizeDelta = new Vector2(OptionW, OptionH);
				rt.anchoredPosition = new Vector2(x, -16f);

				Image bg = rt.gameObject.AddComponent<Image>();
				bg.sprite = OptionSprite();
				bg.color = isCurrent ? StarIntroArt.ConfirmFill : StarIntroArt.ButtonFill;
				bg.raycastTarget = false;
				_graphics.Add(bg);

				Text label = MakeText(rt, "Label", value + " 发", isCurrent ? 19 : 18,
					isCurrent ? StarIntroArt.Gold : StarIntroArt.TextMain, FontStyle.Bold,
					TextAnchor.MiddleCenter, Vector2.zero, new Vector2(OptionW - 4f, OptionH - 6f));

				// 当前生效值：正上方一颗小金标（不参与点击）
				if (isCurrent)
				{
					RectTransform mark = NewUI("CurrentMark", rt);
					mark.sizeDelta = new Vector2(14f, 14f);
					mark.anchoredPosition = new Vector2(0f, OptionH * 0.5f + 9f);
					Image mi = mark.gameObject.AddComponent<Image>();
					mi.sprite = StarIntroArt.Star4();
					mi.color = StarIntroArt.Gold;
					mi.raycastTarget = false;
					_graphics.Add(mi);
				}

				Color baseFill = isCurrent ? StarIntroArt.ConfirmFill : StarIntroArt.ButtonFill;
				int hovered = value;
				AddHit(rt,
					delegate { BeginClose(true, hovered, "玩家选择 " + hovered); },
					delegate (bool on)
					{
						if (bg != null)
						{
							bg.color = on ? StarIntroArt.ButtonFillHot : baseFill;
						}
						if (label != null)
						{
							label.color = (on || isCurrent) ? StarIntroArt.Gold : StarIntroArt.TextMain;
						}
						if (rt != null && rt.gameObject != null)
						{
							rt.localScale = on ? new Vector3(1.07f, 1.07f, 1f) : Vector3.one;
						}
					});
			}
		}

		// ------------------------------------------------------------------ 立绘（自家素材）

		/// <summary>卡片左右两侧各放一张植物立绘（素材直接取自两个资源包；取不到就跳过）。</summary>
		private void BuildPortraits(Transform parent)
		{
			Sprite left = StarIntroArtwork.Preview8003();
			Sprite right = StarIntroArtwork.Preview5166();
			if (left == null && right == null)
			{
				return;
			}
			AddPortrait(parent, left, "终焉·撰星极帝", 0f - PortraitX, 0f);
			AddPortrait(parent, right, "终极流星大帝", PortraitX, 2.1f);
		}

		private void AddPortrait(Transform parent, Sprite sprite, string label, float x, float phase)
		{
			if (sprite == null)
			{
				return;
			}
			RectTransform host = NewUI("Portrait_" + label, parent);
			host.anchorMin = new Vector2(0.5f, 0.5f);
			host.anchorMax = new Vector2(0.5f, 0.5f);
			host.pivot = new Vector2(0.5f, 0.5f);
			host.sizeDelta = new Vector2(PortraitW, PortraitH);
			host.anchoredPosition = new Vector2(x, PortraitY);

			// 底光：立绘与星空之间垫一层柔光
			RectTransform halo = NewUI("PortraitHalo", host);
			halo.sizeDelta = new Vector2(PortraitW * 1.7f, PortraitH * 1.7f);
			halo.anchoredPosition = Vector2.zero;
			Image haloImg = halo.gameObject.AddComponent<Image>();
			haloImg.sprite = StarIntroArt.Glow();
			haloImg.color = new Color(1f, 0.45f, 0.38f, 0.22f);
			haloImg.raycastTarget = false;
			_graphics.Add(haloImg);

			RectTransform imgRt = NewUI("PortraitImage", host);
			imgRt.sizeDelta = new Vector2(PortraitW, PortraitH);
			imgRt.anchoredPosition = Vector2.zero;
			Image img = imgRt.gameObject.AddComponent<Image>();
			img.sprite = sprite;
			// 两张预览图宽高比不同 ⇒ 必须 preserveAspect
			img.preserveAspect = true;
			img.raycastTarget = false;
			_graphics.Add(img);

			MakeText(host, "PortraitLabel", label, 18, StarIntroArt.GoldSoft, FontStyle.Bold,
				TextAnchor.MiddleCenter, new Vector2(0f, -PortraitH * 0.5f - 16f), new Vector2(240f, 26f));

			_portraits.Add(new Portrait { Rt = host, X = x, Phase = phase });
		}

		private void BuildToggle()
		{
			_dontAskAgain = false;
			RectTransform row = NewUI("ToggleRow", _card);
			row.sizeDelta = new Vector2(320f, 40f);
			row.anchoredPosition = new Vector2(-142f, -126f);

			Image rowBg = row.gameObject.AddComponent<Image>();
			rowBg.sprite = ToggleSprite();
			rowBg.color = new Color(StarIntroArt.ButtonFill.r, StarIntroArt.ButtonFill.g, StarIntroArt.ButtonFill.b, 0.75f);
			rowBg.raycastTarget = false;
			_graphics.Add(rowBg);

			RectTransform box = NewUI("Box", row);
			box.sizeDelta = new Vector2(24f, 24f);
			box.anchoredPosition = new Vector2(-136f, 0f);
			_toggleBox = box.gameObject.AddComponent<Image>();
			_toggleBox.sprite = BoxSprite();
			_toggleBox.color = new Color(0.30f, 0.10f, 0.11f, 0.95f);
			_toggleBox.raycastTarget = false;
			_graphics.Add(_toggleBox);

			RectTransform tick = NewUI("Tick", box);
			tick.sizeDelta = new Vector2(16f, 16f);
			tick.anchoredPosition = Vector2.zero;
			_toggleTick = tick.gameObject.AddComponent<Image>();
			_toggleTick.sprite = StarIntroArt.Star4();
			_toggleTick.color = new Color(StarIntroArt.Gold.r, StarIntroArt.Gold.g, StarIntroArt.Gold.b, 0f);
			_toggleTick.raycastTarget = false;
			_graphics.Add(_toggleTick);

			_toggleLabel = MakeText(row, "Label", "下次不再询问", 17, StarIntroArt.TextSub, FontStyle.Normal,
				TextAnchor.MiddleLeft, new Vector2(16f, 0f), new Vector2(240f, 28f));

			AddHit(row, ToggleDontAsk, delegate (bool on)
			{
				if (rowBg != null)
				{
					rowBg.color = new Color(StarIntroArt.ButtonFillHot.r, StarIntroArt.ButtonFillHot.g,
						StarIntroArt.ButtonFillHot.b, on ? 0.95f : 0.75f);
				}
			});
		}

		private void ToggleDontAsk()
		{
			_dontAskAgain = !_dontAskAgain;
			Sfx(SfxToggle, 0.4f);
			if (_toggleTick != null)
			{
				_toggleTick.color = new Color(StarIntroArt.Gold.r, StarIntroArt.Gold.g, StarIntroArt.Gold.b, _dontAskAgain ? 1f : 0f);
			}
			if (_toggleLabel != null)
			{
				_toggleLabel.color = _dontAskAgain ? StarIntroArt.Gold : StarIntroArt.TextSub;
			}
			if (_toggleBox != null)
			{
				_toggleBox.color = _dontAskAgain
					? new Color(0.28f, 0.20f, 0.06f, 0.98f)
					: new Color(0.30f, 0.10f, 0.11f, 0.95f);
			}
		}

		// ------------------------------------------------------------------ 每帧动画

		private void AnimateAmbient(float dt)
		{
			for (int i = 0; i < _stars.Count; i++)
			{
				StarTwinkle s = _stars[i];
				if (s.Img == null)
				{
					continue;
				}
				float a = s.BaseAlpha * (0.45f + 0.55f * Mathf.Abs(Mathf.Sin(_t * s.Speed + s.Phase)));
				Color c = s.Img.color;
				s.Img.color = new Color(c.r, c.g, c.b, a);
				if (s.Rt != null)
				{
					s.Rt.localRotation = Quaternion.Euler(0f, 0f, _t * s.Spin);
				}
			}

			for (int i = 0; i < _meteors.Count; i++)
			{
				Meteor m = _meteors[i];
				if (m.Rt == null)
				{
					continue;
				}
				m.Pos += m.Dir * (m.Speed * dt);
				if (m.Pos.x < -1400f || m.Pos.y < -760f)
				{
					RespawnMeteor(m, false);
				}
				m.Rt.anchoredPosition = m.Pos;
			}

			if (_ringOuter != null)
			{
				_ringOuter.localRotation = Quaternion.Euler(0f, 0f, _t * 7f);
			}
			if (_ringInner != null)
			{
				_ringInner.localRotation = Quaternion.Euler(0f, 0f, -_t * 11f);
			}
			if (_glowRed != null)
			{
				float k = 1f + 0.06f * Mathf.Sin(_t * 1.1f);
				_glowRed.rectTransform.localScale = new Vector3(k, k, 1f);
			}
			if (_glowGold != null)
			{
				float k = 1f + 0.08f * Mathf.Sin(_t * 1.6f + 1.2f);
				_glowGold.rectTransform.localScale = new Vector3(k, k, 1f);
			}
			if (_card != null)
			{
				float k = 1f + 0.010f * Mathf.Sin(_t * 2.2f);
				_card.localScale = new Vector3(k, k, 1f);
			}

			// 立绘轻微上下浮动（相位错开）
			for (int i = 0; i < _portraits.Count; i++)
			{
				Portrait p = _portraits[i];
				if (p.Rt == null)
				{
					continue;
				}
				float bob = Mathf.Sin(_t * 1.3f + p.Phase) * 6f;
				p.Rt.anchoredPosition = new Vector2(p.X, PortraitY + bob);
			}

			// 标题流光：从左扫到右，2.6 秒一轮
			if (_shineRt != null && _titleShine != null)
			{
				float cycle = Mathf.Repeat(_t, 2.6f) / 2.6f;
				float x = Mathf.Lerp(-330f, 330f, cycle);
				_shineRt.anchoredPosition = new Vector2(x, 248f);
				float a = Mathf.Sin(cycle * Mathf.PI) * 0.55f;
				_titleShine.color = new Color(1f, 0.95f, 0.80f, a);
			}
		}

		private void StepBursts(float dt)
		{
			for (int i = _bursts.Count - 1; i >= 0; i--)
			{
				Burst b = _bursts[i];
				if (b.Rt == null || b.Img == null)
				{
					_bursts.RemoveAt(i);
					continue;
				}
				b.T += dt;
				float k = Mathf.Clamp01(b.T / 0.5f);
				float scale = Mathf.Lerp(0.25f, 3.4f, k);
				b.Rt.localScale = new Vector3(scale, scale, 1f);
				b.Img.color = new Color(StarIntroArt.BorderBright.r, StarIntroArt.BorderBright.g,
					StarIntroArt.BorderBright.b, (1f - k) * 0.9f);
				if (k >= 1f)
				{
					UnityEngine.Object.Destroy(b.Rt.gameObject);
					_bursts.RemoveAt(i);
				}
			}
		}

		// ------------------------------------------------------------------ 交互

		private void HandleHover()
		{
			Vector2 mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
			int newHover = -1;
			// 倒序：后建的优先
			for (int i = _hits.Count - 1; i >= 0; i--)
			{
				Hit h = _hits[i];
				if (h.Rt == null)
				{
					continue;
				}
				bool inside = RectTransformUtility.RectangleContainsScreenPoint(h.Rt, mouse, (Camera)null);
				if (inside == h.Hovered)
				{
					continue;
				}
				h.Hovered = inside;
				if (h.OnHover != null)
				{
					try { h.OnHover(inside); }
					catch { }
				}
			}
			// 悬停值：给底部提示行用（选项按钮名形如 Option<值>）
			for (int i = _hits.Count - 1; i >= 0; i--)
			{
				Hit h = _hits[i];
				if (h.Rt == null || !h.Hovered)
				{
					continue;
				}
				string name = h.Rt.name;
				int v;
				if (!string.IsNullOrEmpty(name) && name.StartsWith("Option") &&
					int.TryParse(name.Substring(6), out v))
				{
					newHover = v;
					break;
				}
			}
			if (newHover != _hoverValue)
			{
				_hoverValue = newHover;
				RefreshHint();
				RefreshMeter(newHover > 0 ? newHover : _currentValue);
			}
		}

		private void HandleClicks()
		{
			if (!Input.GetMouseButtonDown(0))
			{
				return;
			}
			Vector2 mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
			for (int i = _hits.Count - 1; i >= 0; i--)
			{
				Hit h = _hits[i];
				if (h.Rt == null || h.OnClick == null)
				{
					continue;
				}
				if (RectTransformUtility.RectangleContainsScreenPoint(h.Rt, mouse, (Camera)null))
				{
					try { h.OnClick(); } catch (Exception ex) { HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 面板点击处理失败：" + ex.Message); }
					return;
				}
			}
		}

		private void HandleKeys()
		{
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				BeginClose(false, _currentValue, "玩家按 Esc 取消");
				return;
			}
			List<int> values = _values;
			for (int i = 0; i < values.Count && i < 9; i++)
			{
				KeyCode key = (KeyCode)((int)KeyCode.Alpha1 + i);
				if (Input.GetKeyDown(key))
				{
					BeginClose(true, values[i], "玩家按数字键选择 " + values[i]);
					return;
				}
			}
		}

		private void RefreshHint()
		{
			if (_hintText == null)
			{
				return;
			}
			if (_hoverValue > 0)
			{
				_hintText.text = "→ " + _hoverValue + " 发 · " + StarBulletCapSettings.HintFor(_hoverValue);
				_hintText.color = StarIntroArt.TextMain;
			}
			else
			{
				_hintText.text = "当前 " + _currentValue + " 发 · " + StarBulletCapSettings.HintFor(_currentValue);
				_hintText.color = StarIntroArt.TextDim;
			}
		}

		private void RefreshMeter(int value)
		{
			int lit = Mathf.Clamp(Mathf.RoundToInt(value / 4000f * MeterStarCount), 1, MeterStarCount);
			for (int i = 0; i < _meterStars.Count; i++)
			{
				Image si = _meterStars[i];
				if (si == null)
				{
					continue;
				}
				si.color = i < lit
					? new Color(StarIntroArt.Gold.r, StarIntroArt.Gold.g, StarIntroArt.Gold.b, 0.85f)
					: new Color(1f, 1f, 1f, 0.10f);
			}
		}

		// ------------------------------------------------------------------ 确认 / 关闭

		/// <summary>收尾：可选地写入配置，然后整体淡出销毁。applied = false 时不写配置。</summary>
		private void BeginClose(bool applied, int value, string reason)
		{
			if (_closing)
			{
				return;
			}
			_closing = true;
			_closeT = 0f;

			if (applied)
			{
				StarBulletCapSettings.ApplyCap(value);
				Sfx(SfxConfirm, 0.6f);
				if (_dontAskAgain)
				{
					StarBulletCapSettings.SetShowIntro(false);
				}
				SpawnBurstAtMouse();
				HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 开场选择：上限设为 " + value +
					(_dontAskAgain ? "，并已设为「下次不再询问」" : "") + "（" + reason + "）");
			}
			else
			{
				Sfx(SfxCancel, 0.5f);
				HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 开场选择：保持 " + _currentValue + " 不变（" + reason + "）");
			}

			SnapshotForFade();
		}

		private void SnapshotForFade()
		{
			_fadeBase.Clear();
			for (int i = 0; i < _graphics.Count; i++)
			{
				Graphic g = _graphics[i];
				_fadeBase.Add(g == null ? new Color(1f, 1f, 1f, 0f) : g.color);
			}
		}

		private void StepFade(float dt)
		{
			_closeT += dt;
			float f = 1f - Mathf.Clamp01(_closeT / FadeSeconds);
			int n = Mathf.Min(_fadeBase.Count, _graphics.Count);
			for (int i = 0; i < n; i++)
			{
				Graphic g = _graphics[i];
				if (g == null)
				{
					continue;
				}
				Color b = _fadeBase[i];
				g.color = new Color(b.r, b.g, b.b, b.a * f);
			}
			if (_closeT >= FadeSeconds)
			{
				FinishDestroy();
			}
		}

		private void SpawnBurstAtMouse()
		{
			if (_burstRoot == null)
			{
				return;
			}
			try
			{
				RectTransform rt = NewUI("Burst", _burstRoot);
				rt.sizeDelta = new Vector2(180f, 180f);
				Vector2 local;
				// 鼠标位置 → 卡片本地坐标
				Vector2 mouse = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
				if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_card, mouse, (Camera)null, out local))
				{
					local = Vector2.zero;
				}
				rt.anchoredPosition = local;
				Image img = rt.gameObject.AddComponent<Image>();
				img.sprite = StarIntroArt.Ring();
				img.color = StarIntroArt.BorderBright;
				img.raycastTarget = false;
				_graphics.Add(img);
				_bursts.Add(new Burst { Rt = rt, Img = img, T = 0f });
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 星爆特效创建失败：" + ex.Message);
			}
		}

		private void FinishDestroy()
		{
			if (Current == this)
			{
				Current = null;
			}
			try
			{
				if (_canvasGo != null)
				{
					UnityEngine.Object.Destroy(_canvasGo);
				}
			}
			catch { }
			try { UnityEngine.Object.Destroy(gameObject); }
			catch { }
		}

		// ------------------------------------------------------------------ 小工具

		private static RectTransform NewUI(string name, Transform parent)
		{
			GameObject go = new GameObject(name);
			go.transform.SetParent(parent, false);
			RectTransform rt = go.AddComponent<RectTransform>();
			rt.localScale = Vector3.one;
			rt.localRotation = Quaternion.identity;
			return rt;
		}

		private static void Stretch(RectTransform rt)
		{
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			rt.anchoredPosition = Vector2.zero;
			rt.sizeDelta = Vector2.zero;
		}

		private Text MakeText(Transform parent, string name, string content, int fontSize, Color color,
			FontStyle style, TextAnchor anchor, Vector2 pos, Vector2 size, bool bestFit = false)
		{
			RectTransform rt = NewUI(name, parent);
			rt.anchorMin = new Vector2(0.5f, 0.5f);
			rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.sizeDelta = size;
			rt.anchoredPosition = pos;

			Text t = rt.gameObject.AddComponent<Text>();
			t.font = BuiltinFont();
			t.text = content;
			t.fontSize = fontSize;
			t.fontStyle = style;
			t.alignment = anchor;
			t.color = color;
			t.raycastTarget = false;
			t.horizontalOverflow = HorizontalWrapMode.Wrap;
			t.verticalOverflow = VerticalWrapMode.Overflow;
			t.lineSpacing = 1.05f;
			if (bestFit)
			{
				// 本环境不要用 BestFit（此分支仅历史对照，正文已用显式字号）
				t.resizeTextForBestFit = true;
				t.resizeTextMinSize = 11;
				t.resizeTextMaxSize = fontSize;
			}
			_graphics.Add(t);
			return t;
		}

		private void AddHit(RectTransform rt, Action onClick, Action<bool> onHover)
		{
			_hits.Add(new Hit { Rt = rt, OnClick = onClick, OnHover = onHover, Hovered = false });
		}

		private static Font BuiltinFont()
		{
			if (_font == null)
			{
				// 游戏内建 Arial 能正常渲染中文
				_font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			}
			return _font;
		}

		private static Sprite OptionSprite()
		{
			if (_optionSprite == null)
			{
				_optionSprite = StarIntroArt.Rounded((int)OptionW, (int)OptionH, 12);
			}
			return _optionSprite;
		}

		private static Sprite BoxSprite()
		{
			if (_boxSprite == null)
			{
				_boxSprite = StarIntroArt.Rounded(24, 24, 6);
			}
			return _boxSprite;
		}

		private static Sprite ToggleSprite()
		{
			if (_toggleSprite == null)
			{
				_toggleSprite = StarIntroArt.Rounded(320, 40, 10);
			}
			return _toggleSprite;
		}
	}
}
