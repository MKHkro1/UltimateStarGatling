using System;
using MengxiLib.BepInEx;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>开场面板立绘</summary>
	internal static class StarIntroArtwork
	{
		/// <summary>8003 所在包里的预览图名</summary>
		internal const string PreviewName8003 = "预览";

		/// <summary>5166 所在包里的预览图名</summary>
		internal const string PreviewName5166 = "mmexport1779515297723_edit_225530974692402";

		private static AssetBundle _bundle8003;
		private static AssetBundle _bundle5166;
		private static Sprite _preview8003;
		private static Sprite _preview5166;
		private static bool _probed8003;
		private static bool _probed5166;

		internal static void AttachBundles(AssetBundle holyStar, AssetBundle star)
		{
			if (holyStar != null)
			{
				_bundle8003 = holyStar;
			}
			if (star != null)
			{
				_bundle5166 = star;
			}
		}

		internal static Sprite Preview8003()
		{
			if (!_probed8003)
			{
				_probed8003 = true;
				_preview8003 = Load(_bundle8003, PreviewName8003, "8003 终焉·撰星极帝机枪射手");
			}
			return _preview8003;
		}

		internal static Sprite Preview5166()
		{
			if (!_probed5166)
			{
				_probed5166 = true;
				_preview5166 = Load(_bundle5166, PreviewName5166, "5166 终极流星大帝机枪射手");
			}
			return _preview5166;
		}

		private static Sprite Load(AssetBundle bundle, string name, string tag)
		{
			if (bundle == null)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 开场面板立绘：资源包为空，跳过 " + tag + " 的立绘");
				return null;
			}
			try
			{
				Sprite sprite = bundle.GetAsset<Sprite>(name);
				if (sprite == null)
				{
					HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 开场面板立绘：包里没有名为「" + name + "」的 Sprite（" + tag + "）");
					return null;
				}
				HolyStarGatling.LogVerbose(HolyStarGatling.LogTag + " 开场面板立绘已就绪：" + tag +
					" ← 「" + name + "」（" + Mathf.RoundToInt(sprite.rect.width) + "×" + Mathf.RoundToInt(sprite.rect.height) + "）");
				return sprite;
			}
			catch (Exception ex)
			{
				HolyStarGatling.LogWarn(HolyStarGatling.LogTag + " 开场面板立绘：「" + name + "」取用失败（" + tag + "）：" + ex.Message);
				return null;
			}
		}
	}
}
