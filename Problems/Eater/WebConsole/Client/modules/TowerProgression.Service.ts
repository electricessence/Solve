import IMap from 'typescript-dotnet-core/IMap'
import LinkedNodeList from 'typescript-dotnet-core/Collections/LinkedNodeList';
import { ILinkedNodeWithValue } from 'typescript-dotnet-core/Collections/ILinkedListNode';
import ObjectPool from 'typescript-dotnet-core/Disposable/ObjectPool';

export interface GenomeFitnessState {
	hash: string;
	level: number;
	ranking: number;
	fitness: number;
	lossCount: number;
}

type Node = ILinkedNodeWithValue<GenomeFitnessState>;

const nodePool = ObjectPool.create<Node>(
	() => {
		//@ts-ignore;
		return <Node>{};
	},
	recycling => {
		if (recycling.next || recycling.previous) throw "Node still belongs to list.";
		recycling.value = undefined!;
	});

function getNode(state: GenomeFitnessState) {
	const n = nodePool.take();
	n.value = state;
	return n;
}

export class Service {
	private readonly _queue = new LinkedNodeList<Node>();
	private readonly _queueRegistry: IMap<Node> = {};

	changed(e: GenomeFitnessState) {
		const _ = this;
		const key = e.hash;
		const reg = _._queueRegistry;
		const node = reg[key];
		if (node) node.value = e;
		else {
			const n = getNode(e);
			reg[key] = n;
			_._queue.addNode(n);
		}
	}

	dequeueChange(): GenomeFitnessState | null {
		const q = this._queue;
		const node = q.first;
		if (!node) return null;
		q.removeFirst();
		try {
			return node.value;
		}
		finally {
			nodePool.give(node);
		}
	}

	dequeueChanges(max: number = Number.MAX_SAFE_INTEGER): GenomeFitnessState[] {
		const result: GenomeFitnessState[] = [];
		for (var i = 0 | 0; i < max; i++) {
			const e = this.dequeueChange();
			if (!e) break;
			result.push(e);
		}
		return result;
	}
}

class Level {
	count = 0;

	entries = new LinkedNodeList<Node>();

	add(node: Node) {
		const e = this.entries;
		const f = node.value.fitness;
		let tail: Node | null | undefined = e.last;
		if (!tail) {
			this.entries.addNode(node);
			this.count++;
			return;
		} else {
			let head: Node | null | undefined = e.first;
			while (head && tail) {
				if (tail.value.fitness < f) {
					this.entries.addNodeAfter(node, tail);
					this.count++;
					return;
				}
				tail = tail.previous;
				if (head.value.fitness >= f) {
					this.entries.addNodeBefore(node, head);
					this.count++;
					return;
				}
				if (head == tail) break;
				head = head.next;
			}
		}
		throw "Unexpected emtpy.";
	}

	takeLast(): Node | null {
		const e = this.entries;
		const last = e.last;
		if (last && e.removeLast()) this.count--;
		return last;
	}

}

let newIndex = 0;
const statePool = ObjectPool.create<Node>(
	() => {
		return getNode(injest({
			hash: (newIndex++) + "",
			level: 0,
			ranking: 0,
			fitness: 0,
			lossCount: 0
		}));
	},
	o => {
		const e = o.value;
		e.hash = (newIndex++) + "";
		injest(e);
		e.level = e.lossCount = 0;
	});

export function simulate(service: Service, updateDelay: number = 0): () => void {


	const levels: Level[] = [new Level()];
	const poolSize = 10;
	const maxLoss = 5;
	const lossInc = 1 / maxLoss;

	const intervalId: NodeJS.Timeout = setInterval(update, updateDelay);
	function update() {
		const newEl = statePool.take();
		levels[0].add(newEl);
		service.changed(newEl.value);
		const levelCount = levels.length;
		for (let level = 0; level < levelCount; level++) {
			const nextLevel = level + 1;
			let a = levels[level];
			let len = a.count;
			if (len > 1) {

				let mid = Math.ceil(len / 2);

				if (len >= poolSize) {
					while (a.count > mid) {
						const n = a.takeLast()!;
						promote(n.value);
						let b = levels[nextLevel] = levels[nextLevel] || new Level();
						b.add(n);
						service.changed(n.value);
					}
					{
						let n: Node | null | undefined = a.entries.first;
						while (n) {
							const next: Node | null | undefined = n.next;
							const v = n.value;
							v.level += lossInc;
							if (v.level >= nextLevel) {
								a.entries.removeNode(n);
								statePool.give(n);
							}
							n = next;
						}
					}

					len = a.count;
					mid = Math.ceil(len);
				}

				if (len > 1) {
					a.entries.forEach((n, i) => {
						updateRank(n.value, i < mid ? (i - mid) : (i - mid + 1));
					});
				}
			}

			if (len == 1)
				updateRank(a.entries.first!.value, 0);
		}
	}

	return () => {
		clearInterval(intervalId);
	}

	function updateRank(v: GenomeFitnessState, rank: number) {
		if (v.ranking !== rank) {
			v.ranking = rank;
			service.changed(v);
		}
	}
}


function injest(e: GenomeFitnessState) {
	e.ranking = 0;
	e.fitness = Math.random() * 100;
	return e;
}

function promote(e: GenomeFitnessState) {
	injest(e);
	e.level = Math.floor(e.level + 1);
	return e;
}