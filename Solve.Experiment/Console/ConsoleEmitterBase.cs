using Open.Collections;
using Open.Threading;
using System;
using System.Diagnostics;
using System.Text;

namespace Solve.Experiment.Console
{
	public class ConsoleEmitterBase<TGenome>
		where TGenome : IGenome
	{
		public readonly AsyncFileWriter LogFile;
		public readonly uint SampleMinimum;

		public ConsoleEmitterBase(uint sampleMinimum = 50, string logFilePath = null)
		{
			LogFile = logFilePath == null ? null : new AsyncFileWriter(logFilePath, 1000);
			SampleMinimum = sampleMinimum;
		}

		(ReadOnlyMemory<double> Fitness, int SampleCount) LastScore = (ReadOnlyMemory<double>.Empty, 0);
		public string LastHash;
		public CursorRange LastTopGenomeUpdate;

		public void EmitTopGenomeStats((TGenome Genome, SampleFitnessCollectionBase Fitness, int SampleCount, int Rejections) announcement)
			=> EmitTopGenomeStats(announcement.Genome, announcement.Fitness, announcement.SampleCount);

		public bool EmitTopGenomeStats((TGenome Genome, SampleFitnessCollectionBase Fitness, int SampleCount) announcement)
			=> EmitTopGenomeStats(announcement.Genome, announcement.Fitness, announcement.SampleCount);

		public bool EmitTopGenomeStats(TGenome genome, SampleFitnessCollectionBase fitness, int sampleCount)
		{
			if (sampleCount < SampleMinimum) return false;

			var f = fitness.ProgressionAverages(sampleCount);

			var asReduced = genome is IReducibleGenome<TGenome> r ? r.AsReduced() : genome;
			return ThreadSafety.LockConditional(
				SynchronizedConsole.Sync,
				() => sampleCount >= SampleMinimum && (LastScore.Fitness.IsEmpty || (LastScore.Fitness.IsLessThan(f) && LastScore.SampleCount < sampleCount)),
				() => SynchronizedConsole.OverwriteIfSame(ref LastTopGenomeUpdate, () => LastHash == genome.Hash,
					cursor =>
					{
						if (asReduced.Equals(genome))
							System.Console.WriteLine("{0}:\t{1}", fitness.GetType(), genome.Hash);
						else
							System.Console.WriteLine("{0}:\t{1}\n=>\t{2}", fitness.GetType(), genome.Hash, asReduced.Hash);

						var fs = f.Span;
						EmitFitnessScoreWithLabels(genome, fitness.Labels, fs, sampleCount);
						System.Console.WriteLine();

						LastScore = (f, sampleCount);
						LastHash = genome.Hash;

						OnEmittingGenome(genome, fitness.GetType().ToString(), fs, sampleCount);

					}));
		}

		protected virtual void OnEmittingGenome(TGenome genome, string fitnessName, ReadOnlySpan<double> fitness, int sampleCount)
		{
			if (LogFile != null)
			{
				var sb = new StringBuilder();
				sb.Append(DateTime.Now).Append(',');
				sb.Append(fitnessName).Append(',');
				sb.Append(sampleCount).Append(',');
				var len = fitness.Length;
				for (var i = 0; i < len; i++)
					sb.Append(fitness[i]).Append(',');
				LogFile.AddLine(sb.ToString());
			}
		}

		public static void EmitFitnessScoreWithLabels(TGenome genome, SampleFitnessCollectionBase fitness, int sampleCount)
			=> EmitFitnessScoreWithLabels(genome, fitness.Labels, fitness.ProgressionAverages(sampleCount).Span, sampleCount);

		public static void EmitFitnessScoreWithLabels(
			TGenome genome,
			ReadOnlySpan<string> labels,
			ReadOnlySpan<double> fitness,
			int sampleCount)
		{
			Debug.Assert(labels.Length == fitness.Length);

			var len = labels.Length;
			var scoreStrings = new string[len];
			for (var i = 0; i < len; i++)
				scoreStrings[i] = String.Format(labels[i], fitness[i]);

			System.Console.WriteLine("  \t{0} ({1:n0} samples)", scoreStrings.JoinToString(", "), sampleCount);
		}

	}
}
