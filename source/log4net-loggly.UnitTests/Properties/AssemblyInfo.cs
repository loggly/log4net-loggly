using Xunit;

// log4net initialization is not thread safe and integration tests use it
[assembly: CollectionBehavior(DisableTestParallelization = true)]