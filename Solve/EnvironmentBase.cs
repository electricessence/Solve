/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Solve
{
	public abstract class EnvironmentBase<TGenome> : BroadcasterBase<(TGenome Genome, SampleFitnessCollectionBase Fitness, int SampleCount, int RejectionCount)>
		where TGenome : class, IGenome
	{
		protected readonly IGenomeFactory<TGenome> Factory;

		protected EnvironmentBase(IGenomeFactory<TGenome> genomeFactory)
			: base()
		{
			Factory = genomeFactory;
		}

		protected readonly CancellationTokenSource Canceller = new CancellationTokenSource();

		int _state = 0;

		protected bool CanStart => _state == 0;

		public Task Start()
		{
			switch (Interlocked.CompareExchange(ref _state, 1, 0))
			{
				case -1:
					throw new InvalidOperationException("Cannot start if cancellation requested.");

				case 0:
					if (Canceller.IsCancellationRequested)
						goto case -1;

					return StartInternal(Canceller.Token);

				case 1:
					throw new InvalidOperationException("Already started.");

			}

			return null;
		}

		protected abstract Task StartInternal(CancellationToken token);

		protected virtual void OnCancelled() { }

		public void Cancel()
		{
			if (-1 != Interlocked.Exchange(ref _state, -1))
			{
				Canceller.Cancel();
				OnCancelled();
			};
		}
	}


}
