namespace Microsoft.DotNet.ApiDiff.Tool.Tests;

public class ToolTests
{
    [Fact]
    public void Test()
    {
        while (!System.Diagnostics.Debugger.IsAttached)
        {
            System.Console.WriteLine($"Attach to {Environment.ProcessId}");
            System.Threading.Thread.Sleep(1000);
        }
        System.Console.WriteLine($"Attached to {Environment.ProcessId}");
        System.Diagnostics.Debugger.Break();

        string beforePath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}");
        string afterPath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}");
        string outputPath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}");

        DeleteDirectoryIfExistsAndRecreate(beforePath);
        DeleteDirectoryIfExistsAndRecreate(afterPath);
        DeleteDirectoryIfExistsAndRecreate(outputPath);

        string testDataPath = Path.Join(AppContext.BaseDirectory, "TestData");

        Assert.True(Directory.Exists(testDataPath), $"Test data path '{testDataPath}' does not exist");

        string originalBeforeDll = Path.Join(testDataPath, "before-System.Formats.Tar.dll.test");
        string originalAfterDll = Path.Join(testDataPath, "after-System.Formats.Tar.dll.test");
        string copiedBeforeDll = Path.Join(beforePath, "System.Formats.Tar.dll");
        string copiedAfterDll = Path.Join(afterPath, "System.Formats.Tar.dll");

        Assert.True(File.Exists(originalBeforeDll), $"File {originalBeforeDll} does not exist.");
        Assert.True(File.Exists(originalAfterDll), $"File {originalAfterDll} does not exist.");

        File.Copy(originalBeforeDll, copiedBeforeDll);
        File.Copy(originalAfterDll, copiedAfterDll);

        Assert.True(File.Exists(copiedBeforeDll), $"File {copiedBeforeDll} does not exist.");
        Assert.True(File.Exists(copiedAfterDll), $"File {copiedAfterDll} does not exist.");

        string[] args = [
            "-b", beforePath,
            "-a", afterPath,
            "-o", outputPath,
            "-tc", "test-title",
            "-bfn", "before",
            "-afn", "after"
        ];

        Task task = Task.Run(async () =>
        {
            await Microsoft.DotNet.ApiDiff.Tool.Program.Start(args);
        });

#pragma warning disable xUnit1031
        Task.WaitAll([task]);
#pragma warning restore xUnit1031

        Assert.NotEmpty(Directory.GetFiles(outputPath, "*.md", SearchOption.AllDirectories));

        Directory.Delete(beforePath, recursive: true);
        Directory.Delete(afterPath, recursive: true);
        Directory.Delete(outputPath, recursive: true);
    }

    private void DeleteDirectoryIfExistsAndRecreate(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }

        Directory.CreateDirectory(path);
    }

}

