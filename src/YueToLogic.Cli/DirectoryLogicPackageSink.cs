using YueToLogic.Core.Logic;

namespace YueToLogic.Cli;

/// <summary>Writes a Logic package as a folder, which Finder and Logic treat as a single <c>.logicx</c> document.</summary>
internal sealed class DirectoryLogicPackageSink(string packagePath) : ILogicPackageSink
{
    public Stream CreateFile(string relativePath)
    {
        var path = Path.Combine(packagePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.Create(path);
    }
}
