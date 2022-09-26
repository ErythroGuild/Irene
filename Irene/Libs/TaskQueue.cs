namespace Irene;

// A lightweight, thread-safe class, which executes queued tasks FIFO.
class TaskQueue {
	public bool IsRunning => !_task.IsCompleted;

	private readonly ConcurrentQueue<Task> _queue = new ();
	private Task _task = Task.CompletedTask; // initialize as available

	// `AddAsync` overloads will queue a task to be run successively,
	// and will return when the queued task has been completed.
	public async Task Run(Task action) {
		_queue.Enqueue(action);
		StartQueue();
		await action;
	}
	public async Task<TResult> Run<TResult>(Task<TResult> action) {
		_queue.Enqueue(action);
		StartQueue();
		return await action;
	}

	// A helper method to ensure the queue is being worked on. Returns
	// immediately if the queue is already running.
	private void StartQueue() {
		if (IsRunning)
			return;

		_task = Task.Run(async () => {
			while (!_queue.IsEmpty) {
				_queue.TryDequeue(out Task? task);

				if (task is null)
					continue;

				if (task.Status == TaskStatus.Created)
					task.Start();

				await task;
			}
		});
	}
}
