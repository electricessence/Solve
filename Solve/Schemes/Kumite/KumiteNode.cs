using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve.Schemes.Kumite
{
	public class KumiteNode<TGenome>
		where TGenome : class, IGenome
	{
		static readonly GenomeFitness<TGenome> DEFAULT = default(GenomeFitness<TGenome>);

		// -2 = new. -1 = current was rejected.  Contenders will only be rejected if a max losses is set.
		int _lossCount = -2;
		int _maximumLosses = 0;
		GenomeFitness<TGenome> _current = DEFAULT;

		readonly IProblem<TGenome> _problem;
		readonly BroadcastBlock<GenomeFitness<TGenome>> _announcer;
		readonly ITargetBlock<GenomeFitness<TGenome>> _rejects;

		public readonly uint Level;
		KumiteNode<TGenome> _nextLevel;
		public KumiteNode<TGenome> NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
			() => new KumiteNode<TGenome>(Level + 1, _problem, _announcer));

		public KumiteNode(
			uint level,
			IProblem<TGenome> problem,
			BroadcastBlock<GenomeFitness<TGenome>> announcer,
			int maxLosses = 0,
			ITargetBlock<GenomeFitness<TGenome>> rejects = null)
		{
			Level = level;
			_problem = problem;
			_announcer = announcer;
			_maximumLosses = maxLosses;
			_rejects = rejects;
		}

		public void Post(GenomeFitness<TGenome> challenger)
		{
			GenomeFitness<TGenome> winner = challenger;
			int losses;

			lock (this) // Let them fight! :P
			{
				losses = _lossCount;
				if (losses < 0)
				{
					_current = challenger;
					_lossCount = 0;
				}
				else
				{
					var c = _current;
					Debug.WriteLineIf(challenger.Genome == c.Genome, "Warning: challenger was the same as the current remaining one.");
					if (c.CompareTo(challenger) > 0)
					{
						// Challenger lost.
						_lossCount = 1;
						_current = challenger;
						winner = c;
					}
					else
					{
						// Challenger won.
						if (_maximumLosses > 0 && losses == _maximumLosses)
						{
							_lossCount = -1;
							_current = DEFAULT;
							if (_rejects != null)
								_rejects.Post(c);
						}
						else
						{
							_lossCount++;
						}
					}
				}
			}


			if (losses >= 0) // Was there a fight?
			{
				NextLevel.Post(winner);
			}
			else if (losses == -2) // New champion?
			{
				_announcer.Post(challenger);
			}

		}

	}
}
