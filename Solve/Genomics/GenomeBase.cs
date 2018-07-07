/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Solve
{

	public abstract class GenomeBase : FreezableBase, IGenome
	{
		protected GenomeBase() : base() { }

		public abstract string Hash { get; }

		public bool Equivalent(IGenome other)
			=> this == other || Hash == other.Hash;

		protected abstract object CloneInternal();

		public object Clone()
			=> CloneInternal();

		protected static IEnumerator<T> EmptyEnumerator<T>()
			=> Enumerable.Empty<T>().GetEnumerator();

		static readonly IEnumerator<IGenome> EmptyVariations
			= EmptyEnumerator<IGenome>();

		public virtual IEnumerator<IGenome> RemainingVariations
			=> EmptyVariations;

		public abstract int Length { get; }

#if DEBUG
		public string StackTrace { get; } = System.Environment.StackTrace;

		class LogEntry : IGenomeLogEntry
		{
			public LogEntry(string category, string message, string data)
			{
				Category = category;
				Message = message;
				Data = data;
			}
			public DateTime TimeStamp { get; } = DateTime.Now;
			public string Category { get; }
			public string Message { get; }
			public string Data { get; }
		}

		List<IGenomeLogEntry> _log;
		List<IGenomeLogEntry> LogInternal => LazyInitializer.EnsureInitialized(ref _log);

		IReadOnlyList<IGenomeLogEntry> _logWrapper;
		public IReadOnlyList<IGenomeLogEntry> Log => LazyInitializer.EnsureInitialized(ref _logWrapper, () => LogInternal.AsReadOnly());

		public void AddLogEntry(string category, string message, string data = null)
			=> LogInternal.Add(new LogEntry(category, message, data));
#endif

	}

}
