using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using MengxiLib.BepInEx;
using TMPro;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>终焉·撰星极帝机枪射手本体组件。</summary>
	public class HolyStarGatlingBehaviour : MonoBehaviour
	{
		public HolyStarGatlingBehaviour() : base(ClassInjector.DerivedConstructorPointer<HolyStarGatlingBehaviour>())
		{
			ClassInjector.DerivedConstructorBody(this);
		}

		public HolyStarGatlingBehaviour(IntPtr pointer) : base(pointer)
		{
		}

		private static readonly Dictionary<IntPtr, HolyStarGatlingBehaviour> ActiveBehaviours =
			new Dictionary<IntPtr, HolyStarGatlingBehaviour>();

		private static readonly Color ChargeTextColor = new Color(1f, 0.95f, 0.35f, 1f);

		private SuperSnowGatling _plant;
		private bool _chargeTextBound;
		/// <summary>充能文本。</summary>
		private TextMeshPro _fallbackText;

		// ---- ⑥ 超充能 ----
		private bool _supercharged;
		private float _physicsAcc;
		private float _appliedBrightness = 1f;
		private SpriteRenderer[] _brightnessRenderers;
		private int _revives;
		private GameObject _chargeTextObject;
		private string _chargeLabelCache;
		private int _chargeLabelValue = int.MinValue;
		private Mouse _cachedMouse;
		private float _mouseRefreshTimer;
		private float _satelliteTimer;
		private float _meteorShowerTimer;
		private float _buffRecheckTimer;
		private bool _hasMeteorShowerBuff;
		private int _displayedCharge = int.MinValue;
		private float _textRetryTimer;
		private bool _particleLayerBound;
		private int _particleLayerAttempts;

		internal float Charge;

		private float _spawnTime = -1f;

		internal float SpawnTime
		{
			get { return _spawnTime; }
		}

		internal float AliveSeconds
		{
			get
			{
				try
				{
					return _spawnTime >= 0f ? Time.time - _spawnTime : 0f;
				}
				catch { return 0f; }
			}
		}

		internal SuperSnowGatling Self
		{
			get
			{
				try
				{
					if (_plant == null)
					{
						_plant = gameObject.GetComponent<SuperSnowGatling>();
					}
				}
				catch { }
				return _plant;
			}
		}

		internal static HolyStarGatlingBehaviour TryGetBehaviour(Plant plant)
		{
			if (plant == null)
			{
				return null;
			}
			try
			{
				HolyStarGatlingBehaviour behaviour;
				if (ActiveBehaviours.TryGetValue(plant.Pointer, out behaviour))
				{
					return behaviour;
				}
			}
			catch { }
			return null;
		}

		private void Register()
		{
			try
			{
				SuperSnowGatling plant = Self;
				if (plant != null)
				{
					ActiveBehaviours[plant.Pointer] = this;
				}
			}
			catch { }
		}

		private void Unregister()
		{
			try
			{
				SuperSnowGatling plant = Self;
				if (plant != null)
				{
					ActiveBehaviours.Remove(plant.Pointer);
				}
			}
			catch { }
		}

		private void Awake()
		{
			try
			{
				if (_spawnTime < 0f)
				{
					_spawnTime = Time.time;
				}
				SuperSnowGatling plant = Self;
				if (plant == null)
				{
					return;
				}
				Register();

				Transform head = transform.Find("GatlingPea_head");
				if (head == null)
				{
					head = transform.childCount > 0 ? transform.GetChild(0) : null;
				}
				Transform shoot = head != null ? head.Find("Shoot") : transform.Find("Shoot");
				if (shoot != null)
				{
					plant.shoot = shoot;
				}
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " Awake 异常：" + ex.Message);
			}
		}

		private void Start()
		{
			if (_spawnTime < 0f)
			{
				_spawnTime = Time.time;
			}
			Register();
			EnsureText();
			EnsureParticleLayer();
		}

		private void OnDestroy()
		{
			Unregister();
			ApplyBrightness(1f); // 亮度必须还原，否则材质属性块残留
			try
			{
				if (_chargeTextObject != null)
				{
					UnityEngine.Object.Destroy(_chargeTextObject);
				}
			}
			catch { }
			_chargeTextObject = null;
		}

		private void Update()
		{
			SuperSnowGatling plant = Self;
			if (plant == null || plant.dying || plant.thePlantHealth <= 0)
			{
				return;
			}

			float dt = Time.deltaTime;

			// ① 每 5 秒一颗卫星
			_satelliteTimer += dt;
			if (_satelliteTimer >= HolyStarGatling.SatelliteInterval)
			{
				_satelliteTimer = 0f;
				HolyStarCombat.SpawnSatellite(plant);
			}

			// ③ 词条「流星雨」：每 10 秒自动开启一次大招
			_buffRecheckTimer -= dt;
			if (_buffRecheckTimer <= 0f)
			{
				_buffRecheckTimer = _hasMeteorShowerBuff ? 0.5f : 2f;
				_hasMeteorShowerBuff = HolyStarCombat.HasMeteorShowerBuff();
			}
			if (_hasMeteorShowerBuff)
			{
				_meteorShowerTimer += dt;
				if (_meteorShowerTimer >= HolyStarGatling.MeteorShowerAutoInterval)
				{
					_meteorShowerTimer = 0f;
					HolyStarCombat.TriggerSuperShoot(plant);
				}
			}
			else
			{
				_meteorShowerTimer = 0f;
			}

			// ⑥ 超充能（充能 ≥ 1 万）
			TickSuperCharge(plant, dt);

			EnsureText();
			RefreshText(false);
			EnsureParticleLayer();
		}

		/// <summary>开大附属动画兜底</summary>
		private void EnsureParticleLayer()
		{
			if (_particleLayerBound)
			{
				return;
			}
			try
			{
				SuperSnowGatling plant = Self;
				ParticleLayerBinder.Result result = ParticleLayerBinder.Ensure(plant != null ? plant.anim : null);
				if (result == ParticleLayerBinder.Result.Ready)
				{
					_particleLayerBound = true;
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " particle 层已激活（开大附属动画 Hide/HP1/HP2 生效）");
					return;
				}
				if (result == ParticleLayerBinder.Result.Missing)
				{
					_particleLayerBound = true;
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 控制器无 particle 层，跳过开大附属动画");
					return;
				}

				// Animator 还没就绪：限时重试，约 10 秒后放弃
				_particleLayerAttempts++;
				if (_particleLayerAttempts >= 600)
				{
					_particleLayerBound = true;
					HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " particle 层激活失败：Animator 一直不可用（开大附属动画不会显示）");
				}
			}
			catch { }
		}

		/// <summary>⑥ 每次命中 +1 充能（无上限）。</summary>
		internal void AddCharge(float amount)
		{
			if (amount <= 0f)
			{
				return;
			}
			Charge += amount;
			RefreshText(true);
		}

		/// <summary>⑦ 复活后重置全部状态。</summary>
		internal void ResetAllState()
		{
			Charge = 0f;
			_satelliteTimer = 0f;
			_meteorShowerTimer = 0f;
			_buffRecheckTimer = 0f;
			_hasMeteorShowerBuff = false;
			_displayedCharge = int.MinValue;
			_supercharged = false;
			_physicsAcc = 0f;
			ApplyBrightness(1f);
			RefreshText(true);
		}

		/// <summary>⑦ 是否还有复活名额。</summary>
		internal bool TryConsumeRevive()
		{
			if (_revives >= HolyStarGatling.MaxRevives)
			{
				return false;
			}
			_revives++;
			return true;
		}

		// ---------------------------------------------------------------- ⑥ 超充能（≥1 万充能）

		private void TickSuperCharge(SuperSnowGatling plant, float dt)
		{
			try
			{
				if (Charge < HolyStarGatling.SuperChargeThreshold)
				{
					if (_supercharged)
					{
						ExitSuperCharge();
					}
					return;
				}

				float t = Mathf.Clamp01((Charge - HolyStarGatling.SuperChargeThreshold) /
					(HolyStarGatling.SuperChargeOverload - HolyStarGatling.SuperChargeThreshold));
				if (!_supercharged)
				{
					_supercharged = true;
					_physicsAcc = 0f;
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 进入超充能（充能 ≥ " +
						(int)HolyStarGatling.SuperChargeThreshold + "）：亮度提升、每 0.02 秒范围打击、能量自增");
				}
				ApplyBrightness(1f + t * (HolyStarGatling.SuperChargeMaxBrightness - 1f));

				// 物理帧节拍；guard 防卡顿后一次跑上百拍
				_physicsAcc += dt;
				int guard = 0;
				while (_physicsAcc >= HolyStarGatling.SuperChargeTick && guard < 16)
				{
					_physicsAcc -= HolyStarGatling.SuperChargeTick;
					guard++;
					HolyStarCombat.SuperChargeStrike(plant);
					Charge += Charge / HolyStarGatling.SuperChargeGainDivisor; // 每拍 +1/2000 当前能量
				}

				if (Charge >= HolyStarGatling.SuperChargeOverload)
				{
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 充能达 " +
						(int)HolyStarGatling.SuperChargeOverload + "：过载湮灭");
					Charge = 0f;
					ExitSuperCharge();
					Vector3 p = plant.transform.position;
					HolyStarSupernova.Begin(plant, new Vector2(p.x, p.y), plant.thePlantRow);
					RefreshText(true);
				}
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 超充能异常：" + ex.Message);
			}
		}

		private void ExitSuperCharge()
		{
			_supercharged = false;
			_physicsAcc = 0f;
			ApplyBrightness(1f);
		}

		private void ApplyBrightness(float brightness)
		{
			if (Mathf.Abs(brightness - _appliedBrightness) < 0.001f)
			{
				return;
			}
			_appliedBrightness = brightness;
			try
			{
				if (_brightnessRenderers == null)
				{
					// 只取一次
					_brightnessRenderers = GetComponentsInChildren<SpriteRenderer>(true);
				}
				SpriteRenderer[] renderers = _brightnessRenderers;
				if (renderers == null)
				{
					return;
				}
				for (int i = 0; i < renderers.Length; i++)
				{
					SpriteRenderer renderer = renderers[i];
					if (renderer == null)
					{
						continue;
					}
					global::Core.GameMaterial.SetBrightness(renderer, brightness);
				}
			}
			catch { }
		}

		private Mouse GetMouse()
		{
			_mouseRefreshTimer -= Time.deltaTime;
			if ((_cachedMouse == null) && _mouseRefreshTimer <= 0f)
			{
				try { _cachedMouse = UnityEngine.Object.FindObjectOfType<Mouse>(); }
				catch { }
				_mouseRefreshTimer = 2f;
			}
			return _cachedMouse;
		}

		private void OnMouseDown()
		{
			try
			{
				Mouse mouse = GetMouse();
				if (mouse != null)
				{
					if (mouse.theCardOnMouse != null) return;
					if (mouse.theItemOnMouse != null) return;
					if ((int)mouse.thePlantTypeOnMouse >= 0) return;
					if (mouse.thePlantOnGlove != null) return;
					if (mouse.theGardenPlantOnGlove != null) return;
					if (mouse.cannonPlant != null) return;
				}

				TryClickAction();
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 点击异常：" + ex.Message);
			}
		}

		/// <summary>同一次点击的多条入口按帧去重。</summary>
		private static int _lastClickFrame = -1;

		/// <summary>「充能不足」日志节流。</summary>
		private static float _chargeShortLogAt = -99f;

		private void LogChargeShort()
		{
			// 默认静默：这是玩家每次点击都会走的交互噪音
			if (!HolyStarGatling.VerboseRuntimeLog)
			{
				return;
			}
			if (Time.time - _chargeShortLogAt < 2f)
			{
				return;
			}
			_chargeShortLogAt = Time.time;
			HolyStarGatling.LogInfo(HolyStarGatling.LogTag + " 点击命中本植物，但充能不足：" +
				Mathf.FloorToInt(Charge) + " < " + (int)HolyStarGatling.SupernovaChargeCost);
		}

		/// <summary>⑥ 点击结算：充能 ≥ 1000 时消耗并释放超新星。两条入口共用，按帧去重。</summary>
		internal bool TryClickAction()
		{
			try
			{
				SuperSnowGatling plant = Self;
				if (plant == null || plant.dying || plant.isCrashed)
				{
					return false;
				}
				if (Time.frameCount == _lastClickFrame)
				{
					return false; // 本帧已被另一条入口处理
				}
				if (Charge < HolyStarGatling.SupernovaChargeCost)
				{
					LogChargeShort();
					return false;
				}
				_lastClickFrame = Time.frameCount;
				Charge -= HolyStarGatling.SupernovaChargeCost;
				HolyStarCombat.TriggerSupernova(plant);
				RefreshText(true);
				return true;
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 超新星异常：" + ex.Message);
				return false;
			}
		}

		private void EnsureText()
		{
			if (_chargeTextBound || _fallbackText != null)
			{
				return;
			}
			_textRetryTimer -= Time.deltaTime;
			if (_textRetryTimer > 0f)
			{
				return;
			}
			_textRetryTimer = 0.5f;

			SuperSnowGatling plant = Self;
			if (plant == null)
			{
				return;
			}
			if (!HolyStarGatling.ShowChargeTextEnabled)
			{
				_chargeTextBound = true; // 配置里关掉了，不再尝试创建
				return;
			}

			// 两株植物共用：自建文本挂为植物子物体（不挂血条容器，避免漂移）
			TextMeshPro text = ChargeTextBinder.Create(plant, ChargeTextColor);
			if (text == null)
			{
				return; // 0.5 秒后再试
			}
			_fallbackText = text;
			_chargeTextObject = text.gameObject;
			_chargeTextBound = true;
		}

		/// <summary>文本内容</summary>
		private string ChargeLabel()
		{
			int value = Mathf.FloorToInt(Charge);
			if (_chargeLabelCache == null || value != _chargeLabelValue)
			{
				_chargeLabelValue = value;
				_chargeLabelCache = "充能:" + value + "/" + (int)HolyStarGatling.SupernovaChargeCost;
			}
			return _chargeLabelCache;
		}

		private void RefreshText(bool force)
		{
			if (_fallbackText == null)
			{
				EnsureText();
				if (_fallbackText == null)
				{
					return;
				}
			}
			int value = Mathf.FloorToInt(Charge);
			if (!force && value == _displayedCharge)
			{
				return;
			}
			_displayedCharge = value;
			try { _fallbackText.text = ChargeLabel(); }
			catch { }
		}
	}
}
