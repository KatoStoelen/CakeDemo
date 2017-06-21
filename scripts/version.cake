public class VersionInfo
{
    public VersionInfo(string version) : this(version, version) { }

    public VersionInfo(string version, string assemblyVersion)
    {
        Version = version;
        AssemblyVersion = assemblyVersion;
    }

    public string Version { get; }
    public string AssemblyVersion { get; }
}

public static VersionInfo GetVersionInfo(
    ICakeContext context,
    string repositoryUrl,
    string branch,
    bool includePreReleaseTag = false)
{
    var gitVersion = context.GitVersion(
        new GitVersionSettings
        {
            Url = repositoryUrl,
            Branch = branch
        });

    var version = includePreReleaseTag
        ? $"{gitVersion.MajorMinorPatch}.{gitVersion.CommitsSinceVersionSource}-{gitVersion.PreReleaseLabel}"
        : $"{gitVersion.MajorMinorPatch}.{gitVersion.CommitsSinceVersionSource}";

    return new VersionInfo(version, gitVersion.AssemblySemVer);
}