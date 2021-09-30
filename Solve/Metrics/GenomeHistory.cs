using System;
using System.Collections;
using System.Collections.Generic;

namespace Solve.Metrics
{
	public class GenomeHistory : IEnumerable<GenomeEvent>
	{
		readonly SortedDictionary<long, GenomeEvent> _events = new();

		public GenomeHistory(string hash)
		{
			Hash = hash ?? throw new ArgumentNullException(nameof(hash));
			if (string.IsNullOrWhiteSpace(hash)) throw new ArgumentException("Cannot be empty or whitespace.", nameof(hash));
		}

		public string Hash { get; }

		public void Add(GenomeEvent e) => _events.Add(e.Id, e);

		public void Add(GenomeEvent.EventType e, string? data = null) => Add(new GenomeEvent(e, data));

		public GenomeEvent Get(long id) => _events[id];

		public IEnumerator<GenomeEvent> GetEnumerator() => _events.Values.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
