using System.Diagnostics.CodeAnalysis;
/*!
* @author electricessence / https://github.com/electricessence/
* Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
*/

#if DEBUG
#endif

namespace Solve;

#pragma warning disable IDE0079 // Remove unnecessary suppression
[SuppressMessage("Naming", "CA1721:Property names should not match get methods")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
public abstract class GenomeBase : FreezableBase, IGenome
{
	protected GenomeBase()
	{
		_hash = new Lazy<string>(GetHash);
		_geneCount = new Lazy<int>(GetGeneCount);
	}

	protected abstract string GetHash();

	private readonly Lazy<string> _hash;

	public string Hash => IsFrozen ? _hash.Value : GetHash();

	protected abstract object CloneInternal();

	public object Clone()
		=> CloneInternal();

	protected static IEnumerator<T> EmptyEnumerator<T>()
		=> Enumerable.Empty<T>().GetEnumerator();

	private static readonly IEnumerator<IGenome> EmptyVariations
		= EmptyEnumerator<IGenome>();

	// ReSharper disable once VirtualMemberNeverOverridden.Global
	public virtual IEnumerator<IGenome> RemainingVariations
		=> EmptyVariations;

	protected abstract int GetGeneCount();

	private readonly Lazy<int> _geneCount;
	public int GeneCount => IsFrozen ? _geneCount.Value : GetGeneCount();

#if DEBUG
	public string StackTrace { get; } = Environment.StackTrace;

	private sealed record LogEntry : IGenomeLogEntry
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

	private List<IGenomeLogEntry>? _log;

	private List<IGenomeLogEntry> LogInternal => LazyInitializer.EnsureInitialized(ref _log);

	private IReadOnlyList<IGenomeLogEntry>? _logWrapper;
	public IReadOnlyList<IGenomeLogEntry> Log => LazyInitializer.EnsureInitialized(ref _logWrapper, LogInternal.AsReadOnly);

	public void AddLogEntry(string category, string message, string? data = null)
		=> LogInternal.Add(new LogEntry(category, message, data));
#endif

}
