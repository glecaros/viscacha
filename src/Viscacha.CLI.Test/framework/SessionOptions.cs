using System.IO;

namespace Viscacha.CLI.Test.Framework;

public record SessionOptions(FileInfo InputFile, DirectoryInfo? ResponsesDirectory);
