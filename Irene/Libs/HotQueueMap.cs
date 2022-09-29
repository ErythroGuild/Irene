namespace Irene;

// This data structure is used to model cached, mapped data that is kept/
// discarded based on recency of access.
// It models a queue of mapped pairs where new items are pushed onto the
// top, stale items are purged from the bottom, and existing items "bubble"
// to the top if they are pushed when they already exist in the queue.
class HotQueueMap<TKey, TValue>
	where TKey : IEquatable<TKey>
{
	// Unpopulated items are stored as null.
	private (TKey Key, TValue Value)?[] _cache;

	// The queuemap can optionally be initialized with an existing list.
	// Items at the start of the list represent the most recently accessed
	// items in the queuemap.
	public HotQueueMap(int capacity, IReadOnlyList<(TKey, TValue)>? queue=null) {
		_cache = new (TKey, TValue)?[capacity];

		List<(TKey, TValue)> cacheInit = queue is null
			? new ()
			: new (queue);

		// Populate available slots with initial data.
		for (var i=0; i<Math.Min(capacity, cacheInit.Count); i++)
			_cache[i] = cacheInit[i];

		// Populate remaining slots with empty data.
		for (var i=cacheInit.Count; i<capacity; i++)
			_cache[i] = null;
	}

	// If the key was found and accessed, it is also brought to the front
	// of the cache queue.
	// The method returns true if the key was found and false otherwise.
	// This mimics the classic "TryParse" pattern (which is why an `out`
	// parameter is used).
	public bool TryAccess(TKey key, out TValue? value) {
		for (var i=0; i<_cache.Length; i++) {
			// Assigning a temporary here allows the compiler to correctly
			// analyze nullability.
			(TKey Key, TValue Value)? pair = _cache[i];

			// Reaching null indicates the remaining cache is unpopulated.
			if (pair is null) {
				value = default;
				return false;
			}

			if (pair.Value.Key.Equals(key)) {
				Bubble(i);
				value = pair.Value.Value;
				return true;
			}
		}

		value = default;
		return false;
	}

	// Returns false if a matching key already exists in the queue.
	// If a matching key exists, it is bubbled to the top of the queue,
	// and its mapped value is replaced (regardless of the current value).
	public bool Push(TKey key, TValue value) {
		// This is the index of the first null item.
		int end = _cache.Length;

		// Search forward through the cache for existing key.
		for (var i = 0; i<_cache.Length; i++) {
			// Assigning a temporary here allows the compiler to correctly
			// analyze nullability.
			(TKey Key, TValue Value)? pair = _cache[i];

			// Reached end of cache without finding key.
			if (pair is null) {
				end = i;
				break;
			}

			// Matching key was found; replace then bubble. Return.
			if (pair.Value.Key.Equals(key)) {
				_cache[i] = new (key, value);
				Bubble(i);
				return false;
			}
		}

		// Pop the last item off the end if the cache is full.
		if (end == _cache.Length)
			end--;

		// Bubble item to the front of the cache.
		_cache[end] = new (key, value);
		Bubble(end);
		return true;
	}

	// "Bubbles" the item at `_cache[i]` to the front (index 0).
	// `i` being signed is important to ensure the reverse loop terminates.
	private void Bubble(int i) {
		(TKey, TValue)? temporary = _cache[i];
		for (; i>0; i--)
			_cache[i] = _cache[i-1];
		_cache[0] = temporary;
	}
}
