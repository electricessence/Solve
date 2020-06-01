import OrderedRegistry from "@tsdotnet/ordered-registry";

export interface GenomeFitnessState
{
	readonly hash: string;
	level: number;
	ranking: number;
	fitness: number;
	lossCount: number;
	rejectionCount:number;
	alive: boolean;
}

export default class DataService
{
	private readonly _queue = new OrderedRegistry<string, GenomeFitnessState>();

	changed(e: GenomeFitnessState)
	{
		this._queue.register(e.hash, e);
	}

	dequeueChange(): GenomeFitnessState | undefined
	{
		return this._queue.takeFirst()?.value;
	}

	dequeueChanges(max: number = Number.MAX_SAFE_INTEGER): GenomeFitnessState[]
	{
		const result: GenomeFitnessState[] = [];
		for (let i = 0 | 0; i < max; i++)
		{
			const e = this.dequeueChange();
			if (!e) break;
			result.push(e);
		}
		return result;
	}
}
