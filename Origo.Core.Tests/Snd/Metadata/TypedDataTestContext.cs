using System;
using Origo.Core.Snd.Metadata;
using Xunit;

namespace Origo.Core.Tests;

[CollectionDefinition("TypedData")]
public class TypedDataTestContext : ICollectionFixture<TypedDataTestContext.Fixture>
{
    public class Fixture : IDisposable
    {
        public Fixture()
        {
            TypedData.ResetForTesting();
        }

        public void Dispose()
        {
            TypedData.ResetForTesting();
            GC.SuppressFinalize(this);
        }
    }
}
