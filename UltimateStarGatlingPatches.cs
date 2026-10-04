using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace UltimateStarGatling.BepInEx
{
			internal static class UltimateStarGatlingPatches
	{
		internal static UltimateStarGatlingTorch.EnhanceKind GetTorchMark(Bullet bullet)
		{
			if (bullet == null)
			{
				return UltimateStarGatlingTorch.EnhanceKind.None;
			}
			UltimateStarGatlingTorch.EnhanceKind result;
			if (!UltimateStarGatlingPatches.TorchMarks.TryGetValue(bullet.Pointer, out result))
			{
				return UltimateStarGatlingTorch.EnhanceKind.None;
			}
			return result;
		}

		internal static void SetTorchMark(Bullet bullet, UltimateStarGatlingTorch.EnhanceKind kind)
		{
			if (bullet == null)
			{
				return;
			}
			if (kind == UltimateStarGatlingTorch.EnhanceKind.None)
			{
				UltimateStarGatlingPatches.TorchMarks.Remove(bullet.Pointer);
				return;
			}
			UltimateStarGatlingPatches.TorchMarks[bullet.Pointer] = kind;
		}

		internal static void ClearTorchMark(Bullet bullet)
		{
			if (bullet == null)
			{
				return;
			}
			UltimateStarGatlingPatches.TorchMarks.Remove(bullet.Pointer);
		}

		internal static void ResetLevelTorchMarks()
		{
			UltimateStarGatlingPatches.TorchMarks.Clear();
			// 染色记录与过火诊断去重表一起清
			UltimateStarGatlingTorch.ResetTintRecords();
		}

				private static bool IsOurGatling(SuperSnowGatling gatling)
		{
			return gatling != null && gatling.thePlantType ==(PlantType)5166;
		}

				private static UltimateStarGatlingBehaviour GetBehaviour(SuperSnowGatling gatling)
		{
			if (!(gatling != null))
			{
				return null;
			}
			return UltimateStarGatlingBehaviour.TryGetBehaviour(gatling);
		}

				private static UltimateStarGatlingBehaviour GetBehaviourFromPlant(Plant plant)
		{
			return UltimateStarGatlingBehaviour.TryGetBehaviour(plant);
		}

		private static readonly System.Collections.Generic.Dictionary<IntPtr, UltimateStarGatlingTorch.EnhanceKind> TorchMarks = new System.Collections.Generic.Dictionary<IntPtr, UltimateStarGatlingTorch.EnhanceKind>();

				[HarmonyPatch(typeof(CreateBullet), "SetBullet")]
		internal static class CreateBullet_SetBullet_SanitizeUltimateStar
		{
						[HarmonyPostfix]
			private static void Postfix(Bullet __result, BulletType theBulletType)
			{
				if (__result == null || theBulletType != (BulletType)49)
				{
					return;
				}
				if (!UltimateStarGatlingCombat.IsAnyGatlingOnBoard())
				{
					return;
				}
				if (UltimateStarGatlingPatches.GetTorchMark(__result) == UltimateStarGatlingTorch.EnhanceKind.None)
				{
					UltimateStarGatlingCombat.ResetBulletTorchState(__result);
				}
			}
		}

				[HarmonyPatch(typeof(Bullet_ultimateStar), "InitData")]
		internal static class Bullet_ultimateStar_InitData_SanitizeTorch
		{
						[HarmonyPostfix]
			private static void Postfix(Bullet_ultimateStar __instance)
			{
				if (__instance == null || !UltimateStarGatlingCombat.IsAnyGatlingOnBoard())
				{
					return;
				}
				if (UltimateStarGatlingPatches.GetTorchMark(__instance) != UltimateStarGatlingTorch.EnhanceKind.None)
				{
					return;
				}
				UltimateStarGatlingCombat.ResetBulletTorchState(__instance);
			}
		}

				[HarmonyPatch(typeof(TorchWood), "CheckFire")]
		internal static class TorchWood_CheckFire_UltimateStarGate
		{
						[HarmonyPrefix]
			private static bool Prefix(Bullet bullet, ref bool __result)
			{
				if (!UltimateStarGatlingTorch.ShouldHandleUltimateStarTorch(bullet))
				{
					return true;
				}
				if (UltimateStarGatlingCombat.HasPassedShootPoint(bullet, null))
				{
					return true;
				}
				__result = false;
				return false;
			}
		}

				[HarmonyPatch(typeof(Bullet_ultimateStar), "Update")]
		internal static class Bullet_ultimateStar_Update_TorchRetry
		{
						[HarmonyPostfix]
			private static void Postfix(Bullet_ultimateStar __instance)
			{
				if (__instance == null)
				{
					return;
				}
				if (!UltimateStarGatlingCombat.HasPendingTorch)
				{
					return;
				}
				if (!UltimateStarGatlingCombat.IsUltimateStarBullet(__instance))
				{
					return;
				}
				UltimateStarGatlingCombat.ProcessPendingTorch(__instance);
			}
		}

		/// <summary>每帧维护（两类星弹共用）：① 处理贴脸树桩排队重试 ② 重申过火染色。</summary>
		[HarmonyPatch(typeof(Bullet_star), "Update")]
		internal static class Bullet_star_Update_TorchMaintain
		{
			[HarmonyPostfix]
			private static void Postfix(Bullet_star __instance)
			{
				if (__instance == null)
				{
					return;
				}
				try
				{
					if (UltimateStarGatlingCombat.HasPendingTorch)
					{
						UltimateStarGatlingCombat.ProcessPendingTorch(__instance);
					}
				}
				catch
				{
				}
				UltimateStarGatlingTorch.ReassertTint(__instance);
			}
		}

				[HarmonyPatch(typeof(SuperSnowGatling), "GetBulletType")]
		internal static class SuperSnowGatling_GetBulletType_Patch
		{
						[HarmonyPostfix]
			private static void Postfix(SuperSnowGatling __instance, ref BulletType __result)
			{
				if (!UltimateStarGatlingPatches.IsOurGatling(__instance))
				{
					return;
				}
				__result = (BulletType)49;
			}
		}

				[HarmonyPatch(typeof(SuperSnowGatling), "SuperShoot", new Type[] { typeof(float), typeof(float), typeof(float), typeof(float), typeof(BulletMoveWay), typeof(int) })]
		internal static class SuperSnowGatling_SuperShoot_Patch
		{
						[HarmonyPrefix]
			private static bool Prefix(SuperSnowGatling __instance, ref float angle, ref float speed, ref float x, ref float y, ref BulletMoveWay mw, ref int dmg)
			{
				if (!UltimateStarGatlingPatches.IsOurGatling(__instance))
				{
					return true;
				}
				// 大帝子弹合并：超限时这一发不生成
				if (!StarBulletMerge.AllowSpawn(__instance, UltimateStarGatlingCombat.ComputeBulletDamage(__instance, Core.BaseAttackDamage)))
				{
					return false;
				}
				Bullet bullet = CreateBullet.Instance.SetBullet(x, y, __instance.thePlantRow, (BulletType)49, mw, false);
				if (bullet == null)
				{
					return false;
				}
				bullet.transform.Rotate(0f, 0f, angle);
				bullet.normalSpeed = speed;
				UltimateStarGatlingCombat.ConfigureStarBullet(bullet, __instance, 50);
				return false;
			}
		}

				[HarmonyPatch(typeof(SuperSnowGatling), "Shoot1")]
		internal static class SuperSnowGatling_Shoot1_Patch
		{
						[HarmonyPostfix]
			private static void Postfix(SuperSnowGatling __instance, ref Bullet __result)
			{
				if (!UltimateStarGatlingPatches.IsOurGatling(__instance))
				{
					return;
				}
				if (__result != null)
				{
					UltimateStarGatlingCombat.ConfigureStarBullet(__result, __instance, 50);
				}
				if (UltimateStarGatlingPatches.SuperSnowGatling_Shoot1_Patch._shoot1StarModeExtraActive)
				{
					return;
				}
				UltimateStarGatlingBehaviour behaviour = UltimateStarGatlingPatches.GetBehaviour(__instance);
				if (behaviour == null || !behaviour.IsStarModeActive || UltimateStarGatlingPatches.SuperSnowGatling_Shoot1_Patch.Shoot1Method == null)
				{
					return;
				}
				UltimateStarGatlingPatches.SuperSnowGatling_Shoot1_Patch._shoot1StarModeExtraActive = true;
				try
				{
					for (int i = 0; i < 2; i++)
					{
						UltimateStarGatlingPatches.SuperSnowGatling_Shoot1_Patch.Shoot1Method.Invoke(__instance, null);
					}
				}
				catch
				{
				}
				finally
				{
					UltimateStarGatlingPatches.SuperSnowGatling_Shoot1_Patch._shoot1StarModeExtraActive = false;
				}
			}

						private static readonly MethodInfo Shoot1Method = AccessTools.Method(typeof(SuperSnowGatling), "Shoot1", null, null);

			private static bool _shoot1StarModeExtraActive;
		}

				[HarmonyPatch(typeof(Bullet_ultimateStar), "HitZombie")]
		internal static class Bullet_ultimateStar_HitZombie_Patch
		{
			[HarmonyPrefix]
			private static void Prefix(Bullet_ultimateStar __instance, Zombie zombie)
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
			private static void Postfix(Bullet_ultimateStar __instance, Zombie zombie)
			{
				if (__instance == null || zombie == null)
				{
					return;
				}
				UltimateStarGatlingTorch.EnhanceKind torchMark = UltimateStarGatlingPatches.GetTorchMark(__instance);
				if (torchMark != UltimateStarGatlingTorch.EnhanceKind.None)
				{
					UltimateStarGatlingTorch.ApplyHitEffect(__instance, zombie, torchMark);
				}
				UltimateStarGatlingPatches.ClearTorchMark(__instance);
				if (!UltimateStarGatlingCombat.IsOurBullet(__instance))
				{
					return;
				}
				try
				{
					UltimateStarGatlingBehaviour behaviourFromPlant = UltimateStarGatlingPatches.GetBehaviourFromPlant(__instance.from);
					if (behaviourFromPlant != null)
					{
						behaviourFromPlant.AddCharge(1f);
					}
				}
				catch
				{
				}
				if (UltimateStarGatlingCombat.IsDirectShotBullet(__instance) && UnityEngine.Random.value < 0.005f)
				{
					UltimateStarGatlingCombat.TrySpawnUltimateMeteor();
				}
				UltimateStarGatlingCombat.UnregisterDirectShotBullet(__instance);
			}
		}

				[HarmonyPatch(typeof(Bullet_cherrySquash), "HitZombie")]
		internal static class Bullet_cherrySquash_UltimateTorchHit_Patch
		{
						[HarmonyPostfix]
			private static void Postfix(Bullet_cherrySquash __instance, Zombie zombie)
			{
				if (__instance == null || zombie == null)
				{
					return;
				}
				if (!UltimateStarGatlingCombat.IsAnyGatlingOnBoard())
				{
					return;
				}
				UltimateStarGatlingTorch.EnhanceKind torchMark = UltimateStarGatlingPatches.GetTorchMark(__instance);
				if (torchMark != UltimateStarGatlingTorch.EnhanceKind.JalaOnHit)
				{
					return;
				}
				UltimateStarGatlingTorch.ApplyHitEffect(__instance, zombie, torchMark);
				UltimateStarGatlingPatches.ClearTorchMark(__instance);
			}
		}

				[HarmonyPatch(typeof(Board), "Start")]
		internal static class Board_Start_ClearDirectShotBullets
		{
			[HarmonyPostfix]
			private static void Postfix()
			{
				UltimateStarGatlingCombat.ResetLevelState();
			}
		}

				[HarmonyPatch(typeof(Zombie), "Die")]
		internal static class Zombie_Die_HypnoTorchClone
		{
						[HarmonyPostfix]
			private static void Postfix(Zombie __instance)
			{
				if (__instance == null || !UltimateStarGatlingTorch.HasHypnoPending)
				{
					return;
				}
				if (!UltimateStarGatlingCombat.IsAnyGatlingOnBoard())
				{
					return;
				}
				UltimateStarGatlingTorch.TrySpawnHypnoCloneOnDeath(__instance);
			}
		}

				[HarmonyPatch(typeof(TypeMgr), "UncrashablePlant")]
		internal static class TypeMgr_UncrashablePlant_Prefix
		{
						[HarmonyPrefix]
			private static bool Prefix(Plant plant, ref bool __result)
			{
				try
				{
					if (UltimateStarGatlingBehaviour.IsUncrashableInStarMode(plant))
					{
						__result = true;
						return false;
					}
				}
				catch (Exception ex)
				{
					ManualLogSource pluginLogger = Core.PluginLogger;
					if (pluginLogger != null)
					{
												BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(39, 1, out bool flag);
						if (flag)
						{
							bepInExWarningLogInterpolatedStringHandler.AppendLiteral("[UltimateStarGatling] UncrashablePlant：");
							bepInExWarningLogInterpolatedStringHandler.AppendFormatted<string>(ex.Message);
						}
						pluginLogger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
					}
				}
				return true;
			}
		}

				[HarmonyPatch(typeof(SuperSnowGatling), "Crashed")]
		internal static class SuperSnowGatling_Crashed_StarModeImmune
		{
						[HarmonyPrefix]
			private static bool Prefix(SuperSnowGatling __instance)
			{
				return !UltimateStarGatlingPatches.IsOurGatling(__instance) || !UltimateStarGatlingBehaviour.IsUncrashableInStarMode(__instance);
			}
		}

				[HarmonyPatch(typeof(Plant), "Crashed")]
		internal static class Plant_Crashed_StarModeImmune
		{
						[HarmonyPrefix]
			private static bool Prefix(Plant __instance)
			{
				try
				{
					if (__instance != null && !__instance.isCrashed && UltimateStarGatlingBehaviour.IsUncrashableInStarMode(__instance))
					{
						return false;
					}
				}
				catch (Exception ex)
				{
					ManualLogSource pluginLogger = Core.PluginLogger;
					if (pluginLogger != null)
					{
												BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(30, 1, out bool flag);
						if (flag)
						{
							bepInExWarningLogInterpolatedStringHandler.AppendLiteral("[UltimateStarGatling] Crashed：");
							bepInExWarningLogInterpolatedStringHandler.AppendFormatted<string>(ex.Message);
						}
						pluginLogger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
					}
				}
				return true;
			}
		}

				private static class UltimateStarTorchTriggerPatchBase
		{
			internal static bool Prefix(Collider2D collision)
			{
				return !UltimateStarGatlingTorch.ShouldSkipVanillaTorchHandler(collision);
			}

			internal static void Postfix(Plant torch, Collider2D collision)
			{
				UltimateStarGatlingTorch.OnTorchTriggerEnter(torch, collision);
			}
		}

				[HarmonyPatch(typeof(TorchWood), "OnTriggerEnter2D")]
		internal static class TorchWood_OnTriggerEnter2D_Patch
		{
			[HarmonyPrefix]
			private static bool Prefix(Collider2D collision)
			{
				return UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Prefix(collision);
			}

			[HarmonyPostfix]
			private static void Postfix(TorchWood __instance, Collider2D collision)
			{
				UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Postfix(__instance, collision);
			}
		}

				[HarmonyPatch(typeof(IceTorch), "OnTriggerEnter2D")]
		internal static class IceTorch_OnTriggerEnter2D_Patch
		{
			[HarmonyPrefix]
			private static bool Prefix(Collider2D collision)
			{
				return UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Prefix(collision);
			}

			[HarmonyPostfix]
			private static void Postfix(IceTorch __instance, Collider2D collision)
			{
				UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Postfix(__instance, collision);
			}
		}

				[HarmonyPatch(typeof(DoomTorch), "OnTriggerEnter2D")]
		internal static class DoomTorch_OnTriggerEnter2D_Patch
		{
			[HarmonyPrefix]
			private static bool Prefix(Collider2D collision)
			{
				return UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Prefix(collision);
			}

			[HarmonyPostfix]
			private static void Postfix(DoomTorch __instance, Collider2D collision)
			{
				UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Postfix(__instance, collision);
			}
		}

				[HarmonyPatch(typeof(JalaTorch), "OnTriggerEnter2D")]
		internal static class JalaTorch_OnTriggerEnter2D_Patch
		{
			[HarmonyPrefix]
			private static bool Prefix(Collider2D collision)
			{
				return UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Prefix(collision);
			}

			[HarmonyPostfix]
			private static void Postfix(JalaTorch __instance, Collider2D collision)
			{
				UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Postfix(__instance, collision);
			}
		}

				[HarmonyPatch(typeof(CherryTorch), "OnTriggerEnter2D")]
		internal static class CherryTorch_OnTriggerEnter2D_Patch
		{
			[HarmonyPrefix]
			private static bool Prefix(Collider2D collision)
			{
				return UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Prefix(collision);
			}

			[HarmonyPostfix]
			private static void Postfix(CherryTorch __instance, Collider2D collision)
			{
				UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Postfix(__instance, collision);
			}
		}

				[HarmonyPatch(typeof(UltimateTorch), "OnTriggerEnter2D")]
		internal static class UltimateTorch_OnTriggerEnter2D_Patch
		{
			[HarmonyPrefix]
			private static bool Prefix(Collider2D collision)
			{
				return UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Prefix(collision);
			}

			[HarmonyPostfix]
			private static void Postfix(UltimateTorch __instance, Collider2D collision)
			{
				UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Postfix(__instance, collision);
			}
		}

				[HarmonyPatch(typeof(UltimateStarTorch), "OnTriggerEnter2D")]
		internal static class UltimateStarTorch_OnTriggerEnter2D_Patch
		{
			[HarmonyPrefix]
			private static bool Prefix(Collider2D collision)
			{
				return UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Prefix(collision);
			}

			[HarmonyPostfix]
			private static void Postfix(UltimateStarTorch __instance, Collider2D collision)
			{
				UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Postfix(__instance, collision);
			}
		}

				[HarmonyPatch(typeof(ThreeTorch), "OnTriggerEnter2D")]
		internal static class ThreeTorch_OnTriggerEnter2D_Patch
		{
			[HarmonyPrefix]
			private static bool Prefix(Collider2D collision)
			{
				return UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Prefix(collision);
			}

			[HarmonyPostfix]
			private static void Postfix(ThreeTorch __instance, Collider2D collision)
			{
				UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Postfix(__instance, collision);
			}
		}

				[HarmonyPatch(typeof(SuperTorch), "OnTriggerEnter2D")]
		internal static class SuperTorch_OnTriggerEnter2D_Patch
		{
			[HarmonyPrefix]
			private static bool Prefix(Collider2D collision)
			{
				return UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Prefix(collision);
			}

			[HarmonyPostfix]
			private static void Postfix(SuperTorch __instance, Collider2D collision)
			{
				UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Postfix(__instance, collision);
			}
		}

				[HarmonyPatch(typeof(HypnoTorch), "OnTriggerEnter2D")]
		internal static class HypnoTorch_OnTriggerEnter2D_Patch
		{
			[HarmonyPrefix]
			private static bool Prefix(Collider2D collision)
			{
				return UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Prefix(collision);
			}

			[HarmonyPostfix]
			private static void Postfix(HypnoTorch __instance, Collider2D collision)
			{
				UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Postfix(__instance, collision);
			}
		}

				[HarmonyPatch(typeof(GarlicTorch), "OnTriggerEnter2D")]
		internal static class GarlicTorch_OnTriggerEnter2D_Patch
		{
			[HarmonyPrefix]
			private static bool Prefix(Collider2D collision)
			{
				return UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Prefix(collision);
			}

			[HarmonyPostfix]
			private static void Postfix(GarlicTorch __instance, Collider2D collision)
			{
				UltimateStarGatlingPatches.UltimateStarTorchTriggerPatchBase.Postfix(__instance, collision);
			}
		}
	}
}
