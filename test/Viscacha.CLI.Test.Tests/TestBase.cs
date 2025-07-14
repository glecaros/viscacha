using System;
using System.IO;

namespace Viscacha.CLI.Test.Tests;

public abstract class TestBase
{
    protected string _tempDirectory;

    [SetUp]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    protected class TestFile : IDisposable
    {
        public TestFile(string path, string content)
        {
            Path = path;
            Content = content;
            File.WriteAllText(path, content);
        }

        public string Path { get; }
        public string Content { get; }

        public FileInfo ToFileInfo() => new(Path);

        public static implicit operator FileInfo(TestFile testFile) => testFile.ToFileInfo();
        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }

    protected TestFile CreateTestFile(string filename, string content)
    {
        var filePath = Path.Combine(_tempDirectory, filename);
        return new TestFile(filePath, content);
    }
}
