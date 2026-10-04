using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using TMPro;
using UnityEngine;

namespace UltimateStarGatling.BepInEx
{
			public class UltimateStarGatlingBehaviour : MonoBehaviour
	{
		public UltimateStarGatlingBehaviour() : base(ClassInjector.DerivedConstructorPointer<UltimateStarGatlingBehaviour>())
		{
			ClassInjector.DerivedConstructorBody(this);
		}

		public UltimateStarGatlingBehaviour(System.IntPtr pointer) : base(pointer)
		{
		}

				public SuperSnowGatling Plant
		{
						get
			{
				if (this._plant == null || !this._plant)
				{
					this._plant = base.gameObject.GetComponent<SuperSnowGatling>();
				}
				return this._plant;
			}
		}

		internal static UltimateStarGatlingBehaviour TryGetBehaviour(Plant plant)
		{
			if (plant == null || plant.thePlantType !=(PlantType)5166)
			{
				return null;
			}
			UltimateStarGatlingBehaviour result;
			try
			{
				UltimateStarGatlingBehaviour ultimateStarGatlingBehaviour;
				result = (UltimateStarGatlingBehaviour.ActiveBehaviours.TryGetValue(plant.Pointer, out ultimateStarGatlingBehaviour) ? ultimateStarGatlingBehaviour : null);
			}
			catch
			{
				result = null;
			}
			return result;
		}

		public bool IsStarModeActive
		{
			get
			{
				return this.StarModeEndTime > Time.time;
			}
		}

		internal static bool IsUncrashableInStarMode(Plant plant)
		{
			UltimateStarGatlingBehaviour ultimateStarGatlingBehaviour = UltimateStarGatlingBehaviour.TryGetBehaviour(plant);
			return ultimateStarGatlingBehaviour != null && ultimateStarGatlingBehaviour.IsStarModeActive;
		}

		private void RegisterBehaviour()
		{
			try
			{
				SuperSnowGatling plant = this.Plant;
				if (plant != null)
				{
					UltimateStarGatlingBehaviour.ActiveBehaviours[plant.Pointer] = this;
				}
			}
			catch
			{
			}
		}

		private void UnregisterBehaviour()
		{
			try
			{
				SuperSnowGatling plant = this.Plant;
				if (plant != null)
				{
					UltimateStarGatlingBehaviour.ActiveBehaviours.Remove(plant.Pointer);
				}
			}
			catch
			{
			}
		}

		private void Awake()
		{
			try
			{
				SuperSnowGatling plant = this.Plant;
				if (!(plant == null))
				{
					this.RegisterBehaviour();
					Transform transform = base.transform.Find("GatlingPea_head");
					if (transform == null)
					{
						transform = base.transform.GetChild(0);
					}
					Transform transform2 = (transform != null) ? transform.Find("Shoot") : base.transform.Find("Shoot");
					if (transform2 != null)
					{
						plant.shoot = transform2;
					}
				}
			}
			catch
			{
			}
		}

		private void Start()
		{
			this.RegisterBehaviour();
			this.TryInitializeChargeText();
		}

		private void OnDestroy()
		{
			this.UnregisterBehaviour();
			this.DestroyFallbackChargeText();
		}

		private void Update()
		{
			SuperSnowGatling plant = this.Plant;
			if (plant == null || plant.dying || plant.thePlantHealth <= 0)
			{
				return;
			}
			float starModeEndTime = this.StarModeEndTime;
			if (starModeEndTime > 0f && starModeEndTime <= Time.time)
			{
				this.StarModeEndTime = 0f;
				this._displayedCharge = int.MinValue;
			}
			this._meteorBuffRecheckTimer -= Time.deltaTime;
			if (this._meteorBuffRecheckTimer <= 0f)
			{
				this._meteorBuffRecheckTimer = ((this._hasMeteorShowerBuff || starModeEndTime > Time.time) ? 0.5f : 2f);
				this._hasMeteorShowerBuff = UltimateStarGatlingCombat.HasMeteorShowerBuff();
			}
			if (this._hasMeteorShowerBuff)
			{
				plant.thePlantAttackInterval = 0.5f;
				this.MeteorShowerTimer += Time.deltaTime;
				if (this.MeteorShowerTimer >= 10f)
				{
					this.MeteorShowerTimer = 0f;
					UltimateStarGatlingCombat.TriggerSuperShootTimer(plant);
				}
			}
			else
			{
				this.MeteorShowerTimer = 0f;
			}
			// 只在还没有可用文本时才重试初始化
			bool worldTextBound = this._chargeTextMode == UltimateStarGatlingBehaviour.ChargeTextDisplayMode.Fallback
				&& this._chargeText != null;
			if (!worldTextBound && this._chargeTextMode != UltimateStarGatlingBehaviour.ChargeTextDisplayMode.PlantText)
			{
				this._chargeTextRetryTimer -= Time.deltaTime;
				if (this._chargeTextRetryTimer <= 0f)
				{
					this._chargeTextRetryTimer = 0.5f;
					this.TryInitializeChargeText();
				}
			}
			else if (!worldTextBound && starModeEndTime <= 0f && !this._hasMeteorShowerBuff)
			{
				return;
			}
			this.RefreshChargeDisplayIfNeeded(false);
		}

		internal bool TryActivateStarMode()
		{
			if (this.IsStarModeActive)
			{
				return false;
			}
			if (this.Charge < 30f)
			{
				return false;
			}
			this.Charge -= 30f;
			this.StarModeEndTime = Time.time + 20f;
			this.RefreshChargeDisplayIfNeeded(true);
			return true;
		}

		internal bool TryTriggerSupernova()
		{
			if (!this.IsStarModeActive)
			{
				return false;
			}
			if (this.Charge < 2000f)
			{
				return false;
			}
			SuperSnowGatling plant = this.Plant;
			if (plant == null || plant.dying || plant.isCrashed)
			{
				return false;
			}
			this.Charge -= 2000f;
			UltimateStarGatlingSupernova.TriggerBurst(plant, this);
			this.RefreshChargeDisplayIfNeeded(true);
			return true;
		}

		internal void AddCharge(float amount)
		{
			if (amount <= 0f || this.IsStarModeActive)
			{
				return;
			}
			this.Charge += amount;
			this.RefreshChargeDisplayIfNeeded(true);
		}

		private void TryInitializeChargeText()
		{
			if (this._chargeTextMode == UltimateStarGatlingBehaviour.ChargeTextDisplayMode.PlantText)
			{
				return;
			}
			if (!HolyStarGatling.ShowChargeTextEnabled)
			{
				// 配置里关掉了充能文本
				return;
			}
			SuperSnowGatling plant = this.Plant;
			if (plant == null)
			{
				return;
			}
			// 首选：与 8003 共用的绑定器
			if (this._chargeText == null && this._chargeTextShadow == null)
			{
				// 自建文本挂植物子物体（不挂血条容器，避免漂移）
				TextMeshPro world = ChargeTextBinder.Create(plant, UltimateStarGatlingBehaviour.ChargeTextColor);
				if (world != null)
				{
					this.DestroyFallbackChargeText();
					this._fallbackTextObject = world.gameObject;
					this._chargeText = world;
					this._chargeTextMode = UltimateStarGatlingBehaviour.ChargeTextDisplayMode.Fallback;
					return;
				}
			}
			Transform transform = UltimateStarGatlingBehaviour.ResolveTextHead(plant);
			if (transform == null || !UltimateStarGatlingBehaviour.IsPlantTextSystemReady(transform))
			{
				return;
			}
			try
			{
				this.DestroyFallbackChargeText();
				if (this._chargeText == null)
				{
					this._chargeText = this.TrySetPlantText("UltimateStarGatlingCharge", UltimateStarGatlingBehaviour.ChargeTextColor, UltimateStarGatlingBehaviour.ChargeTextOffset, transform, "充能:0", 20);
					UltimateStarGatlingBehaviour.ApplyChargeTextStyle(this._chargeText);
				}
				if (this._chargeTextShadow == null)
				{
					this._chargeTextShadow = this.TrySetPlantText("UltimateStarGatlingChargeShadow", Color.black, UltimateStarGatlingBehaviour.ChargeTextShadowOffset, transform, "充能:0", 19);
					UltimateStarGatlingBehaviour.ApplyChargeTextStyle(this._chargeTextShadow);
				}
				if (this._chargeText != null && this._chargeTextShadow != null)
				{
					this._chargeTextMode = UltimateStarGatlingBehaviour.ChargeTextDisplayMode.PlantText;
				}
			}
			catch
			{
			}
		}

				private static bool IsPlantTextSystemReady(Transform textHead)
		{
			bool result;
			try
			{
				if (textHead.childCount <= 0)
				{
					result = false;
				}
				else
				{
					textHead.GetChild(0);
					result = true;
				}
			}
			catch
			{
				result = false;
			}
			return result;
		}

		private void DestroyFallbackChargeText()
		{
			if (this._fallbackTextObject == null)
			{
				return;
			}
			try
			{
				UnityEngine.Object.Destroy(this._fallbackTextObject);
			}
			catch
			{
			}
			this._fallbackTextObject = null;
			if (this._chargeTextMode == UltimateStarGatlingBehaviour.ChargeTextDisplayMode.Fallback)
			{
				this._chargeText = null;
			}
		}

				private static Transform ResolveTextHead(Plant plant)
		{
			Transform result = null;
			try
			{
				if (!UltimateStarGatlingBehaviour._reflectionInitialized)
				{
					UltimateStarGatlingBehaviour._reflectionInitialized = true;
					UltimateStarGatlingBehaviour._cachedHealthSliderProperty = plant.GetType().GetProperty("healthSlider");
				}
				PropertyInfo cachedHealthSliderProperty = UltimateStarGatlingBehaviour._cachedHealthSliderProperty;
				if (cachedHealthSliderProperty != null)
				{
					object healthSlider = cachedHealthSliderProperty.GetValue(plant);
					if (healthSlider != null)
					{
						if (UltimateStarGatlingBehaviour._cachedTextHeadProperty == null)
						{
							UltimateStarGatlingBehaviour._cachedTextHeadProperty = healthSlider.GetType().GetProperty("textHead");
						}
						PropertyInfo cachedTextHeadProperty = UltimateStarGatlingBehaviour._cachedTextHeadProperty;
						if (cachedTextHeadProperty != null)
						{
							result = cachedTextHeadProperty.GetValue(healthSlider) as Transform;
						}
					}
				}
			}
			catch
			{
			}
			if (result != null)
			{
				return result;
			}
			try
			{
				Transform transform = plant.transform.Find("textHead");
				if (transform != null)
				{
					return transform;
				}
				return plant.transform.Find("TextHead");
			}
			catch
			{
				return null;
			}
		}

		private static void ApplyChargeTextStyle(TextMeshPro text)
		{
			if (text == null)
			{
				return;
			}
			try
			{
				text.fontSize = 3.5f;
				text.enableWordWrapping = false;
				text.raycastTarget = false;
			}
			catch
			{
			}
		}

		private void RefreshChargeDisplayIfNeeded(bool force = false)
		{
			bool isStarModeActive = this.IsStarModeActive;
			int num = Mathf.FloorToInt(this.Charge);
			if (!force && isStarModeActive == this._displayedStarMode && num == this._displayedCharge)
			{
				return;
			}
			this._displayedStarMode = isStarModeActive;
			this._displayedCharge = num;
			this.RefreshChargeDisplay();
		}

		private void RefreshChargeDisplay()
		{
			if (this._chargeTextMode != UltimateStarGatlingBehaviour.ChargeTextDisplayMode.PlantText)
			{
				this.TryInitializeChargeText();
			}
			if (this._chargeTextMode != UltimateStarGatlingBehaviour.ChargeTextDisplayMode.PlantText && this._chargeText == null)
			{
				this.EnsureRuntimeFallbackChargeText();
				if (this._chargeText != null)
				{
					this._chargeTextMode = UltimateStarGatlingBehaviour.ChargeTextDisplayMode.Fallback;
				}
			}
			if (this._chargeText == null && this._chargeTextShadow == null)
			{
				return;
			}
			string text2 = this.BuildChargeLabel();
			try
			{
				if (this._chargeText != null)
				{
					this._chargeText.text = text2;
				}
				if (this._chargeTextShadow != null)
				{
					this._chargeTextShadow.text = text2;
				}
			}
			catch
			{
			}
		}

		/// <summary>充能文本内容。</summary>
		internal string BuildChargeLabel()
		{
			if (this.IsStarModeActive)
			{
				return "星澜:" + Mathf.FloorToInt(this.Charge) + "/" + Mathf.FloorToInt(2000f);
			}
			return "充能:" + Mathf.FloorToInt(this.Charge);
		}

		/// <summary>前置库路径不可用时的兜底：自建世界空间 TextMeshPro。</summary>
		private void EnsureRuntimeFallbackChargeText()
		{
			if (!HolyStarGatling.ShowChargeTextEnabled)
			{
				return; // 配置里关掉了
			}
			if (this._chargeText != null || this._chargeTextMode == UltimateStarGatlingBehaviour.ChargeTextDisplayMode.PlantText)
			{
				return;
			}
			TextMeshPro textMeshPro = ChargeTextBinder.Create(this.Plant, UltimateStarGatlingBehaviour.ChargeTextColor);
			if (textMeshPro == null)
			{
				return;
			}
			this._fallbackTextObject = textMeshPro.gameObject;
			this._chargeText = textMeshPro;
		}

		private Mouse GetMouseCached()
		{
			this._mouseRefreshTimer -= Time.deltaTime;
			if ((this._cachedMouse == null || !this._cachedMouse) && this._mouseRefreshTimer <= 0f)
			{
				this._cachedMouse = UnityEngine.Object.FindObjectOfType<Mouse>();
				this._mouseRefreshTimer = 2f;
			}
			return this._cachedMouse;
		}

		private void OnMouseDown()
		{
			try
			{
				Mouse mouseCached = this.GetMouseCached();
				if (mouseCached != null)
				{
					if (mouseCached.theCardOnMouse != null)
					{
						return;
					}
					if (mouseCached.theItemOnMouse != null)
					{
						return;
					}
					if (mouseCached.thePlantTypeOnMouse >= 0)
					{
						return;
					}
					if (mouseCached.thePlantOnGlove != null)
					{
						return;
					}
					if (mouseCached.theGardenPlantOnGlove != null)
					{
						return;
					}
					if (mouseCached.cannonPlant != null)
					{
						return;
					}
				}
				SuperSnowGatling plant = this.Plant;
				if (!(plant == null) && !plant.dying && !plant.isCrashed)
				{
					if (plant.thePlantType ==(PlantType)5166)
					{
						if (this.IsStarModeActive)
						{
							this.TryTriggerSupernova();
						}
						else
						{
							this.TryActivateStarMode();
						}
					}
				}
			}
			catch (Exception ex)
			{
				ManualLogSource pluginLogger = Core.PluginLogger;
				if (pluginLogger != null)
				{
										BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(27, 1, out bool flag);
					if (flag)
					{
						bepInExWarningLogInterpolatedStringHandler.AppendLiteral("[UltimateStarGatling] 点击异常：");
						bepInExWarningLogInterpolatedStringHandler.AppendFormatted<string>(ex.Message);
					}
					pluginLogger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
				}
			}
		}

		[HideFromIl2Cpp]
		private TextMeshPro TrySetPlantText(string name, Color color, Vector2 offset, Transform anchor, string text, int order)
		{
			if (anchor == null)
			{
				return null;
			}
			try
			{
				Transform child = anchor.Find(name);
				TextMeshPro textMeshPro;
				if (child != null)
				{
					textMeshPro = child.GetComponent<TextMeshPro>();
					if (textMeshPro == null)
					{
						return null;
					}
				}
				else
				{
					GameObject gameObject = new GameObject(name);
					gameObject.transform.SetParent(anchor, false);
					textMeshPro = gameObject.AddComponent<TextMeshPro>();
					textMeshPro.alignment = TextAlignmentOptions.Center;
					textMeshPro.fontSize = UltimateStarGatlingBehaviour.ChargeTextFontSize;
				}
				textMeshPro.text = text;
				textMeshPro.color = color;
				textMeshPro.sortingOrder = order;
				textMeshPro.transform.localPosition = new Vector3(offset.x, offset.y, textMeshPro.transform.localPosition.z);
				return textMeshPro;
			}
			catch
			{
				return null;
			}
		}

		internal float Charge;

		internal float StarModeEndTime;

		internal float MeteorShowerTimer;

		private int _displayedCharge = int.MinValue;

		private bool _displayedStarMode;

		private bool _hasMeteorShowerBuff;

		private float _meteorBuffRecheckTimer;

		private const float MeteorBuffRecheckInterval = 0.5f;

		private const float MeteorBuffRecheckIdleInterval = 2f;

		private SuperSnowGatling _plant;

		private TextMeshPro _chargeText;

		private TextMeshPro _chargeTextShadow;

		private GameObject _fallbackTextObject;

		private UltimateStarGatlingBehaviour.ChargeTextDisplayMode _chargeTextMode;

		private float _chargeTextRetryTimer;

		private const float ChargeTextRetryInterval = 0.5f;

		private static readonly Color ChargeTextColor = new Color(1f, 0.95f, 0.35f, 1f);

		private static readonly Vector2 ChargeTextOffset = new Vector2(0f, -0.28f);

		private static readonly Vector2 ChargeTextShadowOffset = new Vector2(0.02f, -0.3f);

		private const float ChargeTextFontSize = 3.5f;

		private const float ChargeTextFallbackFontSize = 6.5f;

		private const float ChargeTextFallbackLocalY = 6.5f;

		private static bool _reflectionInitialized;

		private static PropertyInfo _cachedHealthSliderProperty;

		private static PropertyInfo _cachedTextHeadProperty;

				private static readonly System.Collections.Generic.Dictionary<IntPtr, UltimateStarGatlingBehaviour> ActiveBehaviours = new System.Collections.Generic.Dictionary<IntPtr, UltimateStarGatlingBehaviour>();

		private Mouse _cachedMouse;

		private float _mouseRefreshTimer;

				private enum ChargeTextDisplayMode
		{
			None,
			Fallback,
			PlantText
		}
	}
}
