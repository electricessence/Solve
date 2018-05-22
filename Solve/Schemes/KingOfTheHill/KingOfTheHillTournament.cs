using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve.Schemes
{
	public sealed class KingOfTheHillTournament<TGenome> : ProcessingSchemeBase<TGenome>
		where TGenome : class, IGenome
	{
		// Should generate a genome and queue it to all problems.
		readonly Func<Task> Generator;
		internal readonly IProblem<TGenome> Problem;
		readonly ushort MaximumAllowedLosses;
		readonly ConcurrentDictionary<uint, ConcurrentQueue<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord)>> WaitingToCompete
			= new ConcurrentDictionary<uint, ConcurrentQueue<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord)>>();
		ITargetBlock<IGenomeFitness<TGenome, Fitness>> LoserPool;

		public KingOfTheHillTournament(Func<Task> generator, IProblem<TGenome> problem, ushort maximumLoss = ushort.MaxValue, ITargetBlock<IGenomeFitness<TGenome, Fitness>> loserPool = null) : base()
		{
			Generator = generator ?? throw new ArgumentNullException(nameof(generator));
			Problem = problem ?? throw new ArgumentNullException(nameof(problem));
			if (maximumLoss == 0) throw new ArgumentOutOfRangeException(nameof(maximumLoss), maximumLoss, "Must be greater than zero.");
			MaximumAllowedLosses = maximumLoss;
			LoserPool = loserPool ?? DataflowBlock.NullTarget<IGenomeFitness<TGenome, Fitness>>();
		}

		ConcurrentQueue<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord)> GetWaitingToCompeteLevel(uint level)
			=> WaitingToCompete
				.GetOrAdd(level, k => new ConcurrentQueue<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord)>());

		async ValueTask<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord)> NextContender()
		{
			(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) contender;
			while (!GetWaitingToCompeteLevel(0).TryDequeue(out contender))
				await Generator();
			return contender;
		}

		async ValueTask<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord)> Next(uint level, IGenomeFitness<TGenome, Fitness> previous = null)
		{
			(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) contender;

			if (previous != null)
			{
				contender = (previous, 0);
			}
			else if (level == 0)
			{
				contender = await NextContender();
				// Do we have a new contender, or a previous loser?
				if (contender.GenomeFitness.Fitness.HasSamples)
					return contender;
			}
			else
			{
				if (GetWaitingToCompeteLevel(level).TryDequeue(out contender))
				{
					// A previous loser that has already been tested at this level.
					return contender; // Synchronous return. (Hence using ValueTask is preferred).
				}
				else
				{
					// A new champion from a lower level.
					contender = (await NextChampion(level - 1), 0);
				}
			}

			// Process the test for this level.
			var g = contender.GenomeFitness;
			var fitness = await Problem.ProcessTest(g.Genome, level);
			g.Fitness.Merge(fitness);
			return contender;
		}

		public async Task<IGenomeFitness<TGenome, Fitness>> NextChampion(uint level, IGenomeFitness<TGenome, Fitness> firstContender = null)
		{
			// Queue up both.
			var a = Next(level, firstContender);
			var b = Next(level);

			// Await for their results.
			var aResult = await a;
			var bResult = await b;

			var aGF = aResult.GenomeFitness;
			var bGF = bResult.GenomeFitness;

#if DEBUG
			if (aGF.Fitness.SampleCount != bGF.Fitness.SampleCount)
				Debugger.Break(); // Sample Count Mismatch!!!
#endif
			IGenomeFitness<TGenome, Fitness> winner;
			(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LossRecord) loser;

			if (aGF.CompareTo(bGF) < 0) // NOTE: Ordering = first is better.  Ordering direction inverted.
			{
				winner = aGF;
				loser = bResult;
			}
			else
			{
				winner = bGF;
				loser = aResult;
			}

			loser.LossRecord++;
			if (loser.LossRecord > MaximumAllowedLosses)
			{
				LoserPool.Post(loser.GenomeFitness);
			}
			else
			{
				GetWaitingToCompeteLevel(level).Enqueue(loser);
			}

			Broadcast(winner);
			return winner;
		}

		public void QueueNewContender(TGenome contender)
		{
			GetWaitingToCompeteLevel(0)
				.Enqueue((new GenomeFitness<TGenome, Fitness>(contender, new Fitness()), 0));
		}
	}
}
