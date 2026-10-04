using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace UltimateStarGatling.BepInEx
{
	internal static class UltimateStarGatlingSupernovaPatches
	{
		[HarmonyPatch(typeof(SpriteRenderer), "set_color")]
		private static class SpriteRenderer_SetColor_Patch
		{
						[HarmonyPrefix]
			private static void Prefix(SpriteRenderer __instance, ref Color value)
			{
				if (UltimateStarGatlingSupernova._colorPatchDepth > 0)
				{
					return;
				}
				if (UltimateStarGatlingSupernova.ActiveTintTargetCount <= 0)
				{
					return;
				}
				if (__instance == null)
				{
					return;
				}
				if (!UltimateStarGatlingSupernova.IsTintTarget(__instance.Pointer))
				{
					return;
				}
				value = UltimateStarGatlingSupernova.ApplyFullTint(value);
			}
		}
	}
}
