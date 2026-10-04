using System;
using HarmonyLib;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>终焉·撰星极帝机枪射手</summary>
	internal static class HolyStarGatlingPatches
	{
		private static bool IsOurs(Plant plant)
		{
			return HolyStarCombat.IsOurPlant(plant);
		}

		/// <summary>① 场上是否还有敌方僵尸（大招门槛）。</summary>
		internal static bool HasEnemyZombie()
		{
			try
			{
				Board board = Board.Instance;
				if (board == null || board.zombieArray == null)
				{
					return false;
				}
				Il2CppSystem.Collections.Generic.List<Zombie> zombies = board.zombieArray;
				int n = zombies.Count;
				for (int i = 0; i < n; i++)
				{
					Zombie z = zombies[i];
					if (z == null || z.beforeDying || z.isMindControlled)
					{
						continue;
					}
					if (z.theHealth > 0)
					{
						return true;
					}
				}
			}
			catch { }
			return false;
		}

		// ================================================================ A. 子弹

		/// <summary>子弹类型</summary>
		[HarmonyPatch(typeof(SuperSnowGatling), "GetBulletType")]
		internal static class SuperSnowGatling_GetBulletType_Holy
		{
			[HarmonyPostfix]
			private static void Postfix(SuperSnowGatling __instance, ref BulletType __result)
			{
				if (!IsOurs(__instance))
				{
					return;
				}
				__result = (BulletType)HolyStarGatling.BulletId;
			}
		}

		/// <summary>正射</summary>
		[HarmonyPatch(typeof(SuperSnowGatling), "Shoot1")]
		internal static class SuperSnowGatling_Shoot1_Holy
		{
			[HarmonyPostfix]
			private static void Postfix(SuperSnowGatling __instance, ref Bullet __result)
			{
				if (!IsOurs(__instance) || __result == null)
				{
					return;
				}
				try { HolyStarCombat.ConfigureBullet(__result, __instance, HolyStarGatling.BaseDamage); }
				catch { }
			}
		}

		/// <summary>大招散射自己出弹</summary>
		[HarmonyPatch(typeof(SuperSnowGatling), "SuperShoot", new Type[]
		{
			typeof(float), typeof(float), typeof(float), typeof(float), typeof(BulletMoveWay), typeof(int)
		})]
		internal static class SuperSnowGatling_SuperShoot_Bullet_Holy
		{
			[HarmonyPrefix]
			private static bool Prefix(SuperSnowGatling __instance, float angle, float speed, float x, float y, BulletMoveWay mw)
			{
				if (!IsOurs(__instance))
				{
					return true;
				}
				try
				{
					// 大帝子弹合并
					if (!StarBulletMerge.AllowSpawn(__instance, HolyStarCombat.ComputeDamage(__instance, HolyStarGatling.BaseDamage)))
					{
						return false;
					}
					CreateBullet factory = CreateBullet.Instance;
					if (factory == null)
					{
						return false;
					}
					Bullet bullet = factory.SetBullet(x, y, __instance.thePlantRow,
						(BulletType)HolyStarGatling.BulletId, mw, false);
					if (bullet == null)
					{
						return false;
					}
					bullet.transform.Rotate(0f, 0f, angle);
					bullet.normalSpeed = speed;
					HolyStarCombat.ConfigureBullet(bullet, __instance, HolyStarGatling.BaseDamage);
				}
				catch { }
				return false;
			}
		}

		/// <summary>换弹时的过火状态清理。</summary>
		[HarmonyPatch(typeof(Bullet_star_holy), "InitData")]
		internal static class Bullet_star_holy_InitData_Holy
		{
			[HarmonyPostfix]
			private static void Postfix(Bullet_star_holy __instance)
			{
				if (__instance == null || !HolyStarCombat.IsHolyGatlingOnBoard())
				{
					return;
				}
				if (UltimateStarGatlingPatches.GetTorchMark(__instance) != UltimateStarGatlingTorch.EnhanceKind.None)
				{
					return;
				}
				HolyStarTorch.ResetBulletState(__instance);
			}
		}

		/// <summary>命中：过火命中效果 + 充能 + 究极流星与卫星。</summary>
		[HarmonyPatch(typeof(Bullet_star_holy), "HitZombie")]
		internal static class Bullet_star_holy_HitZombie_Holy
		{
			[HarmonyPrefix]
			private static void Prefix(Bullet_star_holy __instance, Zombie zombie)
			{
				if (__instance == null || zombie == null)
				{
					return;
				}
				if (UltimateStarGatlingPatches.GetTorchMark(__instance) == UltimateStarGatlingTorch.EnhanceKind.HypnoOnKill)
				{
					UltimateStarGatlingTorch.MarkHypnoPending(zombie);
				}
			}

			[HarmonyPostfix]
			private static void Postfix(Bullet_star_holy __instance, Zombie zombie)
			{
				if (__instance == null)
				{
					return;
				}
				try
				{
					UltimateStarGatlingTorch.EnhanceKind mark = UltimateStarGatlingPatches.GetTorchMark(__instance);
					if (mark != UltimateStarGatlingTorch.EnhanceKind.None && zombie != null)
					{
						HolyStarTorch.ApplyHitEffect(__instance, zombie, mark);
					}
					UltimateStarGatlingPatches.ClearTorchMark(__instance);
				}
				catch { }

				if (!HolyStarCombat.IsOurBullet(__instance))
				{
					return;
				}

				try
				{
					HolyStarGatlingBehaviour behaviour = HolyStarGatlingBehaviour.TryGetBehaviour(__instance.from);
					if (behaviour != null)
					{
						behaviour.AddCharge(1f);
					}
				}
				catch { }

				if (UnityEngine.Random.value < HolyStarGatling.MeteorProcChance)
				{
					HolyStarCombat.TrySpawnUltimateMeteor();
					HolyStarCombat.SpawnSatellite(__instance.from);
				}

				HolyStarCombat.UnregisterBullet(__instance);
			}
		}

		/// <summary>魅惑火炬击杀生成魅惑僵尸</summary>
		[HarmonyPatch(typeof(Zombie), "Die")]
		internal static class Zombie_Die_HypnoClone_Holy
		{
			[HarmonyPostfix]
			private static void Postfix(Zombie __instance)
			{
				if (__instance == null || !UltimateStarGatlingTorch.HasHypnoPending)
				{
					return;
				}
				if (!HolyStarCombat.IsHolyGatlingOnBoard())
				{
					return;
				}
				UltimateStarGatlingTorch.TrySpawnHypnoCloneOnDeath(__instance);
			}
		}

		// ================================================================ B. ① 全场开火 / 大招门槛

		/// <summary>① 无论场上是否有敌人都发动攻击。</summary>
		[HarmonyPatch(typeof(Plant), "Shootable")]
		internal static class Plant_Shootable_HolyAlwaysFire
		{
			[HarmonyPostfix]
			private static void Postfix(Plant __instance, ref bool __result)
			{
				if (__result || !HolyStarCombat.IsHolyGatlingOnBoard() || !IsOurs(__instance))
				{
					return;
				}
				try
				{
					HolyStarGatlingBehaviour behaviour = HolyStarGatlingBehaviour.TryGetBehaviour(__instance);
					if (behaviour == null)
					{
						return; // 组件还没就位
					}
					if (behaviour.AliveSeconds < HolyStarGatling.AttackWarmupSeconds)
					{
						return; // 未满 4 秒
					}
				}
				catch { return; }
				__result = true;
			}
		}

		/// <summary>① 场上没有敌人时不触发大招。</summary>
		[HarmonyPatch(typeof(SuperSnowGatling), "SuperShoot", new Type[] { typeof(float) })]
		internal static class SuperSnowGatling_SuperShoot_NoEnemyGate
		{
			[HarmonyPrefix]
			private static bool Prefix(SuperSnowGatling __instance)
			{
				if (!IsOurs(__instance))
				{
					return true;
				}
				return HasEnemyZombie();
			}
		}

		// ================================================================ C. ⑤ 过火

		[HarmonyPatch(typeof(TorchWood), "OnTriggerEnter2D")]
		internal static class TorchWood_OnTriggerEnter2D_Holy
		{
			[HarmonyPrefix]
			private static bool Prefix(TorchWood __instance, Collider2D collision)
			{
				return !HolyStarTorch.ShouldSkipVanillaTorchHandler(__instance, collision);
			}

			[HarmonyPostfix]
			private static void Postfix(TorchWood __instance, Collider2D collision)
			{
				HolyStarTorch.OnTorchTriggerEnter(__instance, collision);
			}
		}

		[HarmonyPatch(typeof(ThreeTorch), "OnTriggerEnter2D")]
		internal static class ThreeTorch_OnTriggerEnter2D_Holy
		{
			[HarmonyPrefix]
			private static bool Prefix(ThreeTorch __instance, Collider2D collision)
			{
				return !HolyStarTorch.ShouldSkipVanillaTorchHandler(__instance, collision);
			}

			[HarmonyPostfix]
			private static void Postfix(ThreeTorch __instance, Collider2D collision)
			{
				HolyStarTorch.OnTorchTriggerEnter(__instance, collision);
			}
		}

		[HarmonyPatch(typeof(CherryTorch), "OnTriggerEnter2D")]
		internal static class CherryTorch_OnTriggerEnter2D_Holy
		{
			[HarmonyPrefix]
			private static bool Prefix(CherryTorch __instance, Collider2D collision)
			{
				return !HolyStarTorch.ShouldSkipVanillaTorchHandler(__instance, collision);
			}

			[HarmonyPostfix]
			private static void Postfix(CherryTorch __instance, Collider2D collision)
			{
				HolyStarTorch.OnTorchTriggerEnter(__instance, collision);
			}
		}

		[HarmonyPatch(typeof(IceTorch), "OnTriggerEnter2D")]
		internal static class IceTorch_OnTriggerEnter2D_Holy
		{
			[HarmonyPrefix]
			private static bool Prefix(IceTorch __instance, Collider2D collision)
			{
				return !HolyStarTorch.ShouldSkipVanillaTorchHandler(__instance, collision);
			}

			[HarmonyPostfix]
			private static void Postfix(IceTorch __instance, Collider2D collision)
			{
				HolyStarTorch.OnTorchTriggerEnter(__instance, collision);
			}
		}

		[HarmonyPatch(typeof(DoomTorch), "OnTriggerEnter2D")]
		internal static class DoomTorch_OnTriggerEnter2D_Holy
		{
			[HarmonyPrefix]
			private static bool Prefix(DoomTorch __instance, Collider2D collision)
			{
				return !HolyStarTorch.ShouldSkipVanillaTorchHandler(__instance, collision);
			}

			[HarmonyPostfix]
			private static void Postfix(DoomTorch __instance, Collider2D collision)
			{
				HolyStarTorch.OnTorchTriggerEnter(__instance, collision);
			}
		}

		[HarmonyPatch(typeof(TorchWood), "CheckFire")]
		internal static class TorchWood_CheckFire_Holy
		{
			[HarmonyPrefix]
			private static bool Prefix(TorchWood __instance, Bullet bullet, ref bool __result)
			{
				try
				{
					if (!HolyStarTorch.ShouldHandle(bullet) || !HolyStarTorch.IsSupportedTorch(__instance))
					{
						return true;
					}
					if (HolyStarCombat.HasPassedShootPoint(bullet, null))
					{
						return true;
					}
					__result = false;
					return false;
				}
				catch { return true; }
			}
		}

		// ================================================================ D. ⑥ 超新星窗口 / ⑦ 复活

		/// <summary> 点击释放超新星</summary>
		[HarmonyPatch(typeof(Mouse), "LeftClickWithNothing")]
		internal static class Mouse_LeftClickWithNothing_HolySupernova
		{
			private static bool _entryLogged;
			private static float _diagAt = -99f;

			[HarmonyPrefix]
			private static bool Prefix(Mouse __instance)
			{
				try
				{
					if (__instance == null)
					{
						return true;
					}
					if (!_entryLogged)
					{
						_entryLogged = true;
						HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 左键入口已生效（Mouse.LeftClickWithNothing 补丁被调用）");
					}

					HolyStarGatlingBehaviour behaviour = FindOurClickedPlant(__instance);
					if (behaviour == null)
					{
						return true;
					}
					if (behaviour.TryClickAction())
					{
						return false; // 消费掉这次点击
					}
					return true;
				}
				catch (Exception ex)
				{
					if (Time.time - _diagAt > 2f)
					{
						_diagAt = Time.time;
						HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 左键入口异常：" + ex.Message);
					}
				}
				return true;
			}

			/// <summary>找鼠标下的自家植物</summary>
			private static HolyStarGatlingBehaviour FindOurClickedPlant(Mouse mouse)
			{
				try
				{
					Il2CppSystem.Collections.Generic.List<Plant> plants = mouse.GetPlantsOnMouse();
					if (plants != null)
					{
						int count = plants.Count;
						for (int i = 0; i < count; i++)
						{
							HolyStarGatlingBehaviour behaviour = HolyStarGatlingBehaviour.TryGetBehaviour(plants[i]);
							if (behaviour != null)
							{
								return behaviour;
							}
						}
					}
				}
				catch { }

				try
				{
					Il2CppSystem.Collections.Generic.List<Plant> cell =
						Lawnf.Get1x1Plants(mouse.theMouseColumn, mouse.theMouseRow);
					if (cell != null)
					{
						int count = cell.Count;
						for (int i = 0; i < count; i++)
						{
							HolyStarGatlingBehaviour behaviour = HolyStarGatlingBehaviour.TryGetBehaviour(cell[i]);
							if (behaviour != null)
							{
								return behaviour;
							}
						}
					}
				}
				catch { }
				return null;
			}
		}

		/// <summary>⑥ 超新星窗口内全场植物免碾压</summary>
		[HarmonyPatch(typeof(TypeMgr), "UncrashablePlant")]
		internal static class TypeMgr_UncrashablePlant_Holy
		{
			[HarmonyPrefix]
			private static bool Prefix(ref bool __result)
			{
				if (!HolyStarCombat.SupernovaActive)
				{
					return true;
				}
				__result = true;
				return false;
			}
		}

		/// <summary>⑥ 超新星窗口内免碾压</summary>
		[HarmonyPatch(typeof(Plant), "Crashed")]
		internal static class Plant_Crashed_Holy
		{
			[HarmonyPrefix]
			private static bool Prefix(Plant __instance)
			{
				try
				{
					if (__instance != null && !__instance.isCrashed && HolyStarCombat.SupernovaActive)
					{
						return false;
					}
				}
				catch { }
				return true;
			}
		}

		/// <summary>⑥ 同上</summary>
		[HarmonyPatch(typeof(SuperSnowGatling), "Crashed")]
		internal static class SuperSnowGatling_Crashed_Holy
		{
			[HarmonyPrefix]
			private static bool Prefix(SuperSnowGatling __instance)
			{
				return !HolyStarCombat.SupernovaActive;
			}
		}

		/// <summary>⑦ 首次死亡（非铲除）化作究极流星重返战场，并重置全部状态。</summary>
		[HarmonyPatch(typeof(Plant), "Die")]
		internal static class Plant_Die_HolyRevive
		{
			[HarmonyPrefix]
			private static bool Prefix(Plant __instance, Plant.DieReason reason)
			{
				if (!IsOurs(__instance))
				{
					return true;
				}
				// 铲除照常死亡
				if (reason == Plant.DieReason.ByShovel)
				{
					return true;
				}
				// ⑦ 复活名额
				HolyStarGatlingBehaviour reviveBehaviour = HolyStarGatlingBehaviour.TryGetBehaviour(__instance);
				if (reviveBehaviour != null && !reviveBehaviour.TryConsumeRevive())
				{
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 复活名额已用完（" +
						HolyStarGatling.MaxRevives + " 次），按普通植物正常死亡");
					return true;
				}
				try
				{
					__instance.thePlantHealth = __instance.thePlantMaxHealth;
					__instance.dying = false;
					__instance.isCrashed = false;
					try { __instance.UpdateText(); }
					catch { }
					try { __instance.Recover(0f, DamageType.Normal, true, false); }
					catch { }

					HolyStarCombat.TrySpawnUltimateMeteor();
					HolyStarCombat.SpawnSatellite(__instance);

					if (reviveBehaviour != null)
					{
						reviveBehaviour.ResetAllState();
					}
					HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 首次死亡触发：化作究极流星重返战场并重置全部状态（原因 " + reason + "）");
				}
				catch (Exception ex)
				{
					HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 复活失败：" + ex.Message);
				}
				return false;
			}
		}

		/// <summary>换关清场</summary>
		[HarmonyPatch(typeof(Board), "Start")]
		internal static class Board_Start_HolyReset
		{
			[HarmonyPostfix]
			private static void Postfix()
			{
				HolyStarCombat.ResetLevelState();
			}
		}
	}
}
