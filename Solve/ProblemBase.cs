using Open.Collections;
using Open.Collections.Synchronized;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Solve
{
	public abstract class ProblemBase<TGenome> : IProblem<TGenome>
	where TGenome : class, IGenome
	{

		public RankedPool<TGenome> ChampionPool { get; }

		protected readonly ConcurrentDictionary<string, Lazy<GenomeFitness<TGenome, Fitness>>>
			Fitnesses = new ConcurrentDictionary<string, Lazy<GenomeFitness<TGenome, Fitness>>>();

		protected readonly LockSynchronizedHashSet<string>
			Rejects = new LockSynchronizedHashSet<string>();


		static int ProblemCount = 0;
		public int ID { get; } = Interlocked.Increment(ref ProblemCount);

		long _testCount = 0;
		public long TestCount => _testCount;


		protected ProblemBase(ushort championPoolSize = 0)
		{
			if (championPoolSize != 0)
				ChampionPool = new RankedPool<TGenome>(championPoolSize);
		}


		// Override this if there is a common key for multiple genomes (aka they are equivalient).
		protected virtual TGenome GetFitnessForKeyTransform(TGenome genome)
		{
			return genome;
		}

		public bool TryGetFitnessFor(TGenome genome, out GenomeFitness<TGenome, Fitness> fitness)
		{
			genome = GetFitnessForKeyTransform(genome);
			var key = genome.Hash;

			if (Fitnesses.TryGetValue(key, out Lazy<GenomeFitness<TGenome, Fitness>> value))
			{
				if (!Rejects.Contains(key))
				{
					fitness = value.Value;
					return true;
				}
			}

			fitness = default;
			return false;
		}

		public GenomeFitness<TGenome, Fitness>? GetFitnessFor(TGenome genome, bool ensureSourceGenome = false)
		{
			TryGetFitnessFor(genome, out GenomeFitness<TGenome, Fitness> gf);
			if (ensureSourceGenome && gf.Genome != genome) // Possible that a 'version' is stored.
				gf = new GenomeFitness<TGenome, Fitness>(genome, gf.Fitness);
			return gf;
		}

		public IEnumerable<IGenomeFitness<TGenome, Fitness>> GetFitnessFor(IEnumerable<TGenome> genomes, bool ensureSourceGenomes = false)
		{
			foreach (var genome in genomes)
			{
				if (TryGetFitnessFor(genome, out GenomeFitness<TGenome, Fitness> gf))
				{
					if (ensureSourceGenomes && gf.Genome != genome) // Possible that a 'version' is stored.
						gf = new GenomeFitness<TGenome, Fitness>(genome, gf.Fitness);
					yield return gf;
				}
			}
		}

		public GenomeFitness<TGenome, Fitness> GetOrCreateFitnessFor(TGenome genome)
		{
			if (!genome.IsReadOnly)
				throw new InvalidOperationException("Cannot recall fitness for an unfrozen genome.");
			genome = GetFitnessForKeyTransform(genome);
			var key = genome.Hash;
			GenomeFitness<TGenome, Fitness> result = default;
			Rejects.IfNotContains(key, hs =>
			{
				result = Fitnesses
					.GetOrAdd(key, k =>
						Lazy.Create(() => GenomeFitness.New(genome, new Fitness()))).Value;
			});
			return result;
		}

		// Presents a means for removing and blocking future storage of (for memory control).
		public void Reject(string hash)
		{
			Rejects.Add(hash);
			Fitnesses.TryRemove(hash);
		}

		public void Reject(IEnumerable<string> hashes)
		{
			foreach (var hash in hashes)
				Reject(hash);
		}

		public bool WasRejected(string hash)
		{
			return Rejects.Contains(hash);
		}

		protected abstract void ProcessTest(TGenome g, Fitness fitness, long sampleId);

		public IFitness ProcessTest(TGenome g, long sampleId = 0, bool mergeWithGlobal = false)
		{
			var f = new Fitness();
			if (sampleId == 0)
			{
				var global = GetOrCreateFitnessFor(g).Fitness;
				using (global.Lock.Lock())
				{
					ProcessTest(g, f, -global.SampleCount);
				}
				if (mergeWithGlobal) global.Merge(f);
			}
			else
			{
				ProcessTest(g, f, sampleId);
				if (mergeWithGlobal) AddToGlobalFitness(g, f);
			}

			Interlocked.Increment(ref _testCount);
			return f;
		}

		protected virtual Task ProcessTestAsync(TGenome g, Fitness fitness, long sampleId)
		{
			ProcessTest(g, fitness, sampleId);
			return Task.CompletedTask;
		}

		public async Task<IFitness> ProcessTestAsync(TGenome g, long sampleId = 0, bool mergeWithGlobal = false)
		{
			var f = new Fitness();
			Task t;
			if (sampleId == 0)
			{
				var global = GetOrCreateFitnessFor(g).Fitness;
				using (await global.Lock.LockAsync())
				{
					t = ProcessTestAsync(g, f, -global.SampleCount);
					if (!t.IsCompleted) await t;
					if (mergeWithGlobal) global.Merge(f);
				}
			}
			else
			{
				t = ProcessTestAsync(g, f, sampleId);
				if (!t.IsCompleted) await t;
				if (mergeWithGlobal) AddToGlobalFitness(g, f);
			}

			Interlocked.Increment(ref _testCount);
			return f;
		}


		GenomeTestDelegate<TGenome> _testProcessor;
		public GenomeTestDelegate<TGenome> TestProcessor
		{
			get
			{
				return LazyInitializer.EnsureInitialized(ref _testProcessor, () => ProcessTestAsync);
			}
		}

		public abstract IReadOnlyList<string> FitnessLabels { get; }

		public void AddToGlobalFitness<T>(IEnumerable<T> results)
			where T : IGenomeFitness<TGenome>
		{
			foreach (var r in results)
				AddToGlobalFitness(r);
		}

		public IFitness AddToGlobalFitness(IGenomeFitness<TGenome> result)
		{
			return AddToGlobalFitness(result.Genome, result.Fitness);
		}

		public IFitness AddToGlobalFitness(TGenome genome, IFitness fitness)
		{
			IFitness result = fitness;
			Rejects.IfNotContains(genome.Hash, hs =>
			{
				var global = GetOrCreateFitnessFor(genome).Fitness;
				if (global == fitness)
					throw new InvalidOperationException("Adding fitness on to itself.");
				global.Merge(fitness);
				result = global.SnapShot();
			});
			return result;
		}

		public int GetSampleCountFor(TGenome genome)
		{
			return TryGetFitnessFor(genome, out GenomeFitness<TGenome, Fitness> fitness)
				? fitness.Fitness.SampleCount
				: 0;
		}


	}
}
