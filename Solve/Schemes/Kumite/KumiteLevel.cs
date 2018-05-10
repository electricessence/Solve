using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve.Schemes.Kumite
{
	public sealed class KumiteLevel<TGenome>
		where TGenome : class, IGenome
	{
		readonly KumiteTournament<TGenome> Host;
		readonly ConcurrentQueue<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord)> WaitingToCompete
			= new ConcurrentQueue<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord)>();

		public readonly uint Level;
		KumiteLevel<TGenome> _nextLevel;
		public KumiteLevel<TGenome> NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
			() => new KumiteLevel<TGenome>(Level + 1, Host));

		int _matches;
		public int MatchCount => _matches;

		public KumiteLevel(
			uint level,
			KumiteTournament<TGenome> host)
		{
			Level = level;
			Host = host;
		}

		async Task Resolve(
			IGenomeFitness<TGenome, Fitness> winner,
			(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) loser)
		{
			var m = Interlocked.Increment(ref _matches);

			ushort maxLoss = Host.MaximumAllowedLosses;
			if (loser.LossRecord == maxLoss)
			{
				Host.RejectProcessor.Post(loser.GenomeFitness);
			}
			else
			{
				loser.LossRecord++;
				WaitingToCompete.Enqueue(loser);
			}

			if (m == 1) Host.Announce(winner);
			await NextLevel.Post(winner);
		}

		public async Task Post(IGenomeFitness<TGenome, Fitness> c)
		{
			// Process a test for this level.
			var fitness = await Host.Problem.ProcessTest(c.Genome, Level);
			c.Fitness.Merge(fitness);

			(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) challenger = (c, 0);

			if (WaitingToCompete.TryDequeue(out (IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) defender))
			{
				// A defender is available.
				var d = defender.GenomeFitness;
				Debug.Assert(c.Genome != d.Genome, "Challenger must be different than the defender.");
				if (d.CompareTo(c) > 0)
				{
					// Challenger lost.  // Defender moves on.
					await Resolve(winner: d, loser: challenger);
				}
				else
				{
					// Challenger won.  // Defender stays and recieves a mark on its record.
					await Resolve(winner: c, loser: defender);
				}
			}
			else
			{
				// The challenger becomes a defender.
				WaitingToCompete.Enqueue(challenger);
			}
		}
	}
}
