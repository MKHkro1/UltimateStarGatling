using System;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>卫星</summary>
	public class HolyStarSatelliteBehaviour : MonoBehaviour
	{
		public HolyStarSatelliteBehaviour() : base(ClassInjector.DerivedConstructorPointer<HolyStarSatelliteBehaviour>())
		{
			ClassInjector.DerivedConstructorBody(this);
		}

		public HolyStarSatelliteBehaviour(IntPtr pointer) : base(pointer)
		{
		}

		private Plant _owner;
		private Transform _anchor;
		private Transform _ring;
		private Vector3 _visualOffset;
		private int _damage = 1;
		private float _angle;
		private float _contactTimer;
		private float _life;
		private int _accumulated;
		private bool _inContact;
		private bool _exploded;
		/// <summary>整棵卫星的渲染器</summary>
		private SpriteRenderer[] _renderers;
		/// <summary>伤害归属</summary>
		private IDamageMaker _maker;

		internal void Init(Plant owner, int damage, Transform visualAnchor, Vector3 visualScale)
		{
			_owner = owner;
			_damage = Mathf.Max(1, damage);
			_angle = 0f;
			_contactTimer = 0f;
			_life = 0f;
			_accumulated = 0;
			_inContact = false;
			_exploded = false;

			try
			{
				if (visualScale.x > 0.0001f && visualScale.y > 0.0001f)
				{
					transform.localScale = visualScale;
				}
			}
			catch { }

			try
			{
				// 只取一次
				_renderers = GetComponentsInChildren<SpriteRenderer>(true);
				for (int i = 0; i < _renderers.Length; i++)
				{
					if (_renderers[i] != null)
					{
						_renderers[i].enabled = true;
					}
				}
			}
			catch { _renderers = null; }

			// 伤害归属一次转型即可
			try { _maker = owner != null ? owner.TryCast<IDamageMaker>() : null; }
			catch { _maker = null; }

			// 淡入起点：整棵卫星从全透明开始
			ApplyAlpha(0f);

			_anchor = visualAnchor;
			_visualOffset = Vector3.zero;
			if (_anchor != null)
			{
				try { _visualOffset = _anchor.position - transform.position; }
				catch { _visualOffset = Vector3.zero; }

				// 外围星星的容器
				try
				{
					GameObject center = HolyStarCombat.FindDeep(_anchor, "Center");
					if (center != null)
					{
						_ring = center.transform;
					}
					else
					{
						_ring = ResolveRingByChildren(_anchor);
					}
				}
				catch { }
			}
		}

		/// <summary>兜底</summary>
		private static Transform ResolveRingByChildren(Transform anchor)
		{
			try
			{
				int count = anchor.childCount;
				for (int i = 0; i < count; i++)
				{
					Transform child = anchor.GetChild(i);
					if (child == null)
					{
						continue;
					}
					int renderers = 0;
					int kids = child.childCount;
					for (int k = 0; k < kids; k++)
					{
						Transform grand = child.GetChild(k);
						if (grand != null && grand.GetComponent<SpriteRenderer>() != null)
						{
							renderers++;
						}
					}
					if (renderers >= 2)
					{
						return child;
					}
				}
			}
			catch { }
			return null;
		}

		/// <summary>淡入用</summary>
		private void ApplyAlpha(float alpha)
		{
			SpriteRenderer[] renderers = _renderers;
			if (renderers == null)
			{
				return; // 取不到渲染器时不做淡入
			}
			try
			{
				for (int i = 0; i < renderers.Length; i++)
				{
					SpriteRenderer renderer = renderers[i];
					if (renderer == null)
					{
						continue;
					}
					Color c = renderer.color;
					if (Mathf.Abs(c.a - alpha) > 0.001f)
					{
						renderer.color = new Color(c.r, c.g, c.b, alpha);
					}
				}
			}
			catch { }
		}

		/// <summary>世界坐标下的视觉中心</summary>
		private Vector2 VisualCenter()
		{
			if (_anchor != null)
			{
				try
				{
					Vector3 p = _anchor.position;
					return new Vector2(p.x, p.y);
				}
				catch { }
			}
			Vector3 fallback = transform.position;
			return new Vector2(fallback.x, fallback.y);
		}

		private Vector2 OwnerCenter()
		{
			try
			{
				if (_owner != null && _owner.axis != null)
				{
					Vector3 p = _owner.axis.position;
					return new Vector2(p.x, p.y);
				}
			}
			catch { }
			return VisualCenter();
		}

		private void Update()
		{
			try
			{
				if (_owner == null || Board.Instance == null)
				{
					UnityEngine.Object.Destroy(gameObject);
					return;
				}

				float dt = Time.deltaTime;
				_life += dt;

				// 出现淡入 0 → 1
				if (_life < HolyStarGatling.SatelliteFadeInSeconds)
				{
					ApplyAlpha(Mathf.Clamp01(_life / HolyStarGatling.SatelliteFadeInSeconds));
				}

				// 自转：中心逆时针、外围顺时针
				try
				{
					if (_anchor != null)
					{
						_anchor.Rotate(0f, 0f, HolyStarGatling.SatelliteCenterSpinSpeed * dt);
					}
					if (_ring != null)
					{
						_ring.Rotate(0f, 0f, HolyStarGatling.SatelliteRingSpinSpeed * dt);
					}
				}
				catch { }

				// 环绕：正圆轨迹，半径 3 格
				_angle += dt * HolyStarGatling.SatelliteOrbitSpeed;
				if (_angle > 360f)
				{
					_angle -= 360f;
				}
				float rad = _angle * Mathf.Deg2Rad;
				Vector2 center = OwnerCenter();
				Vector2 target = new Vector2(
					center.x + Mathf.Cos(rad) * HolyStarGatling.SatelliteOrbitRadius,
					center.y + Mathf.Sin(rad) * HolyStarGatling.SatelliteOrbitRadius *
						HolyStarGatling.SatelliteOrbitVerticalFactor);

				// 把视觉中心摆到轨道点上
				transform.position = new Vector3(target.x - _visualOffset.x, target.y - _visualOffset.y,
					transform.position.z);

				_contactTimer -= dt;
				if (_contactTimer <= 0f)
				{
					_contactTimer = HolyStarGatling.SatelliteContactInterval;
					ContactTick(VisualCenter());
				}

				if (_life >= HolyStarGatling.SatelliteLifetime)
				{
					Explode();
				}
			}
			catch
			{
				UnityEngine.Object.Destroy(gameObject);
			}
		}

		private void ContactTick(Vector2 pos)
		{
			Board board = Board.Instance;
			if (board == null || board.zombieArray == null)
			{
				return;
			}
			bool touched = false;
			int contactDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * HolyStarGatling.SatelliteContactRatio));
			float radiusSqr = HolyStarGatling.SatelliteContactRadius * HolyStarGatling.SatelliteContactRadius;

			Il2CppSystem.Collections.Generic.List<Zombie> zombies = board.zombieArray;
			int n = zombies.Count;
			for (int i = 0; i < n; i++)
			{
				Zombie z = zombies[i];
				if (z == null || z.beforeDying || z.isMindControlled)
				{
					continue;
				}
				Vector3 zp;
				try { zp = z.axis != null ? z.axis.position : z.transform.position; }
				catch { continue; }

				float dx = zp.x - pos.x;
				float dy = zp.y - pos.y;
				if (dx * dx + dy * dy > radiusSqr)
				{
					continue;
				}
				touched = true;
				try
				{
					z.TakeDamage(contactDamage, _maker, DamageType.NormalAll,
						(PlantType)HolyStarGatling.PlantId, false);
					_accumulated += contactDamage;
				}
				catch { }
				try { z.transform.Translate(0.12f, 0f, 0f); }
				catch { }
			}

			if (touched && !_inContact)
			{
				HolyStarCombat.SpawnSplitBullets(_owner, pos, _damage);
			}
			_inContact = touched;
		}

		private void Explode()
		{
			if (_exploded)
			{
				return;
			}
			_exploded = true;

			try
			{
				Vector2 visual = VisualCenter();
				Vector2 center = OwnerCenter();
				// 正前方 = +x；横向偏移按格给
				Vector2 pos = new Vector2(
					center.x + HolyStarGatling.CellWidth * HolyStarGatling.SatelliteExplodeOffsetCells,
					visual.y);
				int damage = Mathf.Max(1, _accumulated);
				int row = _owner != null ? _owner.thePlantRow : 0;

				try
				{
					if (ParticleManager.Instance != null)
					{
						ParticleManager.Instance.SetParticle((ParticleType)131, pos, row, true, 0f);
					}
				}
				catch { }
				try { GameAPP.PlaySound(40, 0.5f, 1.0f); } catch { }

				Board board = Board.Instance;
				if (board != null && board.zombieArray != null)
				{
					float radiusSqr = HolyStarGatling.SatelliteExplodeRadius * HolyStarGatling.SatelliteExplodeRadius;
					Il2CppSystem.Collections.Generic.List<Zombie> zombies = board.zombieArray;
					int n = zombies.Count;
					for (int i = 0; i < n; i++)
					{
						Zombie z = zombies[i];
						if (z == null || z.beforeDying || z.isMindControlled)
						{
							continue;
						}
						Vector3 zp;
						try { zp = z.axis != null ? z.axis.position : z.transform.position; }
						catch { continue; }
						float dx = zp.x - pos.x;
						float dy = zp.y - pos.y;
						if (dx * dx + dy * dy > radiusSqr)
						{
							continue;
						}
						try
						{
							z.TakeDamage(damage, _maker, DamageType.NormalAll,
								(PlantType)HolyStarGatling.PlantId, false);
						}
						catch { }
					}
				}
			}
			catch { }

			try
			{
				if (HolyStarGatling.VerboseSatellite)
				{
					HolyStarGatling.LogInfo(HolyStarGatling.LogTag + " 卫星爆炸：偏移 " +
						HolyStarGatling.SatelliteExplodeOffsetCells + " 格（本地 x=+" +
						(HolyStarGatling.CellWidth * HolyStarGatling.SatelliteExplodeOffsetCells).ToString("F2") +
						"），伤害 " + _accumulated);
				}
			}
			catch { }

			UnityEngine.Object.Destroy(gameObject);
		}
	}
}
