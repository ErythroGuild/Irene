namespace Irene;

// A lightweight, thread-safe class, which executes queued tasks FIFO.
class TaskQueue {
	public bool IsRunning => !_task.IsCompleted;

	// The first item in the task pair is used to start the action.
	// The second item awaits the result of the action, and indicates
	// when the queue can proceed to the next queued item.
	private record class TaskPair(Task EntryPoint, Task Result);

	private readonly ConcurrentQueue<TaskPair> _queue = new ();
	// The task that loops over all queue items.
	// Initialized as available, since the queue starts out empty.
	private Task _task = Task.CompletedTask;

	// The `Run()` methods will queue tasks to be run in succession,
	// and each task will return once it has been completed.
	//
	// Usage examples:
	// int x = await queue.Run(new Task<Task<int>>(async () => {
	//     await Task.Delay(1000);
	//     return 0;
	// }));
	// await queue.Run(new Task<Task>(async () => {
	//     await Task.Delay(1000);
	// }));
	//
	// This seemingly redundant syntax is actually required, to prevent
	// the defined lambda from executing immediately. The lambda cannot
	// be passed on its own, because the type of the lambda is dependent
	// on what it's being passed to.

	public async Task<TResult> Run<TResult>(Task<Task<TResult>> action) {
		// The action always requires 2 awaits: one to start the action,
		// and one to actually await the action's completion.
		Task result = Task.Run(async () => await await action);
		_queue.Enqueue(new (action, result));
		StartQueue();
		return await await action;
	}
	// This overload is actually a special case of `Task<Task<TResult>>`
	// with no inner `TResult`.
	public async Task Run(Task<Task> action) {
		// The action always requires 2 awaits: one to start the action,
		// and one to actually await the action's completion.
		Task result = Task.Run(async () => await await action);
		_queue.Enqueue(new (action, result));
		StartQueue();
		await await action;
	}

	// A helper method to ensure the queue is being worked on. Returns
	// immediately if the queue is already running.
	private void StartQueue() {
		if (IsRunning)
			return;

		_task = Task.Run(async () => {
			while (!_queue.IsEmpty) {
				_queue.TryDequeue(out TaskPair? queueItem);

				if (queueItem is null)
					continue;

				if (queueItem.EntryPoint.Status == TaskStatus.Created)
					queueItem.EntryPoint.Start();

				// Await to ensure all queue items are run successively.
				await queueItem.Result;
			}
		});
	}
}
