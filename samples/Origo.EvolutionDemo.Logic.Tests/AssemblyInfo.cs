using Xunit;

// GenomeContentGate is process-global for the Godot-free logic tests. Keep
// tests serial so a gate test cannot observe another test's temporary filter.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
