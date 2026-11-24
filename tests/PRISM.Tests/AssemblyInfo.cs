using Xunit;

// Disable test parallelization across all test collections to prevent concurrent I2C bus collision on physical monitor hardware
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
