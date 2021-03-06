﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Dataflow
{
	// ReSharper disable once PossibleInfiniteInheritance
	public partial class DataflowScheme<TGenome> : TowerProcessingSchemeBase<TGenome>
		where TGenome : class, IGenome
	{
		public DataflowScheme(
			IGenomeFactory<TGenome> genomeFactory,
			in (ushort First, ushort Minimum, ushort Step) poolSize,
			ushort maxLevels = ushort.MaxValue,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION)
			: base(genomeFactory, in poolSize, maxLevels, maxLevelLosses, maxLossesBeforeElimination)
		{

		}

		public DataflowScheme(
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize,
			ushort maxLevels = ushort.MaxValue,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION)
			: this(genomeFactory, (poolSize, poolSize, 2), maxLevels, maxLevelLosses, maxLossesBeforeElimination) { }

		private IEnumerable<ProblemTower> ActiveTowers;

		protected override Task StartInternal(CancellationToken token)
		{
			var towers = Problems.Select(p => new ProblemTower(p, this)).ToList();
			//Towers = towers.AsReadOnly();
			ActiveTowers = towers.Where(t => !t.Problem.HasConverged);
			return base.StartInternal(token);
		}

#if DEBUG

		private const bool EMIT_GENOMES = false;

		// ReSharper disable once UnusedMember.Local
		static StringBuilder GetGenomeInfo(TGenome genome)
		{
			var sb = new StringBuilder(genome.Hash);
			sb.AppendLine();
			foreach (var logEntry in genome.Log)
			{
				sb.Append(logEntry.Category)
					.Append(" > ")
					.Append(logEntry.Message);

				var data = logEntry.Data;
				if (!string.IsNullOrWhiteSpace(data))
					sb.Append(':').AppendLine().Append(data);

				sb.AppendLine();
			}
			return sb;
		}
#endif

		protected override Task PostAsync(TGenome genome, CancellationToken token)
		{
			if (genome == null) throw new ArgumentNullException(nameof(genome));
			Contract.EndContractBlock();

#if DEBUG
			Debug.WriteLineIf(EMIT_GENOMES,
				$"Posting (async):\n{GetGenomeInfo(genome)}",
				"TowerProcessingScheme");
#endif
			return Task.WhenAll(ActiveTowers
				.Select(t => t.PostAsync(genome, token)));
		}

		protected override void Post(TGenome genome)
		{
			if (genome == null) throw new ArgumentNullException(nameof(genome));
			Contract.EndContractBlock();

#if DEBUG
			Debug.WriteLineIf(EMIT_GENOMES,
				$"Posting:\n{GetGenomeInfo(genome)}\n",
				"TowerProcessingScheme");
#endif

			foreach (var t in ActiveTowers)
				t.Post(genome);
		}
	}
}
