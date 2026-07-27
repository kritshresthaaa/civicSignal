namespace CivicSignal.Architecture.Tests;

public sealed class CleanArchitectureReferenceTests
{
    [Fact]
    public void Domain_has_no_project_references()
    {
        var domainProject = ReadProject("src", "CivicSignal.Domain", "CivicSignal.Domain.csproj");

        Assert.DoesNotContain("<ProjectReference", domainProject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Application_depends_on_domain_but_not_infrastructure_or_api()
    {
        var applicationProject = ReadProject("src", "CivicSignal.Application", "CivicSignal.Application.csproj");

        Assert.Contains("CivicSignal.Domain.csproj", applicationProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CivicSignal.Infrastructure.csproj", applicationProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CivicSignal.Api.csproj", applicationProject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Api_and_worker_do_not_reference_domain_directly()
    {
        var apiProject = ReadProject("src", "CivicSignal.Api", "CivicSignal.Api.csproj");
        var workerProject = ReadProject("src", "CivicSignal.Worker", "CivicSignal.Worker.csproj");

        Assert.DoesNotContain("CivicSignal.Domain.csproj", apiProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CivicSignal.Domain.csproj", workerProject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Application_uses_service_oriented_folders()
    {
        var applicationRoot = Path.Combine(FindRepoRoot(), "src", "CivicSignal.Application");
        var workflowFolderPaths = Directory
            .EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                path.Contains($"{Path.DirectorySeparatorChar}Commands{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                path.Contains($"{Path.DirectorySeparatorChar}Queries{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(applicationRoot, path))
            .ToArray();

        Assert.Empty(workflowFolderPaths);
    }

    private static string ReadProject(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), Path.Combine(pathParts)));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CivicSignal.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
