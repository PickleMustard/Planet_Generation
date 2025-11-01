using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetGeneration
{
    public class MeshGenerationTask
    {
        public string Id { get; set; }
        public string BodyName { get; set; }
        public TaskType Type { get; set; }
        public TaskPriority Priority { get; set; }
        public Task Task { get; set; }
        public DateTime StartTime { get; set; }
        public Action<float> ProgressCallback { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public Exception Exception { get; set; }
        public bool IsCompleted { get; set; }
        public float Progress { get; set; }
    }

    public enum TaskType
    {
        BaseMeshGeneration,
        Deformation,
        VoronoiGeneration,
        BiomeAssignment,
        SurfaceGeneration
    }

    public enum TaskPriority
    {
        High = 0,
        Medium = 1,
        Low = 2,
        Background = 3
    }

    public class SystemResourceStatus
    {
        public int ActiveThreads { get; set; }
        public int AvailableMemoryMB { get; set; }
        public float CpuUsage { get; set; }
        public int QueuedTasks { get; set; }
    }
}