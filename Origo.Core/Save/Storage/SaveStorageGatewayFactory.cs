using System;
using Origo.Core.Abstractions.FileSystem;
using Origo.Core.DataSource;

namespace Origo.Core.Save.Storage;

internal static class SaveStorageGatewayFactory
{
    internal static IDataSourceIoGateway CreateIoGateway(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return DataSourceFactory.CreateDefaultIoGateway(fileSystem, false);
    }

    internal static void ValidateRootPath(string path, string paramName, string message)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(message, paramName);
    }
}
