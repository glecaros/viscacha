using System.IO;

namespace Viscacha.TestRunner.Framework;

public record SessionOptions(FileInfo InputFile, DirectoryInfo? ResponsesDirectory);