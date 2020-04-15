/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System;
using System.Collections.Generic;
using System.Linq;
#if DEBUG
using System.Threading;
#endif

namespace Solve
{
	public abstract class GenomeBase : FreezableBase, IGenome
	{
		protected GenomeBase()
		{
			_hash = new Lazy<string>(GetHash);
			_geneCount = new Lazy<int>(GetGeneCount);
		}

		protected abstract string GetHash();

		readonly Lazy<string> _hash;
		public string Hash => IsFrozen ? _hash.Value : GetHash();

		protected abstract object CloneInternal();

		public object Clone()
			=> CloneInternal();

		protected static IEnumerator<T> EmptyEnumerator<T>()
			=> Enumerable.Empty<T>().GetEnumerator();

		static readonly IEnumerator<IGenome> EmptyVariations
			= EmptyEnumerator<IGenome>();

		// ReSharper disable once VirtualMemberNeverOverridden.Global
		public virtual IEnumerator<IGenome> RemainingVariations
			=> EmptyVariations;

		protected abstract int GetGeneCount();

		readonly Lazy<int> _geneCount;
		public int GeneCount => IsFrozen ? _geneCount.Value : GetGeneCount();

#if DEBUG
		public string StackTrace { get; } = Environment.StackTrace;

		class LogEntry : IGenomeLogEntry
		{
			public LogEntry(string category, string message, string? data)
			{
				Category = category;
				Message = message;
				Data = data;
			}
			public DateTime TimeStamp { get; } = DateTime.Now;
			public string Category { get; }
			public string Message { get; }
			public string? Data { get; }
		}

		List<IGenomeLogEntry>? _log;
		List<IGenomeLogEntry> LogInternal => LazyInitializer.EnsureInitialized(ref _log);

		IReadOnlyList<IGenomeLogEntry>? _logWrapper;
		public IReadOnlyList<IGenomeLogEntry> Log => LazyInitializer.EnsureInitialized(ref _logWrapper, () => LogInternal.AsReadOnly());

		public void AddLogEntry(string category, string message, string? data = null)
			=> LogInternal.Add(new LogEntry(category, message, data));
#endif

	}

}
