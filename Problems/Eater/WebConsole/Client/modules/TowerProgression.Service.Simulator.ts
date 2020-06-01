/*!
 * @author electricessence / https://github.com/electricessence/
 * @license MIT
 */

import {LinkedNodeWithValue, LinkedValueNodeList} from "@tsdotnet/linked-node-list";
import ObjectPool from "@tsdotnet/object-pool";
import DataService, {GenomeFitnessState} from "./TowerProgression.Service";

type GFS = GenomeFitnessState & { readonly fitnessRange: Readonly<{ low: number, range:number }> }

type Node = LinkedNodeWithValue<GFS>;

const nodePool = ObjectPool.create<Node>(
	() =>
	{
		return {} as Node;
	},
	recycling =>
	{
		if (recycling.next || recycling.previous) throw "Node still belongs to list.";
		recycling.value = undefined!;
	});

let newIndex = 0;

export function simulate(service: DataService, updateDelay: number = 0): () => void
{
	const poolSize = 40;
	const maxLoss = 5;
	const maxRejection = 5;

	class Level extends LinkedValueNodeList<GFS>
	{
		private _changed: boolean = false;

		process(): boolean
		{
			if (!this._changed) return false;
			this._changed = false;
			let changed = false;
			let len = this.unsafeCount;
			if (len > 1)
			{
				let mid = Math.ceil(len / 2);

				if (len >= poolSize)
				{
					while (this.unsafeCount > mid)
					{
						const n = this.takeLast()!;
						n.value.rejectionCount = 0; // reset consecutive.
						promote(n);
					}

					let n = this.first;
					while (n)
					{
						const next = n.next;
						if (++n.value.lossCount > maxLoss)
						{
							this.removeNode(n);
							if (++n.value.rejectionCount > maxRejection) kill(n);
							else promote(n); // give losers another chance.
						}
						n = next;
					}
					len = this.unsafeCount;

					changed = true;
				}
			}

			if (len == 1) updateRank(this.first!.value, 0);
			if (len > 1)
			{
				let mid = Math.ceil(len / 2);

				let i = 0;
				for (const n of this)
				{
					updateRank(n.value, i < mid ? (i - mid) : (i - mid + 1));
					i++;
				}
			}

			return changed;
		}

		add(node: Node): void
		{
			this._changed = true;
			const _ = this, {fitness, lossCount} = node.value;
			let tail = _.last;
			if (!tail)
			{
				this.addNode(node);
				return;
			}
			else
			{
				let head = _.first;
				while (head && tail)
				{
					const tf = tail.value.fitness;
					if (fitness > tf || fitness === tf && lossCount <= tail.value.lossCount)
					{
						this.addNodeAfter(node, tail);
						return;
					}

					tail = tail.previous;
					if (fitness < head.value.fitness)
					{
						this.addNodeBefore(node, head);
						return;
					}
					head = head.next;
				}
			}
			console.error("Problem sorting item.");
		}


	}

	const levels: Level[] = [new Level()];

	const intervalId = setInterval(update, updateDelay);

	function update()
	{
		levels[0].add(getNewNode());
		for (let level = 0; level < levels.length; level++)
		{
			let a = levels[level];
			if (!a.process()) break;
		}
	}

	return () =>
	{
		clearInterval(intervalId);
	}

	function updateRank(v: GFS, rank: number)
	{
		if (v.ranking !== rank)
		{
			v.ranking = rank;
			service.changed(v);
		}
	}

	function promote(node: Node)
	{
		const v = node.value;
		ingest(v);
		const level = ++v.level;

		let b = levels[level];
		if (!b) levels[level] = b = new Level()
		b.add(node);
		service.changed(v);
	}

	function kill(n: Node): void
	{
		const v = n.value;
		nodePool.give(n);
		v.alive = false;
		service.changed(v);
	}
}


function newGFS(): GFS
{
	const high = Math.random() * 100;
	const low = Math.random() * 0.75 * high;
	const range = high - low;
	return {
		hash: (newIndex++) + "",
		level: 0,
		ranking: 0,
		fitness: 0,
		lossCount: 0,
		fitnessRange: Object.freeze({low,range}),
		rejectionCount: 0,
		alive: true
	}
}

function getNewNode()
{
	const n = nodePool.take();
	n.value = newGFS();
	return n;
}

function ingest(e: GFS)
{
	e.lossCount = 0;
	e.ranking = 0;
	e.fitness = Math.random() * e.fitnessRange.range + e.fitnessRange.low;
	return e;
}
