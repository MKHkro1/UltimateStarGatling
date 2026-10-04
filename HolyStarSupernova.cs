using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>超新星爆炸</summary>
	internal static class HolyStarSupernova
	{
		internal static void Begin(Plant plant, Vector2 center, int row)
		{
			try
			{
				HolyStarSupernovaSequence sequence = HolyStarSupernovaSequence.Ensure();
				if (sequence == null)
				{
					HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 超新星宿主创建失败，本次大招无效");
					return;
				}
				sequence.Play(plant, center, row);

				try
				{
					HolyStarGatlingBehaviour behaviour = HolyStarGatlingBehaviour.TryGetBehaviour(plant);
					if (behaviour != null)
					{
						UltimateStarGatlingSupernova.TriggerBurst(plant, behaviour);
					}
				}
				catch (Exception ex)
				{
					HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 原版超新星叠加失败：" + ex.Message);
				}
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 超新星启动异常：" + ex.Message);
			}
		}
	}

	/// <summary>超新星三段流程的执行体</summary>
	internal sealed class HolyStarSupernovaSequence : MonoBehaviour
	{
		internal static HolyStarSupernovaSequence Instance;

		private static GameObject _host;

		private bool _active;
		private float _elapsed;
		private float _absorbAcc;
		private float _waveTimer;
		private int _wavesDone;
		private int _absorbed;
		private Vector2 _center;
		private int _row;
		private GameObject _star;
		private Vector3 _starBaseScale = Vector3.one;
		/// <summary>红矮星的渲染器。</summary>
		private SpriteRenderer[] _starRenderers;
		/// <summary>起手闪白计时。</summary>
		private float _flashTimer;
		/// <summary>正在被吸走的僵尸。</summary>
		private readonly List<Suction> _suctions = new List<Suction>();

		/// <summary>被吸走的表现单元。</summary>
		private sealed class Suction
		{
			internal Zombie zombie;
			internal float elapsed;
		}

		internal static HolyStarSupernovaSequence Ensure()
		{
			if (Instance != null)
			{
				return Instance;
			}
			try
			{
				if (_host == null)
				{
					_host = new GameObject("HolyStarSupernova");
					UnityEngine.Object.DontDestroyOnLoad(_host);
				}
				Instance = _host.AddComponent<HolyStarSupernovaSequence>();
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 超新星宿主异常：" + ex.Message);
			}
			return Instance;
		}

		internal void Play(Plant plant, Vector2 center, int row)
		{
			if (_active)
			{
				return; // 上一发还在跑
			}
			_active = true;
			_elapsed = 0f;
			_absorbAcc = 0f;
			_waveTimer = 0f;
			_wavesDone = 0;
			_absorbed = 0;
			_suctions.Clear();
			_flashTimer = 0f;
			_center = center;
			_row = row;
			DestroyStar();

			// —— 湮灭成红矮星 + 开场冲击
			_star = HolyStarCombat.SpawnRedDwarf(center);
			if (_star != null)
			{
				_starBaseScale = _star.transform.localScale;
				_starRenderers = _star.GetComponentsInChildren<SpriteRenderer>(true);
			}
			try { ScreenShake.TriggerShake(0.25f); } catch { }
			try { GameAPP.PlaySound(74, 0.9f, 1.0f); } catch { } // 74 = 巨人砸击
			try
			{
				ParticleManager manager = ParticleManager.Instance;
				if (manager != null)
				{
					manager.SetParticle(ParticleType.BombCloud, center, _row, true, 0f);
				}
			}
			catch { }

			HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 超新星爆炸：红矮星湮灭启动（吸走 " +
				HolyStarGatling.SupernovaAbsorbSeconds + " 秒 → " + HolyStarGatling.SupernovaWaveCount +
				" 波 × " + HolyStarGatling.SupernovaDamage + " 毁灭打击）");
		}

		private void Update()
		{
			if (!_active)
			{
				return;
			}
			float dt = Time.deltaTime;
			try
			{
				if (Board.Instance == null)
				{
					Finish(false); // 换关/回主菜单：直接收尾
					return;
				}
				_elapsed += dt;

				// 相位 A：吸走全屏非领袖僵尸
				if (_elapsed < HolyStarGatling.SupernovaAbsorbSeconds)
				{
					_absorbAcc += dt;
					// 每 1/60 秒吸收一只
					while (_absorbAcc >= 1f / 60f)
					{
						_absorbAcc -= 1f / 60f;
						AbsorbOne();
					}
					UpdateSuctions(dt); // 推进上一拍的表现
					GrowStar(Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, HolyStarGatling.SupernovaAbsorbSeconds)));
					return;
				}

				// 相位 B：毁灭打击
				if (_wavesDone < HolyStarGatling.SupernovaWaveCount)
				{
					_waveTimer -= dt;
					if (_waveTimer <= 0f)
					{
						_waveTimer = HolyStarGatling.SupernovaWaveInterval;
						AnnihilateWave();
						_wavesDone++;
						if (_wavesDone % 10 == 0)
						{
							try { ScreenShake.TriggerShake(0.08f); } catch { }
							try
							{
								ParticleManager manager = ParticleManager.Instance;
								if (manager != null)
								{
									manager.SetParticle(ParticleType.BombCloud, _center, _row, true, 0f);
								}
							}
							catch { }
						}
					}
					return;
				}

				Finish(true);
			}
			catch
			{
				Finish(false);
			}
		}

		/// <summary>场上敌方僵尸表</summary>
		private static Il2CppSystem.Collections.Generic.List<Zombie> EnemyZombies()
		{
			try
			{
				Board board = Board.Instance;
				return board != null ? board.zombieArray : null;
			}
			catch { return null; }
		}

		/// <summary>相位 A：随机吸走一只非领袖僵尸。</summary>
		private void AbsorbOne()
		{
			try
			{
				Il2CppSystem.Collections.Generic.List<Zombie> zombies = EnemyZombies();
				if (zombies == null)
				{
					return;
				}
				int count = zombies.Count;
				if (count <= 0)
				{
					return;
				}
				// 抽样最多试 8 次，跳过领袖与魅惑僵尸
				for (int i = 0; i < 8; i++)
				{
					Zombie zombie = zombies[UnityEngine.Random.Range(0, count)];
					if (zombie == null)
					{
						continue;
					}
					if (zombie.isMindControlled || zombie.beforeDying)
					{
						continue;
					}
					if (TypeMgr.IsLeaderZombie(zombie.theZombieType))
					{
						continue;
					}
					// 队列满时直接 Die(1)，保证清场能力
					if (_suctions.Count < HolyStarGatling.SupernovaSuctionMax)
					{
						Suction suction = new Suction();
						suction.zombie = zombie;
						_suctions.Add(suction);
					}
					else
					{
						zombie.Die(1);
						_absorbed++;
					}
					return;
				}
			}
			catch { }
		}

		/// <summary>相位 B：一波全屏毁灭打击。</summary>
		private void AnnihilateWave()
		{
			try
			{
				Il2CppSystem.Collections.Generic.List<Zombie> zombies = EnemyZombies();
				if (zombies == null)
				{
					return;
				}
				int count = zombies.Count;
				for (int i = 0; i < count; i++)
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
							continue; // 只打敌方
						}
						// MaxDamage 档：不经 GetDamage，不吃领袖限伤
						zombie.Charred(HolyStarGatling.SupernovaDamage, (PlantType)HolyStarGatling.PlantId,
							false, DamageType.MaxDamage);
					}
					catch { }
				}
			}
			catch { }
		}

		/// <summary>推进被吸走的表现：朝红矮星收拢 + 缩小，到时结算 Die(1)。</summary>
		private void UpdateSuctions(float dt)
		{
			for (int i = _suctions.Count - 1; i >= 0; i--)
			{
				Suction suction = _suctions[i];
				Zombie zombie = suction.zombie;
				bool gone = zombie == null;
				if (!gone)
				{
					try { gone = zombie.beforeDying; } catch { gone = true; }
				}
				if (gone)
				{
					_suctions.RemoveAt(i);
					continue;
				}
				suction.elapsed += dt;
				float k = Mathf.Clamp01(suction.elapsed / Mathf.Max(0.01f, HolyStarGatling.SupernovaSuctionSeconds));
				try
				{
					Vector3 p = zombie.transform.position;
					zombie.transform.position = Vector3.Lerp(p, new Vector3(_center.x, _center.y, p.z), k * 0.35f);
					float scale = Mathf.Lerp(1f, 0.15f, k);
					zombie.transform.localScale = new Vector3(scale, scale, 1f);
				}
				catch { }
				if (suction.elapsed >= HolyStarGatling.SupernovaSuctionSeconds)
				{
					try { zombie.Die(1); } catch { }
					_absorbed++;
					_suctions.RemoveAt(i);
				}
			}
		}

		private void GrowStar(float progress)
		{
			try
			{
				if (_star == null)
				{
					return;
				}
				// 缓动 + 呼吸脉动 + 高速自转
				float ease = progress * progress * (3f - 2f * progress);
				float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 14f);
				float scale = (1f + 1.2f * ease) * pulse;
				_star.transform.localScale = _starBaseScale * scale;
				_star.transform.Rotate(0f, 0f, -260f * Time.deltaTime);

				// 起手闪白：前 0.45 秒染成炽白，再渐回红矮星色
				_flashTimer += Time.deltaTime;
				if (_starRenderers != null && _flashTimer <= 0.45f)
				{
					float hot = 1f - Mathf.Clamp01(_flashTimer / 0.45f);
					for (int i = 0; i < _starRenderers.Length; i++)
					{
						SpriteRenderer renderer = _starRenderers[i];
						if (renderer == null)
						{
							continue;
						}
						renderer.color = new Color(1f, Mathf.Lerp(0.30f, 1f, hot), Mathf.Lerp(0.20f, 0.9f, hot), 1f);
					}
				}
			}
			catch { }
		}

		private void Finish(bool completed)
		{
			if (!_active)
			{
				return;
			}
			_active = false;
			try
			{
				if (completed)
				{
					try { ScreenShake.TriggerShake(0.2f); } catch { }
					try { GameAPP.PlaySound(43, 0.8f, 1.0f); } catch { } // 43 = 基洛夫炸弹爆炸
					try
					{
						ParticleManager manager = ParticleManager.Instance;
						if (manager != null)
						{
							manager.SetParticle(ParticleType.BombCloud, _center, _row, true, 0f);
						}
					}
					catch { }
				}
				DestroyStar();
				HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 超新星收尾：" +
					(completed ? "完成" : "中断") + "，吸走 " + _absorbed + " 只非领袖僵尸，毁灭打击 " +
					_wavesDone + " 波");
			}
			catch { }
		}

		private void DestroyStar()
		{
			try
			{
				if (_star != null)
				{
					UnityEngine.Object.Destroy(_star);
				}
			}
			catch { }
			_star = null;
			_starRenderers = null;
			_suctions.Clear();
		}
	}
}
