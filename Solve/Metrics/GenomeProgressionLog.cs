using Open.Collections;
using System.Collections.Concurrent;

namespace Solve.Metrics;

public class GenomeProgressionLog
{
	private readonly ConcurrentHashSet<string> _dead = [];
	private readonly ConcurrentDictionary<string, GenomeHistory> _log = new();

	public GenomeHistory this[string hash] => _log.GetOrAdd(hash, key => new GenomeHistory(key));

	public bool AddDead(string hash) => _dead.Add(hash);
	public bool IsDead(string hash) => _dead.Contains(hash);
	public bool IsAlive(string hash) => !_dead.Contains(hash);

	public IEnumerable<GenomeHistory> Alive
	{
		get
		{
			foreach (GenomeHistory h in _log.Values)
			{
				if (_dead.Contains(h.Hash)) continue;
				yield return h;
			}
		}
	}
}
