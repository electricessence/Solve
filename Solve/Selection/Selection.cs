using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Solve
{
	/// <summary>
	/// Provides a means for selecting and rejecting entries by chosing a mid point in thier population assuming that the selected ones are first and the rejected ones are last.
	/// </summary>
	public struct Selection<T>
	{
		public readonly T[] All;
		public readonly T[] Selected;
		public readonly T[] Rejected;

		public Selection(IEnumerable<T> contenders, double selectionPoint = 0.5) : this((contenders as T[] ?? contenders?.ToArray()).AsSpan())
		{
		}

		public Selection(ReadOnlySpan<T> contenders, double selectionPoint = 0.5)
		{
			if (selectionPoint <= 0 || selectionPoint >= 1)
				throw new ArgumentOutOfRangeException(nameof(selectionPoint), selectionPoint, "Must be greater than zero and less than one.");
			int len = contenders.Length;
			if (len < 2) throw new InvalidOperationException("Selection requires at least to contenders.");
			Contract.EndContractBlock();

			// Find a valid middle point that includes at least one on either side of the middle point. (At least one should be selected, and one should be rejected.)
			int middleIndex = (int)Math.Floor(selectionPoint * len);
			if (middleIndex == 0) middleIndex = 1;
			else if (middleIndex == len) middleIndex--;

			var all = new T[len];
			var selected = new T[middleIndex];
			var rejected = new T[len - middleIndex];
			for (var i = 0; i < len; i++)
			{
				var g = contenders[i];
				all[i] = g;
				if (i < middleIndex)
					selected[i] = g;
				else
					rejected[i - middleIndex] = g;
			}

			All = all;
			Selected = selected;
			Rejected = rejected;
		}


	}
}
