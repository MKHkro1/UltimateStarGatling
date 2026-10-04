using System;
using System.Collections;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace UltimateStarGatling.BepInEx
{
			internal static class UltimateStarGatlingSupernova
	{
		internal static int ActiveTintTargetCount { get; private set; }

		internal static void TriggerBurst(Plant source, MonoBehaviour host)
		{
			if (source == null || host == null)
			{
				return;
			}
			Board board = (source.board != null) ? source.board : Board.Instance;
			if (board == null || CreatePlant.Instance == null)
			{
				return;
			}
			MonoBehaviourExtensions.StartCoroutine(host, UltimateStarGatlingSupernova.TriggerBurstRoutine(source, host, board));
		}

		[HideFromIl2Cpp]
		private static IEnumerator TriggerBurstRoutine(Plant source, MonoBehaviour host, Board board)
		{
			UltimatePlantern plantern = UltimateStarGatlingSupernova.AcquireShrinkCaster(source, board);
			if (plantern == null)
			{
				ManualLogSource pluginLogger = Core.PluginLogger;
				if (pluginLogger != null)
				{
					pluginLogger.LogWarning("[UltimateStarGatling] 超新星爆发：无法获取究极路灯花载体");
				}
				yield break;
			}
			bool isTemp = UltimateStarGatlingSupernova.IsTempProxy(plantern);
			yield return null;
			yield return null;
			UltimateStarGatlingSupernova.AlignShrinkOrigin(plantern, source);
			if (!UltimateStarGatlingSupernova.TryTriggerNativeSupernova(plantern))
			{
				ManualLogSource pluginLogger2 = Core.PluginLogger;
				if (pluginLogger2 != null)
				{
					pluginLogger2.LogWarning("[UltimateStarGatling] 超新星爆发：原生 OnSuperCharge 未进入坍缩状态");
				}
				if (isTemp)
				{
					UltimateStarGatlingSupernova.TryDestroyTempProxy(plantern);
				}
				yield break;
			}
			MonoBehaviourExtensions.StartCoroutine(host, UltimateStarGatlingSupernova.TintSupernovaLightRoutine(plantern));
			if (!isTemp)
			{
				yield break;
			}
			float elapsed = 0f;
			while (elapsed < 20f)
			{
				if (plantern == null || plantern.dying)
				{
					yield break;
				}
				if (plantern.theStatus == (PlantStatus)42)
				{
					elapsed += Time.deltaTime;
					yield return null;
					continue;
				}
				break;
			}
			UltimateStarGatlingSupernova.TryDestroyTempProxy(plantern);
		}

		private static UltimatePlantern AcquireShrinkCaster(Plant source, Board board)
		{
			UltimatePlantern ultimatePlantern = UltimateStarGatlingSupernova.TrySpawnTempPlantern(source, board);
			if (ultimatePlantern != null)
			{
				return ultimatePlantern;
			}
			return UltimateStarGatlingSupernova.FindExistingPlantern(source);
		}

		private static UltimatePlantern TrySpawnTempPlantern(Plant source, Board board)
		{
			int num = 0;
			foreach (ValueTuple<int, int> valueTuple in UltimateStarGatlingSupernova.EnumerateSpawnCells(source, board))
			{
				int item = valueTuple.Item1;
				int item2 = valueTuple.Item2;
				num++;
				UltimatePlantern ultimatePlantern = UltimateStarGatlingSupernova.TrySetPlanternAt(item, item2);
				if (ultimatePlantern != null)
				{
					UltimateStarGatlingSupernova.TempProxyPlanterns.Add(ultimatePlantern.Pointer);
					return ultimatePlantern;
				}
			}
			ManualLogSource pluginLogger = Core.PluginLogger;
			if (pluginLogger != null)
			{
								BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(57, 1, out bool flag);
				if (flag)
				{
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral("[UltimateStarGatling] 临时路灯花生成失败（已尝试 ");
					bepInExWarningLogInterpolatedStringHandler.AppendFormatted<int>(num);
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral(" 个格子，可能未解锁究极路灯花或植物上限）");
				}
				pluginLogger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
			}
			return null;
		}

				private static UltimatePlantern TrySetPlanternAt(int col, int row)
		{
			try
			{
				Plant plant = CreatePlant.Instance.SetPlant(col, row, (PlantType)949, null, default(Vector2), true, false, null);
				UltimatePlantern ultimatePlantern = UltimateStarGatlingSupernova.AsUltimatePlantern(plant);
				if (ultimatePlantern != null)
				{
					return ultimatePlantern;
				}
				if (plant != null && !plant.dying)
				{
					try
					{
						plant.Die(0);
					}
					catch
					{
					}
				}
			}
			catch (Exception ex)
			{
				ManualLogSource pluginLogger = Core.PluginLogger;
				if (pluginLogger != null)
				{
										BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(37, 3, out bool flag);
					if (flag)
					{
						bepInExWarningLogInterpolatedStringHandler.AppendLiteral("[UltimateStarGatling] SetPlant(");
						bepInExWarningLogInterpolatedStringHandler.AppendFormatted<int>(col);
						bepInExWarningLogInterpolatedStringHandler.AppendLiteral(",");
						bepInExWarningLogInterpolatedStringHandler.AppendFormatted<int>(row);
						bepInExWarningLogInterpolatedStringHandler.AppendLiteral(") 异常：");
						bepInExWarningLogInterpolatedStringHandler.AppendFormatted<string>(ex.Message);
					}
					pluginLogger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
				}
			}
			return null;
		}

		private static System.Collections.Generic.IEnumerable<ValueTuple<int, int>> EnumerateSpawnCells(Plant source, Board board)
		{
			int columnNum = board.columnNum;
			int rowNum = board.rowNum;
			if (columnNum <= 0 || rowNum <= 0)
			{
				yield break;
			}
			UltimateStarGatlingSupernova.SpawnCellScratch.Clear();
			for (int i = 0; i < columnNum; i++)
			{
				for (int j = 0; j < rowNum; j++)
				{
					int score = Math.Abs(i - source.thePlantColumn) + Math.Abs(j - source.thePlantRow);
					if (i == source.thePlantColumn && j == source.thePlantRow)
					{
						score -= 10000;
					}
					UltimateStarGatlingSupernova.SpawnCellScratch.Add(new ValueTuple<int, int, int>(i, j, score));
				}
			}
			UltimateStarGatlingSupernova.SpawnCellScratch.Sort((ValueTuple<int, int, int> a, ValueTuple<int, int, int> b) => a.Item3.CompareTo(b.Item3));
			foreach (ValueTuple<int, int, int> item in UltimateStarGatlingSupernova.SpawnCellScratch)
			{
				yield return new ValueTuple<int, int>(item.Item1, item.Item2);
			}
		}

				private static UltimatePlantern AsUltimatePlantern(Plant plant)
		{
			if (plant == null || plant.dying)
			{
				return null;
			}
			if (plant.thePlantType !=(PlantType)949)
			{
				return null;
			}
			return plant.TryCast<UltimatePlantern>();
		}

		private static UltimatePlantern FindExistingPlantern(Plant source)
		{
			UltimatePlantern result;
			try
			{
				Il2CppSystem.Collections.Generic.List<Plant> allPlants = Lawnf.GetAllPlants();
				if (allPlants == null)
				{
					result = null;
				}
				else
				{
					UltimatePlantern ultimatePlantern = null;
					UltimatePlantern ultimatePlantern2 = null;
					UltimatePlantern ultimatePlantern3 = null;
					foreach (Plant plant in allPlants)
					{
						if (!(plant == null) && !plant.dying && plant.thePlantHealth > 0)
						{
							UltimatePlantern ultimatePlantern4 = UltimateStarGatlingSupernova.AsUltimatePlantern(plant);
							if (!(ultimatePlantern4 == null) && ultimatePlantern4.theStatus !=(PlantStatus)42)
							{
								ultimatePlantern3 = ultimatePlantern4;
								if (plant.thePlantRow == source.thePlantRow)
								{
									if (ultimatePlantern2 == null)
									{
										ultimatePlantern2 = ultimatePlantern4;
									}
								}
								else if (plant.thePlantColumn > source.thePlantColumn + 1 && ultimatePlantern == null)
								{
									ultimatePlantern = ultimatePlantern4;
								}
							}
						}
					}
					UltimatePlantern ultimatePlantern5;
					if ((ultimatePlantern5 = ultimatePlantern) == null)
					{
						ultimatePlantern5 = (ultimatePlantern2 ?? ultimatePlantern3);
					}
					result = ultimatePlantern5;
				}
			}
			catch
			{
				result = null;
			}
			return result;
		}

		private static void AlignShrinkOrigin(UltimatePlantern plantern, Plant source)
		{
			try
			{
				Vector3 position = (source.axis != null) ? source.axis.position : source.transform.position;
				if (plantern.axis != null)
				{
					plantern.axis.position = position;
				}
				else
				{
					plantern.transform.position = position;
				}
				SpriteRenderer ultimateLight = plantern.ultimateLight;
				if (ultimateLight != null)
				{
					ultimateLight.transform.position = position;
				}
			}
			catch
			{
			}
		}

		private static bool TryTriggerNativeSupernova(UltimatePlantern plantern)
		{
			if (plantern.theStatus ==(PlantStatus)42)
			{
				return false;
			}
			bool result;
			try
			{
				if (plantern.ultimateLight == null)
				{
					ManualLogSource pluginLogger = Core.PluginLogger;
					if (pluginLogger != null)
					{
						pluginLogger.LogWarning("[UltimateStarGatling] 超新星爆发：载体缺少 ultimateLight");
					}
					result = false;
				}
				else
				{
					plantern.ultimateLight.enabled = true;
					UltimateStarGatlingSupernova.RegisterTintTargets(plantern);
					plantern.theStatus =(PlantStatus)25;
					plantern.attributeCount = 100000;
					plantern.OnSuperCharge();
					if (plantern.theStatus ==(PlantStatus)42)
					{
						result = true;
					}
					else
					{
						UltimateStarGatlingSupernova.UnregisterTintTargets();
						result = false;
					}
				}
			}
			catch (Exception ex)
			{
				UltimateStarGatlingSupernova.UnregisterTintTargets();
				ManualLogSource pluginLogger2 = Core.PluginLogger;
				if (pluginLogger2 != null)
				{
										BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(41, 1, out bool flag);
					if (flag)
					{
						bepInExWarningLogInterpolatedStringHandler.AppendLiteral("[UltimateStarGatling] OnSuperCharge 调用失败：");
						bepInExWarningLogInterpolatedStringHandler.AppendFormatted<string>(ex.Message);
					}
					pluginLogger2.LogWarning(bepInExWarningLogInterpolatedStringHandler);
				}
				result = false;
			}
			return result;
		}

		private static bool IsTempProxy(UltimatePlantern plantern)
		{
			bool result;
			try
			{
				result = UltimateStarGatlingSupernova.TempProxyPlanterns.Contains(plantern.Pointer);
			}
			catch
			{
				result = false;
			}
			return result;
		}

		internal static bool IsTintTarget(System.IntPtr rendererPtr)
		{
			return UltimateStarGatlingSupernova.ActiveTintTargetCount > 0 && UltimateStarGatlingSupernova.ActiveTintTargets.Contains(rendererPtr);
		}

		internal static void ClearTintGpuState()
		{
			UltimateStarGatlingSupernova.LastAppliedBrightness.Clear();
			UltimateStarGatlingSupernova.PreviewRendererCache.Clear();
		}

		internal static bool TryApplyTintBrightness(SpriteRenderer renderer, Color tintedColor)
		{
			if (renderer == null)
			{
				return false;
			}
			bool result;
			try
			{
				IntPtr pointer = renderer.Pointer;
				float num = 1.75f * (0.4f + tintedColor.a * 0.35f);
				float num2;
				if (UltimateStarGatlingSupernova.LastAppliedBrightness.TryGetValue(pointer, out num2) && Mathf.Abs(num2 - num) < 0.02f)
				{
					result = false;
				}
				else
				{
					UltimateStarGatlingSupernova.SetRendererBrightness(renderer, num);
					UltimateStarGatlingSupernova.LastAppliedBrightness[pointer] = num;
					result = true;
				}
			}
			catch
			{
				result = false;
			}
			return result;
		}

		private static void SetRendererBrightness(SpriteRenderer renderer, float brightness)
		{
			if (renderer == null)
			{
				return;
			}
			Color color = renderer.color;
			float num = Mathf.Clamp(brightness, 0f, 4f);
			UltimateStarGatlingSupernova.SetRendererColorBypassPatch(renderer, new Color(color.r * num, color.g * num, color.b * num, color.a));
		}

		private static void SetRendererColorBypassPatch(SpriteRenderer renderer, Color value)
		{
			if (renderer == null)
			{
				return;
			}
			UltimateStarGatlingSupernova._colorPatchDepth++;
			try
			{
				renderer.color = value;
			}
			finally
			{
				UltimateStarGatlingSupernova._colorPatchDepth--;
			}
		}

		internal static int _colorPatchDepth;

		internal static Color ApplyTintToColor(Color value)
		{
			Color supernovaTintColor = Core.SupernovaTintColor;
			float num = Mathf.Max(value.r, Mathf.Max(value.g, value.b));
			float num2 = Mathf.Clamp01((num > 0.02f) ? num : value.a);
			return new Color(Mathf.Max(value.r * supernovaTintColor.r, supernovaTintColor.r * num2), Mathf.Max(value.g * supernovaTintColor.g, supernovaTintColor.g * num2), Mathf.Max(value.b * supernovaTintColor.b, supernovaTintColor.b * num2), value.a);
		}

		internal static Color ApplyFullTint(Color value)
		{
			value = UltimateStarGatlingSupernova.ApplyTintToColor(value);
			float num = Mathf.Clamp(1.75f * (0.4f + value.a * 0.35f), 0f, 4f);
			return new Color(value.r * num, value.g * num, value.b * num, value.a);
		}

		private static void RegisterTintTargets(UltimatePlantern plantern)
		{
			UltimateStarGatlingSupernova.ActiveTintTargets.Clear();
			UltimateStarGatlingSupernova.ActiveTintTargetCount = 0;
			try
			{
				SpriteRenderer ultimateLight = plantern.ultimateLight;
				if (!(ultimateLight == null))
				{
					UltimateStarGatlingSupernova.ActiveTintTargets.Add(ultimateLight.Pointer);
					UltimateStarGatlingSupernova.ActiveTintTargetCount = 1;
				}
			}
			catch
			{
			}
		}

		private static void UnregisterTintTargets()
		{
			UltimateStarGatlingSupernova.ActiveTintTargets.Clear();
			UltimateStarGatlingSupernova.ActiveTintTargetCount = 0;
			UltimateStarGatlingSupernova.ClearTintGpuState();
		}

		private static void AddTintTarget(System.IntPtr ptr)
		{
			if (UltimateStarGatlingSupernova.ActiveTintTargets.Add(ptr))
			{
				UltimateStarGatlingSupernova.ActiveTintTargetCount = UltimateStarGatlingSupernova.ActiveTintTargets.Count;
			}
		}

		[HideFromIl2Cpp]
		private static IEnumerator TintSupernovaLightRoutine(UltimatePlantern plantern)
		{
			System.Collections.Generic.HashSet<IntPtr> trackedPreviews = new System.Collections.Generic.HashSet<IntPtr>();
			float nextPreviewScanTime = 0f;
			try
			{
				while (plantern != null && !plantern.dying && plantern.theStatus == (PlantStatus)42)
				{
					if (Time.time >= nextPreviewScanTime)
					{
						nextPreviewScanTime = Time.time + 0.35f;
						UltimateStarGatlingSupernova.RegisterNearbyPreviewRenderers(plantern, trackedPreviews);
					}
					yield return null;
				}
			}
			finally
			{
				UltimateStarGatlingSupernova.UnregisterTintTargets();
			}
		}

		private static void RegisterNearbyPreviewRenderers(UltimatePlantern plantern, System.Collections.Generic.HashSet<IntPtr> tracked)
		{
			try
			{
				SpriteRenderer ultimateLight = plantern.ultimateLight;
				if (!(ultimateLight == null))
				{
					Vector3 position = ultimateLight.transform.position;
					if (!(((plantern.board != null) ? plantern.board : Board.Instance) == null))
					{
						Il2CppSystem.Collections.Generic.List<Zombie> allZombies = Lawnf.GetAllZombies(false);
						if (allZombies != null)
						{
							foreach (Zombie zombie in allZombies)
							{
								if (!(zombie == null))
								{
									GameObject gameObject = zombie.gameObject;
									if (!(gameObject == null) && gameObject.name.Contains("Preview", StringComparison.OrdinalIgnoreCase) && (gameObject.transform.position - position).sqrMagnitude <= 900f)
									{
										IntPtr pointer;
										try
										{
											pointer = zombie.Pointer;
										}
										catch
										{
											continue;
										}
										SpriteRenderer[] array;
										if (!UltimateStarGatlingSupernova.PreviewRendererCache.TryGetValue(pointer, out array) || array == null)
										{
											array = gameObject.GetComponentsInChildren<SpriteRenderer>(true);
											UltimateStarGatlingSupernova.PreviewRendererCache[pointer] = array;
										}
										foreach (SpriteRenderer spriteRenderer in array)
										{
											if (!(spriteRenderer == null) && !tracked.Contains(spriteRenderer.Pointer))
											{
												tracked.Add(spriteRenderer.Pointer);
												UltimateStarGatlingSupernova.AddTintTarget(spriteRenderer.Pointer);
											}
										}
									}
								}
							}
						}
					}
				}
			}
			catch
			{
			}
		}

		private static void TryDestroyTempProxy(UltimatePlantern proxy)
		{
			try
			{
				UltimateStarGatlingSupernova.TempProxyPlanterns.Remove(proxy.Pointer);
			}
			catch
			{
			}
			if (proxy == null || proxy.dying)
			{
				return;
			}
			try
			{
				proxy.Die(0);
			}
			catch
			{
			}
		}

		private const int SupernovaEnergyThreshold = 100000;

		private const float ProxyCleanupTimeoutSeconds = 20f;

		private const float PreviewScanIntervalSeconds = 0.35f;

		private const float PreviewScanRadiusSqr = 900f;

		private static readonly System.Collections.Generic.List<ValueTuple<int, int, int>> SpawnCellScratch = new System.Collections.Generic.List<ValueTuple<int, int, int>>(64);

		private static readonly System.Collections.Generic.HashSet<IntPtr> TempProxyPlanterns = new System.Collections.Generic.HashSet<IntPtr>();

		private static readonly System.Collections.Generic.HashSet<IntPtr> ActiveTintTargets = new System.Collections.Generic.HashSet<IntPtr>();

		private static readonly System.Collections.Generic.Dictionary<IntPtr, float> LastAppliedBrightness = new System.Collections.Generic.Dictionary<IntPtr, float>();

		private static readonly System.Collections.Generic.Dictionary<IntPtr, SpriteRenderer[]> PreviewRendererCache = new System.Collections.Generic.Dictionary<IntPtr, SpriteRenderer[]>();
	}
}
