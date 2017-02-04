/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/evaluation-framework/blob/master/LICENSE.txt
 */

using System.Collections.Generic;
using System.Linq;

namespace EvaluationFramework
{
	public interface IParent<TDescendant>
	{
		IReadOnlyList<TDescendant> Children { get; }

		IReadOnlyList<TDescendant> Descendants { get; }
	}

	public static class ParentExtensions
	{
		public static IEnumerable<TDescendant> GetDescendants<TDescendant>(this IParent<TDescendant> parent)
		{
			return parent.Children.Concat(
				parent.Children.OfType<IParent<TDescendant>>().SelectMany(c => c.Descendants));			
		}
	}
}