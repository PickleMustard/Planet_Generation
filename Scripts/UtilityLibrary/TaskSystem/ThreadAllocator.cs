using System;

namespace UtilityLibrary.TaskSystem
{
    public static class ThreadAllocator
    {
        public static int CalculateThreadCount()
        {
            int cores = Environment.ProcessorCount;
            return cores switch
            {
                <= 4 => Math.Max(2, (int)(cores * 0.5)),
                <= 8 => (int)(cores * 0.75),
                _ => (int)(cores * 0.9)
            };
        }

        public static ThreadAllocationInfo GetAllocationInfo()
        {
            int cores = Environment.ProcessorCount;
            float percentage = cores switch
            {
                <= 4 => 0.5f,
                <= 8 => 0.75f,
                _ => 0.9f
            };

            return new ThreadAllocationInfo
            {
                TotalCores = cores,
                AllocatedThreads = CalculateThreadCount(),
                AllocationPercentage = percentage
            };
        }
    }

    public class ThreadAllocationInfo
    {
        public int TotalCores { get; set; }
        public int AllocatedThreads { get; set; }
        public float AllocationPercentage { get; set; }
    }
}
