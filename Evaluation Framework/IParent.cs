/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/evaluation-framework/blob/master/LICENSE.txt
 */

using System.Collections.Generic;
using System.Linq;

namespace EvaluationFramework
{
	public interface IParent<out TDescendant>
	{
		IReadOnlyList<TDescendant> Children { get; }

		IReadOnlyList<TDescendant> Descendants { get; }
	}


	public static class Hierarchy
	{
		public static IEnumerable<TDescendant> GetDescendants<TDescendant>(this IParent<TDescendant> parent)
		{
			return parent.Children.Concat(
				parent.Children.OfType<IParent<TDescendant>>().SelectMany(c => c.Descendants));			
		}


		public static IParent<TChild> FindParentOf<TParent,TChild>(TParent parent, TChild child)
			where TParent : class
			where TChild : class
		{
			var p = parent as IParent<TChild>;
			if (p != null)
			{
				foreach (var c in p.Children)
				{
					if (child == c) return p;
					var np = FindParentOf(c, child);
					if (np != null) return np;
				}
			}
			return null;
		}

		public static IParent<T> FindParent<T>(this IParent<T> parent, T child)
			where T : class
		{
			return FindParentOf(parent, child);
		}

	}
}