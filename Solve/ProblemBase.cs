using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Solve
{
	public abstract class ProblemBase<TGenome> : IProblem<TGenome>
	where TGenome : class, IGenome
	{

		public RankedPool<TGenome> ChampionPool { get; }

		//protected readonly ConcurrentDictionary<string, Lazy<GenomeFitness<TGenome, Fitness>>>
		//	Fitnesses = new ConcurrentDictionary<string, Lazy<GenomeFitness<TGenome, Fitness>>>();

		//protected readonly LockSynchronizedHashSet<string>
		//	Rejects = new LockSynchronizedHashSet<string>();


		static int ProblemCount = 0;
		public int ID { get; } = Interlocked.Increment(ref ProblemCount);

		long _testCount = 0;
		public long TestCount => _testCount;


		protected ProblemBase(ushort championPoolSize = 0)
		{
			if (championPoolSize != 0)
				ChampionPool = new RankedPool<TGenome>(championPoolSize);
		}


		//// Override this if there is a common key for multiple genomes (aka they are equivalient).
		//protected virtual TGenome GetFitnessForKeyTransform(TGenome genome)
		//{
		//	return genome;
		//}

		//public bool TryGetFitnessFor(TGenome genome, out GenomeFitness<TGenome, Fitness> fitness)
		//{
		//	genome = GetFitnessForKeyTransform(genome);
		//	var key = genome.Hash;

		//	if (Fitnesses.TryGetValue(key, out Lazy<GenomeFitness<TGenome, Fitness>> value))
		//	{
		//		if (!Rejects.Contains(key))
		//		{
		//			fitness = value.Value;
		//			return true;
		//		}
		//	}

		//	fitness = default;
		//	return false;
		//}

		//public GenomeFitness<TGenome, Fitness>? GetFitnessFor(TGenome genome, bool ensureSourceGenome = false)
		//{
		//	TryGetFitnessFor(genome, out GenomeFitness<TGenome, Fitness> gf);
		//	if (ensureSourceGenome && gf.Genome != genome) // Possible that a 'version' is stored.
		//		gf = new GenomeFitness<TGenome, Fitness>(genome, gf.Fitness);
		//	return gf;
		//}

		//public IEnumerable<IGenomeFitness<TGenome, Fitness>> GetFitnessFor(IEnumerable<TGenome> genomes, bool ensureSourceGenomes = false)
		//{
		//	foreach (var genome in genomes)
		//	{
		//		if (TryGetFitnessFor(genome, out GenomeFitness<TGenome, Fitness> gf))
		//		{
		//			if (ensureSourceGenomes && gf.Genome != genome) // Possible that a 'version' is stored.
		//				gf = new GenomeFitness<TGenome, Fitness>(genome, gf.Fitness);
		//			yield return gf;
		//		}
		//	}
		//}

		//public GenomeFitness<TGenome, Fitness> GetOrCreateFitnessFor(TGenome genome)
		//{
		//	if (!genome.IsReadOnly)
		//		throw new InvalidOperationException("Cannot recall fitness for an unfrozen genome.");
		//	genome = GetFitnessForKeyTransform(genome);
		//	var key = genome.Hash;
		//	GenomeFitness<TGenome, Fitness> result = default;
		//	Rejects.IfNotContains(key, hs =>
		//	{
		//		result = Fitnesses
		//			.GetOrAdd(key, k =>
		//				Lazy.Create(() => GenomeFitness.New(genome, new Fitness()))).Value;
		//	});
		//	return result;
		//}

		//// Presents a means for removing and blocking future storage of (for memory control).
		//public void Reject(string hash)
		//{
		//	Rejects.Add(hash);
		//	Fitnesses.TryRemove(hash);
		//}

		//public void Reject(IEnumerable<string> hashes)
		//{
		//	foreach (var hash in hashes)
		//		Reject(hash);
		//}

		//public bool WasRejected(string hash)
		//{
		//	return Rejects.Contains(hash);
		//}

		protected abstract double[] ProcessTestInternal(TGenome g, long sampleId);

		public double[] ProcessTest(TGenome g, long sampleId = 0)
		{
			try
			{
				return ProcessTestInternal(g, sampleId);
			}
			finally
			{
				Interlocked.Increment(ref _testCount);
			}
		}

		protected virtual Task<double[]> ProcessTestAsyncInternal(TGenome g, long sampleId)
			=> Task.FromResult(ProcessTestInternal(g, sampleId));

		public async Task<double[]> ProcessTestAsync(TGenome g, long sampleId = 0)
		{
			try
			{
				return await ProcessTestAsyncInternal(g, sampleId);
			}
			finally
			{
				Interlocked.Increment(ref _testCount);
			}
		}


		//GenomeTestDelegate<TGenome> _testProcessor;
		//public GenomeTestDelegate<TGenome> TestProcessor
		//{
		//	get
		//	{
		//		return LazyInitializer.EnsureInitialized(ref _testProcessor, () => ProcessTestAsync);
		//	}
		//}

		public abstract IReadOnlyList<string> FitnessLabels { get; }

		//public void AddToGlobalFitness<T>(IEnumerable<T> results)
		//	where T : IGenomeFitness<TGenome>
		//{
		//	foreach (var r in results)
		//		AddToGlobalFitness(r);
		//}

		//public IFitness AddToGlobalFitness(IGenomeFitness<TGenome> result)
		//{
		//	return AddToGlobalFitness(result.Genome, result.Fitness);
		//}

		//public IFitness AddToGlobalFitness(TGenome genome, IFitness fitness)
		//{
		//	IFitness result = fitness;
		//	Rejects.IfNotContains(genome.Hash, hs =>
		//	{
		//		var global = GetOrCreateFitnessFor(genome).Fitness;
		//		if (global == fitness)
		//			throw new InvalidOperationException("Adding fitness on to itself.");
		//		global.Merge(fitness);
		//		result = global.SnapShot();
		//	});
		//	return result;
		//}

		//public int GetSampleCountFor(TGenome genome)
		//{
		//	return TryGetFitnessFor(genome, out GenomeFitness<TGenome, Fitness> fitness)
		//		? fitness.Fitness.SampleCount
		//		: 0;
		//}


	}
}
