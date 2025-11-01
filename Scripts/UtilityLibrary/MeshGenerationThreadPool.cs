using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace PlanetGeneration
{
    public partial class MeshGenerationThreadPool : Node
    {
        private static MeshGenerationThreadPool _instance;
        public static MeshGenerationThreadPool Instance
        {
            get
            {
                if (_instance == null)
                {
                    GD.PrintErr("MeshGenerationThreadPool not initialized. Call Initialize() first.");
                }
                return _instance;
            }
        }

        private int maxConcurrentThreads;
        private SemaphoreSlim semaphore;
        private Dictionary<TaskPriority, Queue<MeshGenerationTask>> taskQueues;
        private List<Task> activeTasks;
        private Dictionary<string, MeshGenerationTask> taskRegistry;
        private CancellationTokenSource cancellationTokenSource;
        private bool isInitialized = false;
        private readonly object lockObject = new object();

        public override void _Ready()
        {
            if (_instance == null)
            {
                _instance = this;
                Initialize();
            }
        }

        public void Initialize()
        {
            if (isInitialized) return;

            lock (lockObject)
            {
                if (isInitialized) return;

                maxConcurrentThreads = DetermineOptimalThreadCount();
                GD.Print($"Max concurrent threads: {maxConcurrentThreads}");
                semaphore = new SemaphoreSlim(maxConcurrentThreads, maxConcurrentThreads);
                taskQueues = new Dictionary<TaskPriority, Queue<MeshGenerationTask>>();
                activeTasks = new List<Task>();
                taskRegistry = new Dictionary<string, MeshGenerationTask>();
                cancellationTokenSource = new CancellationTokenSource();

                foreach (TaskPriority priority in Enum.GetValues(typeof(TaskPriority)))
                {
                    taskQueues[priority] = new Queue<MeshGenerationTask>();
                }

                isInitialized = true;
                GD.Print($"MeshGenerationThreadPool initialized with {maxConcurrentThreads} threads");

                StartTaskProcessor();
            }
        }

        public void Shutdown()
        {
            if (!isInitialized) return;

            lock (lockObject)
            {
                if (!isInitialized) return;

                cancellationTokenSource.Cancel();
                semaphore.Dispose();
                _instance = null;
                isInitialized = false;
                GD.Print("MeshGenerationThreadPool shutdown");
            }
        }

        private int DetermineOptimalThreadCount()
        {
            // Base calculation on CPU cores
            int processorCount = System.Environment.ProcessorCount;
            int optimalThreads = Math.Max(1, processorCount - 2);
            return optimalThreads;
        }

        public Task<T> EnqueueTask<T>(Func<T> work, string taskId, TaskPriority priority, string bodyName)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("ThreadPool not initialized");
            }

            var taskCompletionSource = new TaskCompletionSource<T>();
            var meshTask = new MeshGenerationTask
            {
                Id = taskId,
                BodyName = bodyName,
                Priority = priority,
                CancellationToken = cancellationTokenSource.Token,
                StartTime = DateTime.Now
            };

            lock (lockObject)
            {
                taskRegistry[taskId] = meshTask;
                taskQueues[priority].Enqueue(meshTask);
            }

            Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationTokenSource.Token);
                try
                {
                    if (cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        taskCompletionSource.SetCanceled();
                        return;
                    }

                    var result = work();
                    taskCompletionSource.SetResult(result);
                    meshTask.IsCompleted = true;
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Error in MeshGenerationThreadPool: {ex.Message}\n{ex.StackTrace}");
                    meshTask.Exception = ex;
                    taskCompletionSource.SetException(ex);
                }
                finally
                {
                    semaphore.Release();
                    lock (lockObject)
                    {
                        activeTasks.Remove(meshTask.Task);
                        taskRegistry.Remove(taskId);
                    }
                }
            }, cancellationTokenSource.Token);

            meshTask.Task = taskCompletionSource.Task;
            lock (lockObject)
            {
                activeTasks.Add(meshTask.Task);
            }

            return taskCompletionSource.Task;
        }

        public Task EnqueueTask(Action work, string taskId, TaskPriority priority, string bodyName)
        {
            return EnqueueTask(() =>
            {
                work();
                return true;
            }, taskId, priority, bodyName);
        }

        private async void StartTaskProcessor()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    MeshGenerationTask nextTask = null;
                    lock (lockObject)
                    {
                        foreach (var priority in Enum.GetValues(typeof(TaskPriority)).Cast<TaskPriority>())
                        {
                            if (taskQueues[priority].Count > 0)
                            {
                                nextTask = taskQueues[priority].Dequeue();
                                break;
                            }
                        }
                    }

                    if (nextTask != null)
                    {
                        await Task.Delay(10, cancellationTokenSource.Token);
                    }
                    else
                    {
                        await Task.Delay(100, cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void CancelTask(string taskId)
        {
            lock (lockObject)
            {
                if (taskRegistry.TryGetValue(taskId, out var task))
                {
                    taskRegistry.Remove(taskId);
                    foreach (var queue in taskQueues.Values)
                    {
                        var queueList = queue.ToList();
                        queue.Clear();
                        foreach (var item in queueList)
                        {
                            if (item.Id != taskId)
                            {
                                queue.Enqueue(item);
                            }
                        }
                    }
                }
            }
        }

        public void CancelTasksForBody(string bodyName)
        {
            lock (lockObject)
            {
                var tasksToRemove = taskRegistry.Values.Where(t => t.BodyName == bodyName).ToList();
                foreach (var task in tasksToRemove)
                {
                    CancelTask(task.Id);
                }
            }
        }

        public float GetProgress(string taskId)
        {
            lock (lockObject)
            {
                if (taskRegistry.TryGetValue(taskId, out var task))
                {
                    return task.Progress;
                }
            }
            return 0f;
        }

        public int GetActiveTaskCount()
        {
            lock (lockObject)
            {
                return activeTasks.Count(t => !t.IsCompleted);
            }
        }

        public int GetQueuedTaskCount()
        {
            lock (lockObject)
            {
                return taskQueues.Values.Sum(queue => queue.Count);
            }
        }

        public SystemResourceStatus GetResourceStatus()
        {
            lock (lockObject)
            {
                return new SystemResourceStatus
                {
                    ActiveThreads = GetActiveTaskCount(),
                    AvailableMemoryMB = (int)(GC.GetTotalMemory(false) / (1024 * 1024)),
                    CpuUsage = 0f,
                    QueuedTasks = GetQueuedTaskCount()
                };
            }
        }

        public override void _ExitTree()
        {
            Shutdown();
            base._ExitTree();
        }
    }
}
