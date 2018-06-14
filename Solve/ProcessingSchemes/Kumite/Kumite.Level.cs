using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public sealed partial class KumiteProcessingScheme<TGenome>
	{
		internal sealed class Level
		{
			readonly Tower Tower;
			readonly ConcurrentQueue<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord)> WaitingToCompete
				= new ConcurrentQueue<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord)>();

			public readonly uint Index;
			Level _nextLevel;
			public Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			int _matches;
			public int MatchCount => _matches;

			public Level(
				uint level,
				Tower host)
			{
				Index = level;
				Tower = host;
			}

			IEnumerable<IGenomeFitness<TGenome, Fitness>> Resolution(
				(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) winner,
				(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) loser)
			{
				var winnerHash = winner.GenomeFitness.Genome.Hash;
				if (winnerHash == loser.GenomeFitness.Genome.Hash)
				{
					Debug.WriteLine($"Level {Index}: Contender fought itself: {winnerHash}");
					WaitingToCompete.Enqueue(winner);
				}
				else
				{

					var m = Interlocked.Increment(ref _matches);
					var wgf = winner.GenomeFitness;
					if (m == 1)
					{
						Debug.WriteLine("New Kumite Level: {0}", Index);
						Tower.Broadcast(wgf);
					}
					else
					{
						Tower.Environment.EnqueueForBreeding(wgf);
					}

					yield return wgf;

					ushort maxLoss = Tower.MaximumAllowedLosses;
					ushort losses = loser.LossRecord;

					if (losses >= maxLoss)
					{
						Tower.Rejection(loser.GenomeFitness);
					}
					// The smaller the level the more expedient losers are allowed to progress up the tournament.
					else if (losses <= Index)
					{
						loser.LossRecord++;
						WaitingToCompete.Enqueue(loser);
					}
					else
					{
						yield return loser.GenomeFitness;
					}
				}
			}

			void Resolve(
				(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) winner,
				(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) loser)
			{
				foreach (var next in Resolution(winner, loser))
					NextLevel.Post(next);
			}

			async Task ResolveAsync(
				(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) winner,
				(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) loser)
			{
				foreach (var next in Resolution(winner, loser))
					await NextLevel.PostAsync(next);
			}

			public void Post(IGenomeFitness<TGenome, Fitness> c)
			{
				// Process a test for this level.
				var fitness = Tower.Problem.ProcessTest(c.Genome, Index);
				c.Fitness.Merge(fitness);

				(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) challenger = (c, 0);

				if (WaitingToCompete.TryDequeue(out (IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) defender))
				{
					// A defender is available.
					var d = defender.GenomeFitness;
					if (d.IsSuperiorTo(c))
					{
						// Challenger lost.  // Defender moves on.
						Resolve(winner: defender, loser: challenger);
					}
					else
					{
						// Challenger won.  // Defender stays.
						Resolve(winner: challenger, loser: defender);
					}
				}
				else
				{
					// The challenger becomes a defender.
					WaitingToCompete.Enqueue(challenger);
				}
			}

			public async Task PostAsync(IGenomeFitness<TGenome, Fitness> c)
			{
				// Process a test for this level.
				var fitness = await Tower.Problem.ProcessTestAsync(c.Genome, Index);
				c.Fitness.Merge(fitness);

				(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) challenger = (c, 0);

				if (WaitingToCompete.TryDequeue(out (IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) defender))
				{
					// A defender is available.
					var d = defender.GenomeFitness;
					if (d.CompareTo(c) < 0) // NOTE: Ordering = first is better.  Ordering direction inverted.
					{
						// Challenger lost.  // Defender moves on.
						await ResolveAsync(winner: defender, loser: challenger);
					}
					else
					{
						// Challenger won.  // Defender stays.
						await ResolveAsync(winner: challenger, loser: defender);
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
}
