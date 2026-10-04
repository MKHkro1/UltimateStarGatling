using System;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>开场选择界面的程序化美术</summary>
	internal static class StarIntroArt
	{
		// ---------------------------------------------------------------- 配色

		/// <summary>卡片底板</summary>
		internal static readonly Color VoidDark = new Color(0.086f, 0.030f, 0.038f, 0.94f);
		/// <summary>描边·金</summary>
		internal static readonly Color BorderBright = new Color(1f, 0.84f, 0.35f, 0.95f);
		/// <summary>描边·红</summary>
		internal static readonly Color BorderDim = new Color(0.94f, 0.34f, 0.30f, 0.85f);
		/// <summary>亮红</summary>
		internal static readonly Color Crimson = new Color(1f, 0.36f, 0.30f, 1f);
		/// <summary>深红（备用）</summary>
		internal static readonly Color CrimsonDeep = new Color(0.46f, 0.17f, 0.14f, 0.95f);
		internal static readonly Color Gold = new Color(1f, 0.84f, 0.35f, 1f);
		internal static readonly Color GoldSoft = new Color(0.93f, 0.74f, 0.30f, 0.92f);
		internal static readonly Color TextMain = new Color(0.98f, 0.95f, 0.94f, 1f);
		internal static readonly Color TextSub = new Color(0.95f, 0.84f, 0.80f, 0.96f);
		internal static readonly Color TextDim = new Color(0.84f, 0.72f, 0.69f, 0.9f);
		/// <summary>警示文字</summary>
		internal static readonly Color TextWarn = new Color(1f, 0.72f, 0.42f, 1f);
		internal static readonly Color ButtonFill = new Color(0.30f, 0.09f, 0.10f, 0.92f);
		internal static readonly Color ButtonFillHot = new Color(0.52f, 0.16f, 0.17f, 0.98f);
		internal static readonly Color ConfirmFill = new Color(0.30f, 0.20f, 0.06f, 0.96f);
		internal static readonly Color ConfirmFillHot = new Color(0.48f, 0.33f, 0.10f, 1f);

		// ---------------------------------------------------------------- 缓存

		private static Sprite _cardFill;
		private static Sprite _cardBorder;
		private static Sprite _glow;
		private static Sprite _ring;
		private static Sprite _star4;
		private static Sprite _circle;
		private static Sprite _meteor;
		private static Sprite _shine;
		private static Sprite _btnWide;
		private static Sprite _btnSmall;

		// 卡片与按钮尺寸集中定义
		internal const float CardWidth = 660f;
		/// <summary>卡片高度。</summary>
		internal const float CardHeight = 600f;

		internal static Sprite CardFill()
		{
			if (_cardFill == null)
			{
				_cardFill = Rounded((int)CardWidth, (int)CardHeight, 26);
			}
			return _cardFill;
		}

		internal static Sprite CardBorder()
		{
			if (_cardBorder == null)
			{
				_cardBorder = Rounded((int)CardWidth + 8, (int)CardHeight + 8, 29);
			}
			return _cardBorder;
		}

		internal static Sprite Glow()
		{
			if (_glow == null)
			{
				_glow = Radial(256, 1.8f);
			}
			return _glow;
		}

		internal static Sprite Ring()
		{
			if (_ring == null)
			{
				_ring = RingMask(256);
			}
			return _ring;
		}

		internal static Sprite Star4()
		{
			if (_star4 == null)
			{
				_star4 = Star4Mask(128);
			}
			return _star4;
		}

		internal static Sprite Circle()
		{
			if (_circle == null)
			{
				_circle = CircleMask(96);
			}
			return _circle;
		}

		internal static Sprite Meteor()
		{
			if (_meteor == null)
			{
				_meteor = MeteorMask(220, 26);
			}
			return _meteor;
		}

		internal static Sprite Shine()
		{
			if (_shine == null)
			{
				_shine = ShineMask(160, 64);
			}
			return _shine;
		}

		internal static Sprite ButtonWide()
		{
			if (_btnWide == null)
			{
				_btnWide = Rounded(184, 58, 14);
			}
			return _btnWide;
		}

		internal static Sprite ButtonSmall()
		{
			if (_btnSmall == null)
			{
				_btnSmall = Rounded(252, 62, 16);
			}
			return _btnSmall;
		}

		// ---------------------------------------------------------------- 蒙版生成

		private static Sprite Bake(int w, int h, Func<float, float, float> alphaFn)
		{
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			tex.wrapMode = TextureWrapMode.Clamp;
			tex.filterMode = FilterMode.Bilinear;
			Color[] px = new Color[w * h];
			for (int y = 0; y < h; y++)
			{
				float v = (h <= 1) ? 0.5f : (float)y / (h - 1);
				for (int x = 0; x < w; x++)
				{
					float u = (w <= 1) ? 0.5f : (float)x / (w - 1);
					float a = alphaFn(u, v);
					if (a < 0f)
					{
						a = 0f;
					}
					else if (a > 1f)
					{
						a = 1f;
					}
					px[y * w + x] = new Color(1f, 1f, 1f, a);
				}
			}
			tex.SetPixels(px);
			tex.Apply();
			return Sprite.Create(tex, new Rect(0f, 0f, (float)w, (float)h), new Vector2(0.5f, 0.5f), 100f);
		}

		/// <summary>圆角矩形</summary>
		internal static Sprite Rounded(int w, int h, int radius)
		{
			return Bake(w, h, delegate (float u, float v)
			{
				float dx = Mathf.Max(0f, Mathf.Abs(u - 0.5f) * w - ((float)w * 0.5f - radius));
				float dy = Mathf.Max(0f, Mathf.Abs(v - 0.5f) * h - ((float)h * 0.5f - radius));
				float d = Mathf.Sqrt(dx * dx + dy * dy);
				return Mathf.Clamp01((float)radius - d + 0.5f);
			});
		}

		/// <summary>实心圆（边缘软化 1.5px）</summary>
		internal static Sprite CircleMask(int size)
		{
			return Bake(size, size, delegate (float u, float v)
			{
				float dx = (u - 0.5f) * 2f;
				float dy = (v - 0.5f) * 2f;
				float d = Mathf.Sqrt(dx * dx + dy * dy);
				return Mathf.Clamp01((1f - d) * (float)size * 0.5f);
			});
		}

		/// <summary>中心亮、边缘透明的径向光晕</summary>
		internal static Sprite Radial(int size, float power)
		{
			return Bake(size, size, delegate (float u, float v)
			{
				float dx = (u - 0.5f) * 2f;
				float dy = (v - 0.5f) * 2f;
				float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
				return Mathf.Pow(1f - d, power);
			});
		}

		/// <summary>扩散冲击波用的环</summary>
		internal static Sprite RingMask(int size)
		{
			return Bake(size, size, delegate (float u, float v)
			{
				float dx = (u - 0.5f) * 2f;
				float dy = (v - 0.5f) * 2f;
				float d = Mathf.Sqrt(dx * dx + dy * dy);
				float ring = Mathf.Exp(-Mathf.Pow((d - 0.74f) / 0.11f, 2f));
				float core = Mathf.Exp(-Mathf.Pow(d / 0.16f, 2f)) * 0.25f;
				return Mathf.Clamp01(ring + core) * Mathf.Clamp01(1.15f - d);
			});
		}

		/// <summary>四角星光</summary>
		internal static Sprite Star4Mask(int size)
		{
			return Bake(size, size, delegate (float u, float v)
			{
				float dx = (u - 0.5f) * 2f;
				float dy = (v - 0.5f) * 2f;
				float d = Mathf.Sqrt(dx * dx + dy * dy);
				float ang = Mathf.Atan2(dy, dx);
				float petals = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang)), 3f);
				float core = Mathf.Clamp01(1f - d / 0.30f) * 0.85f;
				float arms = petals * Mathf.Clamp01(1f - d) * 0.95f;
				return Mathf.Clamp01(core + arms);
			});
		}

		/// <summary>流星拖尾：右端点头部、向左淡出</summary>
		internal static Sprite MeteorMask(int w, int h)
		{
			return Bake(w, h, delegate (float u, float v)
			{
				float head = Mathf.Pow(u, 1.5f);
				float band = Mathf.Exp(-Mathf.Pow((v - 0.5f) / 0.26f, 2f));
				return head * band;
			});
		}

		/// <summary>标题流光扫过用的细长高光带</summary>
		internal static Sprite ShineMask(int w, int h)
		{
			return Bake(w, h, delegate (float u, float v)
			{
				float band = Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.16f, 2f));
				float fade = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 1.6f);
				return band * fade;
			});
		}
	}
}
