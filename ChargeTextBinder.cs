using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using TMPro;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>充能文本绑定器</summary>
	internal static class ChargeTextBinder
	{
		private static bool _logged;

		private static float _bodyAnchorAt = -99f;
		private static bool _bodyAnchorValid;
		private static float _bodyAnchorX;
		private static float _bodyAnchorTopY;
		private const float BodyAnchorCacheSeconds = 0.5f;

		private static readonly List<Bounds> BodyBounds = new List<Bounds>(32);

		internal static void ResetLog()
		{
			_logged = false;
		}

		internal static TextMeshPro Create(Plant plant, Color color)
		{
			if (plant == null || !HolyStarGatling.ShowChargeTextEnabled)
			{
				return null;
			}
			try
			{
				Transform parent = plant.transform;
				if (parent == null)
				{
					return null;
				}
				float parentScale = ResolveParentScale(parent);
				float inv = 1f / parentScale;

				// 字号按世界单位给
				float worldEm = HolyStarGatling.ChargeTextWorldFontSize;

				GameObject go = new GameObject("HolyStarChargeText");
				go.transform.SetParent(parent, false);
				go.transform.localScale = new Vector3(inv, inv, inv);
				go.transform.localPosition = Vector3.zero;

				TextMeshPro text = go.AddComponent<TextMeshPro>();
				text.font = GameAPP.font;
				text.fontSize = worldEm;
				text.alignment = TextAlignmentOptions.Center;
				text.color = color;
				text.outlineWidth = 0.2f;
				text.outlineColor = Color.black;
				text.raycastTarget = false;
				// 关闭自动换行，允许溢出
				text.enableWordWrapping = false;
				try { text.overflowMode = TextOverflowModes.Overflow; }
				catch { }
				MeshRenderer renderer = text.GetComponent<MeshRenderer>();
				if (renderer != null)
				{
					renderer.sortingLayerName = "particle11";
					renderer.sortingOrder = 10000;
				}

				// 每帧跟随血量数字正下方
				ChargeTextFollower follower = go.AddComponent<ChargeTextFollower>();
				follower.Bind(text, plant, renderer);
				follower.Tick();

				if (!_logged)
				{
					_logged = true;
					HealthSlider slider = null;
					Vector3 target;
					bool fromNumber = TryGetNumberAnchor(plant, ref slider, out target);
					Vector3 local = parent.InverseTransformPoint(target);
					float templateEm = ResolveTemplateWorldEm(plant);
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 充能文本绑定：字号（世界单位）=" +
						worldEm.ToString("0.###") + "，模板反推世界字高=" + templateEm.ToString("0.####") +
						"（仅诊断，不再用于字号），植物根缩放=" + parentScale.ToString("0.####") +
						"，锚点来源=" + (fromNumber ? "血量数字" : "本体聚类兜底") +
						"，锚点顶部世界=(" + target.x.ToString("0.###") + "," + target.y.ToString("0.###") + ")" +
						"，本地落点=(" + local.x.ToString("0.###") + "," + local.y.ToString("0.###") + ")");
				}
				return text;
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 自建充能文本失败：" + ex.Message);
				return null;
			}
		}

		private static float ResolveParentScale(Transform parent)
		{
			try
			{
				float s = Mathf.Abs(parent.lossyScale.y);
				if (s > 0.0001f)
				{
					return s;
				}
			}
			catch { }
			return 1f;
		}

		/// <summary>锚点</summary>
		internal static bool TryGetNumberAnchor(Plant plant, ref HealthSlider cachedSlider, out Vector3 target)
		{
			target = Vector3.zero;
			try
			{
				if (cachedSlider == null)
				{
					cachedSlider = plant.healthSlider;
				}
				if (cachedSlider != null && cachedSlider.healthText != null)
				{
					Vector3 p = cachedSlider.healthText.transform.position;
					target = new Vector3(
						p.x + HolyStarGatling.ChargeTextWorldShiftX,
						p.y - HolyStarGatling.ChargeTextDropBelowNumber + HolyStarGatling.ChargeTextWorldLift,
						p.z);
					return true;
				}
			}
			catch { }

			try
			{
				Vector3 origin = plant.transform.position;
				if (ResolveBodyAnchorCached(plant, out float centerX, out float topY))
				{
					target = new Vector3(
						centerX + HolyStarGatling.ChargeTextWorldShiftX,
						topY - HolyStarGatling.ChargeTextDropFromArtTop + HolyStarGatling.ChargeTextWorldLift,
						origin.z);
					return true;
				}
			}
			catch { }
			return false;
		}

		/// <summary>血条模板渲染出的世界字高</summary>
		private static float ResolveTemplateWorldEm(Plant plant)
		{
			try
			{
				HealthSlider slider = plant.healthSlider;
				if (slider != null && slider.healthText != null)
				{
					TextMeshProUGUI template = slider.healthText;
					return Mathf.Abs(template.fontSize * template.transform.lossyScale.y);
				}
			}
			catch { }
			return 0f;
		}

		/// <summary>兜底锚点</summary>
		private static bool ResolveBodyAnchorCached(Plant plant, out float centerX, out float topY)
		{
			centerX = 0f;
			topY = 0f;
			float now;
			try { now = Time.time; }
			catch { now = _bodyAnchorAt; }
			if (_bodyAnchorValid && now - _bodyAnchorAt < BodyAnchorCacheSeconds)
			{
				centerX = _bodyAnchorX;
				topY = _bodyAnchorTopY;
				return true;
			}

			int kept;
			bool ok = ResolveBodyAnchor(plant, out centerX, out topY, out kept);
			_bodyAnchorAt = now;
			_bodyAnchorValid = ok;
			_bodyAnchorX = centerX;
			_bodyAnchorTopY = topY;
			return ok;
		}

		private static bool ResolveBodyAnchor(Plant plant, out float centerX, out float topY, out int kept)
		{
			centerX = 0f;
			topY = 0f;
			kept = 0;
			try
			{
				List<Bounds> list = BodyBounds;
				list.Clear();
				foreach (SpriteRenderer renderer in plant.GetComponentsInChildren<SpriteRenderer>(true))
				{
					if (renderer == null || !renderer.enabled || renderer.sprite == null)
					{
						continue;
					}
					Color color = renderer.color;
					if (color.a < 0.5f)
					{
						continue;
					}
					string name = renderer.gameObject.name;
					if (name.StartsWith("HolyStar"))
					{
						continue;
					}
					list.Add(renderer.bounds);
				}
				if (list.Count == 0)
				{
					return false;
				}
				int seed = 0;
				float bestArea = -1f;
				for (int i = 0; i < list.Count; i++)
				{
					float area = list[i].size.x * list[i].size.y;
					if (area > bestArea)
					{
						bestArea = area;
						seed = i;
					}
				}
				Vector3 seedCenter = list[seed].center;
				float areaSum = 0f;
				float weightedX = 0f;
				float maxY = float.MinValue;
				for (int i = 0; i < list.Count; i++)
				{
					Vector3 c = list[i].center;
					if (Mathf.Abs(c.x - seedCenter.x) > 2.0f || Mathf.Abs(c.y - seedCenter.y) > 3.0f)
					{
						continue;
					}
					float area = Mathf.Max(0.0001f, list[i].size.x * list[i].size.y);
					areaSum += area;
					weightedX += c.x * area;
					maxY = Mathf.Max(maxY, list[i].max.y);
					kept++;
				}
				if (kept == 0 || areaSum <= 0f)
				{
					return false;
				}
				centerX = weightedX / areaSum;
				topY = maxY;
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

	/// <summary>让充能文本每帧贴在血量数字正下方</summary>
	public class ChargeTextFollower : MonoBehaviour
	{
		public ChargeTextFollower() : base(ClassInjector.DerivedConstructorPointer<ChargeTextFollower>())
		{
			ClassInjector.DerivedConstructorBody(this);
		}

		public ChargeTextFollower(IntPtr pointer) : base(pointer)
		{
		}

		private TextMeshPro _text;
		private MeshRenderer _renderer;
		private Plant _plant;
		private HealthSlider _slider;
		private float _halfHeightWorld = -1f;
		/// <summary>血量文本容器</summary>
		private GameObject _healthContainer;

		internal void Bind(TextMeshPro text, Plant plant, MeshRenderer renderer)
		{
			_text = text;
			_plant = plant;
			_renderer = renderer;
			_slider = null;
			_healthContainer = null;
			_halfHeightWorld = -1f;
		}

		/// <summary>是否应显示本文本：与游戏的「显示血量」开关同进同出。</summary>
		private bool ResolveVisible()
		{
			if (!HolyStarGatling.ShowChargeTextEnabled)
			{
				return false; // 配置里关掉了充能文本
			}
			try
			{
				if (_healthContainer != null)
				{
					return _healthContainer.activeInHierarchy;
				}
			}
			catch { }
			try
			{
				Board board = _plant != null ? _plant.board : null;
				if (board != null && board.showPlantHealth == 0)
				{
					return false;
				}
			}
			catch { }
			return true;
		}

		/// <summary>血量容器一经就位就不再变，只取一次。</summary>
		private void CacheHealthContainer()
		{
			if (_healthContainer != null || _slider == null)
			{
				return;
			}
			try
			{
				RectTransform container = _slider.healthTextContainer;
				if (container != null)
				{
					_healthContainer = container.gameObject;
				}
			}
			catch { }
		}

		/// <summary>按当前锚点重算一次本地位置，并同步「显示血量」开关。</summary>
		internal void Tick()
		{
			try
			{
				if (_text == null || _plant == null)
				{
					return;
				}
				Transform parent = _plant.transform;
				if (parent == null)
				{
					return;
				}

				// 先同步可见性
				if (_renderer == null)
				{
					try { _renderer = _text.GetComponent<MeshRenderer>(); }
					catch { }
				}
				bool visible = ResolveVisible();
				if (_renderer != null && _renderer.enabled != visible)
				{
					// 只关渲染器，不关 GameObject
					_renderer.enabled = visible;
				}
				if (!visible)
				{
					return;
				}

				Vector3 top;
				if (!ChargeTextBinder.TryGetNumberAnchor(_plant, ref _slider, out top))
				{
					return;
				}
				CacheHealthContainer();
				Vector3 localTop = parent.InverseTransformPoint(top);

				// 文本世界缩放已归一到 1
				float plantScale = 1f;
				try
				{
					Vector3 s = _text.transform.localScale;
					plantScale = (s.x > 0.0001f) ? 1f / s.x : 1f;
				}
				catch { }
				float halfWorld = ResolveHalfHeightWorld();
				_text.transform.localPosition = new Vector3(localTop.x, localTop.y - halfWorld / plantScale, 0f);
			}
			catch { }
		}

		/// <summary>文本自身一半高度（世界单位），用渲染包围盒量</summary>
		private float ResolveHalfHeightWorld()
		{
			try
			{
				if (_renderer != null)
				{
					float extents = _renderer.bounds.extents.y;
					if (extents > 0.0005f)
					{
						_halfHeightWorld = extents;
						return extents;
					}
				}
			}
			catch { }
			if (_halfHeightWorld > 0f)
			{
				return _halfHeightWorld;
			}
			// 首帧按字号估一半，下一帧被真实包围盒覆盖
			return Mathf.Max(0.05f, HolyStarGatling.ChargeTextWorldFontSize * 0.5f);
		}

		private void Update()
		{
			Tick();
		}
	}
}
