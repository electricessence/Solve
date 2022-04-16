using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Solve;

/// <summary>
/// Provides a means for selecting and rejecting entries by chosing a mid point in thier population assuming that the selected ones are first and the rejected ones are last.
/// </summary>
public struct Selection<T>
{
	public readonly ImmutableArray<T> All;
	public readonly ImmutableArray<T> Selected;
	public readonly ImmutableArray<T> Rejected;


	// ReSharper disable once UnusedParameter.Local
	public Selection(IEnumerable<T> contenders, double selectionPoint = 0.5) : this(contenders as IReadOnlyList<T> ?? contenders.ToArray(), selectionPoint)
	{
	}

	public Selection(IReadOnlyList<T> contenders, double selectionPoint = 0.5)
	{
		if (selectionPoint <= 0 || selectionPoint >= 1)
			throw new ArgumentOutOfRangeException(nameof(selectionPoint), selectionPoint, "Must be greater than zero and less than one.");
		var len = contenders.Count;
		if (len < 2) throw new InvalidOperationException("Selection requires at least to contenders.");
		Contract.EndContractBlock();

		// Find a valid middle point that includes at least one on either side of the middle point. (At least one should be selected, and one should be rejected.)
		var middleIndex = (int)Math.Floor(selectionPoint * len);
		if (middleIndex == 0) middleIndex = 1;
		else if (middleIndex == len) middleIndex--;

		var all = ImmutableArray.CreateBuilder<T>(len);
		var selected = ImmutableArray.CreateBuilder<T>(middleIndex);
		var rejected = ImmutableArray.CreateBuilder<T>(len - middleIndex);
		for (var i = 0; i < len; i++)
		{
			var g = contenders[i];
			all[i] = g;
			if (i < middleIndex)
				selected[i] = g;
			else
				rejected[i - middleIndex] = g;
		}

		All = all.MoveToImmutable();
		Selected = selected.MoveToImmutable();
		Rejected = rejected.MoveToImmutable();
	}


}
