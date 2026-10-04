using System;
using UnityEngine;

#nullable disable

namespace UltimateStarGatling.BepInEx
{
	/// <summary>充能条</summary>
	internal static class ParticleLayerBinder
	{
		/// <summary>目标层名。</summary>
		internal const string ParticleLayerName = "particle";

		internal enum Result
		{
			/// <summary>Animator 未就绪。</summary>
			NotReady,

			/// <summary>没有该层。</summary>
			Missing,

			/// <summary>确认权重为 1。</summary>
			Ready,
		}

		/// <summary>找到 `particle` 层并确保权重为 1；异常按 NotReady 处理。</summary>
		internal static Result Ensure(Animator anim)
		{
			if (anim == null)
			{
				return Result.NotReady;
			}
			try
			{
				int count = anim.layerCount;
				for (int i = 0; i < count; i++)
				{
					if (!string.Equals(anim.GetLayerName(i), ParticleLayerName, StringComparison.Ordinal))
					{
						continue;
					}
					if (anim.GetLayerWeight(i) < 1f)
					{
						anim.SetLayerWeight(i, 1f);
					}
					return Result.Ready;
				}
				return Result.Missing;
			}
			catch
			{
				return Result.NotReady;
			}
		}
	}
}
