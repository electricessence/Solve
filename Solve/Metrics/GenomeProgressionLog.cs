using Open.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Solve.Metrics
{
	public class GenomeProgressionLog
	{
		readonly ConcurrentHashSet<string> _dead = new ConcurrentHashSet<string>();
		readonly  ConcurrentDictionary<string, GenomeHistory> _log = new ConcurrentDictionary<string, GenomeHistory>();

		public GenomeHistory this[string hash] => _log.GetOrAdd(hash, key => new GenomeHistory(key));

		public bool AddDead(string hash) => _dead.Add(hash);
		public bool IsDead(string hash) => _dead.Contains(hash);
		public bool IsAlive(string hash) => !_dead.Contains(hash);

		public IEnumerable<GenomeHistory> Alive
		{
			get
			{
				foreach (var h in _log.Values)
				{
					if (_dead.Contains(h.Hash)) continue;
					yield return h;
				}
			}
		}
	}
}
