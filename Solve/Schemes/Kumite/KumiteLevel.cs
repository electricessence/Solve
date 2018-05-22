﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve.Schemes
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
			(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) winner,
			(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) loser)
		{
			var winnerHash = winner.GenomeFitness.Genome.Hash;
			if (winnerHash == loser.GenomeFitness.Genome.Hash)
			{
				Debug.WriteLine($"Level {Level}: Contender fought itself: {winnerHash}");
				WaitingToCompete.Enqueue(winner);
			}
			else
			{
				var m = Interlocked.Increment(ref _matches);

				ushort maxLoss = Host.MaximumAllowedLosses;
				if (loser.LossRecord == maxLoss)
				{
					Host.LoserPool.Post(loser.GenomeFitness);
				}
				else
				{
					loser.LossRecord++;
					WaitingToCompete.Enqueue(loser);
				}

				if (m == 1)
				{
					Debug.WriteLine("New Kumite Level: {0}", Level);
					Host.Broadcast(winner.GenomeFitness);
				}
				await NextLevel.Post(winner.GenomeFitness);
			}


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
				if (d.CompareTo(c) < 0) // NOTE: Ordering = first is better.  Ordering direction inverted.
				{
					// Challenger lost.  // Defender moves on.
					await Resolve(winner: defender, loser: challenger);
				}
				else
				{
					// Challenger won.  // Defender stays.
					await Resolve(winner: challenger, loser: defender);
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
