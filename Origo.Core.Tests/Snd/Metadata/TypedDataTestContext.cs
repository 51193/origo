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
            TypedDataTestSupport.ResetKindRegistry();
        }

        public void Dispose()
        {
            TypedDataTestSupport.ResetKindRegistry();
            GC.SuppressFinalize(this);
        }
    }
}
