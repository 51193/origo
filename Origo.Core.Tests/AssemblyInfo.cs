using Xunit;

// Test suite global serialization: process-wide static registries (e.g.
// TypedData.KindTypeMap, the strategy pool index registry, and the
// TypedDataLayeredRegistry delegate chain) are shared state that cannot be
// isolated per-test via AsyncLocal, and several tests reset/rebuild them
// (TypedDataTestSupport.ResetKindRegistry, per-test SndWorld registrations).
// Running in parallel would race those mutations. The assembly-wide
// collection behavior below disables xUnit parallelization for every test
// in this project; keep this attribute when adding new tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
