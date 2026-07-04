using System.IO;
using Godot;
using Origo.GodotAdapter.FileSystem;
using Origo.GodotAdapter.Integration.Tests.Runner;

namespace Origo.GodotAdapter.Integration.Tests;

public class GodotFileOperationsIntegrationTests
{
    [IntegrationTest(Description = "ReadAllText with null path throws ArgumentException")]
    public void ReadAllText_NullPath_Throws()
    {
        IntegrationTestRunner.AssertThrows<System.ArgumentException>(
            () => GodotFileOperations.ReadAllText(null!),
            "ReadAllText(null) should throw ArgumentException");
    }

    [IntegrationTest(Description = "ReadAllText with whitespace path throws ArgumentException")]
    public void ReadAllText_WhitespacePath_Throws()
    {
        IntegrationTestRunner.AssertThrows<System.ArgumentException>(
            () => GodotFileOperations.ReadAllText("   "),
            "ReadAllText(whitespace) should throw ArgumentException");
    }

    [IntegrationTest(Description = "ReadAllText for non-existent file throws FileNotFoundException")]
    public void ReadAllText_FileNotFound_Throws()
    {
        IntegrationTestRunner.AssertThrows<System.IO.FileNotFoundException>(
            () => GodotFileOperations.ReadAllText("user://nonexistent_read_file.dat"),
            "ReadAllText(nonexistent) should throw FileNotFoundException");
    }

    [IntegrationTest(Description = "WriteAllText with overwrite=false when file exists throws IOException")]
    public void WriteAllText_NoOverwrite_ExistingFile_Throws()
    {
        var path = "user://test_no_overwrite.txt";
        GodotFileOperations.WriteAllText(path, "first", overwrite: true);

        try
        {
            IntegrationTestRunner.AssertThrows<IOException>(
                () => GodotFileOperations.WriteAllText(path, "second", overwrite: false),
                "WriteAllText(overwrite=false) on existing file should throw IOException");
        }
        finally
        {
            GodotFileOperations.Delete(path);
        }
    }

    [IntegrationTest(Description = "Copy with missing source throws FileNotFoundException")]
    public void Copy_SourceMissing_Throws()
    {
        IntegrationTestRunner.AssertThrows<System.IO.FileNotFoundException>(
            () => GodotFileOperations.Copy("user://nonexistent_src.dat", "user://nonexistent_dst.dat", overwrite: true),
            "Copy(missing source) should throw FileNotFoundException");
    }

    [IntegrationTest(Description = "Write and Exists round-trip works correctly")]
    public void Write_And_Exists_RoundTrip()
    {
        var path = "user://test_exists_ops.txt";
        GodotFileOperations.WriteAllText(path, "data", overwrite: true);

        IntegrationTestRunner.Assert(
            GodotFileOperations.Exists(path),
            "File should exist after write.");

        GodotFileOperations.Delete(path);
        IntegrationTestRunner.Assert(
            !GodotFileOperations.Exists(path),
            "File should not exist after delete.");
    }

    [IntegrationTest(Description = "Copy duplicates file content")]
    public void Copy_DuplicatesContent()
    {
        var src = "user://test_copy_src.txt";
        var dst = "user://test_copy_dst.txt";
        var content = "copy me";

        GodotFileOperations.WriteAllText(src, content, overwrite: true);
        GodotFileOperations.Copy(src, dst, overwrite: true);

        IntegrationTestRunner.AssertEqual(content, GodotFileOperations.ReadAllText(dst), "copied content");

        GodotFileOperations.Delete(src);
        GodotFileOperations.Delete(dst);
    }
}
