#tool "NUnit.ConsoleRunner"
#tool "NUnit.Extension.NUnitV2ResultWriter"
#tool "NUnit.Extension.NUnitV2Driver"
#tool "NUnit.Extension.TeamCityEventListener"

#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0-beta0011"

#addin "NuGet.Core"
#addin "Cake.ExtendedNuGet"
#addin "Cake.Npm"
#addin "Cake.Powershell"
#addin "Cake.Git"

#load "version.cake"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target                      = Argument("target", "Build");
var branch                      = Argument("branch", "master");
var buildConfiguration          = Argument("configuration", "Release");
var verbosity                   = Argument("verbosity", "Verbose");
var nugetFeed                   = Argument("nugetFeed", (string) null);
var apiKey                      = Argument("apiKey", (string) null);
var buildLogger                 = Argument("buildLogger", (string) null);

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var solutionName                = "AnnuityClaimReserves";
var repositoryUrlFormat         = "http://tfs2010at.ifint.biz/tfs/TFS2008Collection/WayPoint%20GIT/_git/{0}";
var repositoryUrl               = string.Format(repositoryUrlFormat, solutionName);
var solution                    = Directory("../") + File($"{solutionName}.sln");
var webPath                     = Directory("../Web");
var artifactsPath               = Directory("../artifacts");
var packagesPath                = artifactsPath + Directory("packages");
var testReportsPath             = artifactsPath + Directory("Reports");
var versionInfo                 = new VersionInfo("1.0.0.0");

//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////

Setup(context =>
{
    if (BuildSystem.IsRunningOnTeamCity)
    {
        var isMasterBranch = System.Text.RegularExpressions.Regex.IsMatch(branch, @"^(refs\/heads\/)?master$");

        versionInfo = GetVersionInfo(
            Context, repositoryUrl, branch, includePreReleaseTag: !isMasterBranch);

        BuildSystem.TeamCity.SetBuildNumber(versionInfo.Version);
    }

    Information($"Version: {versionInfo.Version}");
    Information($"Assembly version: {versionInfo.AssemblyVersion}");
});

TaskSetup(context =>
{
    if (BuildSystem.IsRunningOnTeamCity)
    {
        BuildSystem.TeamCity.WriteStartBlock(context.Task.Name);

        if (!string.IsNullOrEmpty(context.Task.Description))
        {
            BuildSystem.TeamCity.WriteProgressMessage(context.Task.Description);
        }
    }
});

TaskTeardown(context =>
{
    if (BuildSystem.IsRunningOnTeamCity)
    {
        BuildSystem.TeamCity.WriteEndBlock(context.Task.Name);
    }
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clone-PsakeScripts")
    .Description("Cloning PsakeScripts repository")
    .Does(() =>
{
    if (DirectoryExists("./PsakeScripts"))
    {
        StartPowershellScript(
            "Remove-Item",
            args =>
                args.Append(@".\PsakeScripts")
                    .AppendSwitch("-Recurse", string.Empty)
                    .AppendSwitch("-Force", string.Empty));
    }

    GitClone(
        "http://tfs2010at.ifint.biz/tfs/TFS2008Collection/WayPoint%20GIT/_git/PsakeScripts",
        Directory("./PsakeScripts"));
});

Task("NuGet-Restore")
    .Description("Restoring NuGet packages")
    .Does(() =>
{
    NuGetRestore(solution);
});

Task("Npm-Install")
    .Description("Installing NPM packages")
    .Does(() =>
{
    NpmInstall(new NpmInstallSettings
    {
        WorkingDirectory = webPath
    });
});

Task("Webpack")
    .Description("Running Webpack")
    .IsDependentOn("Npm-Install")
    .Does(() =>
{
    NpmRunScript(new NpmRunScriptSettings
    {
        WorkingDirectory = webPath,
        ScriptName = buildConfiguration == "Release"
            ? "build"
            : "build-dev"
    });
});

Task("Build")
    .Description($"Building {solutionName}")
    .IsDependentOn("NuGet-Restore")
    .IsDependentOn("Webpack")
    .Does(() =>
{
    MSBuild(
        solution,
        settings =>
        {
            settings
                .SetConfiguration(buildConfiguration)
                .SetNodeReuse(false)
                .SetMaxCpuCount(0)
                .SetVerbosity((Verbosity) Enum.Parse(typeof(Verbosity), verbosity))
                .WithTarget("Clean")
                .WithTarget("Build")
                .WithProperty("DebugSymbols", new[] { "true" });

            if (!string.IsNullOrEmpty(buildLogger))
            {
                settings.ArgumentCustomization =
                    arguments => arguments.Append($"/logger:{buildLogger}");
            }
        });
});

Task("Unit-Test")
    .Description("Running unit tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    if (!DirectoryExists(testReportsPath))
    {
        CreateDirectory(testReportsPath);
    }

    var unitTestAssemblies = GetFiles($"../Test/**/bin/{buildConfiguration}/*UnitTest*.dll");

    NUnit3(
        unitTestAssemblies,
        new NUnit3Settings()
        {
            NoHeader = true,
            ResultFormat = "nunit2",
            TeamCity = BuildSystem.IsRunningOnTeamCity,
            Results = testReportsPath + File("UnitTest.xml")
        });
});

Task("Migrate-Database")
    .Description("Migrating database")
    .IsDependentOn("Clone-PsakeScripts")
    .Does(() =>
{
    CopyDirectory("PsakeScripts", "../Database/scripts/PsakeScripts");

    StartPowershellFile("../Database/scripts/install.ps1");

    StartPowershellScript(
            "Remove-Item",
            args =>
                args.Append(@"..\Database\scripts\PsakeScripts")
                    .AppendSwitch("-Recurse", string.Empty)
                    .AppendSwitch("-Force", string.Empty));
});

Task("Integration-Test")
    .Description("Running integration tests")
    .IsDependentOn("Build")
    .IsDependentOn("Migrate-Database")
    .Does(() =>
{
    if (!DirectoryExists(testReportsPath))
    {
        CreateDirectory(testReportsPath);
    }

    var integrationTestAssemblies = GetFiles($"../Test/**/bin/{buildConfiguration}/*IntegrationTest*.dll");

    NUnit3(
        integrationTestAssemblies,
        new NUnit3Settings()
        {
            NoHeader = true,
            ResultFormat = "nunit2",
            TeamCity = BuildSystem.IsRunningOnTeamCity,
            Results = testReportsPath + File("IntegrationTest.xml")
        });
});

Task("Pack-Packages")
    .Description("Creating NuGet packages")
    .IsDependentOn("Build")
    .IsDependentOn("Clone-PsakeScripts")
    .Does(() =>
{
    if (DirectoryExists(packagesPath))
    {
        CleanDirectory(packagesPath);
    }
    else
    {
        CreateDirectory(packagesPath);
    }

    var nuspecFiles = GetFiles("../**/*.deploy.nuspec");

    NuGetPack(
        nuspecFiles,
        new NuGetPackSettings
        {
            OutputDirectory = packagesPath,
            NoPackageAnalysis = true,
            Properties = new Dictionary<string, string>
            {
                ["configuration"] = buildConfiguration
            }
        });
});

Task("Push-Packages")
    .Description("Pushing NuGet packages")
    .IsDependentOn("Pack-Packages")
    .WithCriteria(() => BuildSystem.IsRunningOnTeamCity)
    .Does(() =>
{
    var nugetPackages = GetFiles($"{packagesPath}/*.nupkg");

    NuGetPush(
        nugetPackages,
        new NuGetPushSettings
        {
            Source = nugetFeed,
            ApiKey = apiKey
        });
});

Task("Create-AssemblyInfo")
    .Description("Creating AssemblyInfo")
    .Does(() =>
{
    var assemblyInfoFiles = GetFiles("../**/AssemblyInfo.cs");

    foreach (var assemblyInfoFile in assemblyInfoFiles)
    {
        Information($"Creating assembly info: {assemblyInfoFile}");

        var pathSegments = assemblyInfoFile.Segments;
        var projectName = pathSegments[pathSegments.Length - 3];

        CreateAssemblyInfo(
            assemblyInfoFile,
            new AssemblyInfoSettings
            {
                Title = $"{solutionName}.{projectName}",
                Description = string.Empty,
                Configuration = string.Empty,
                Company = "If",
                Product = solutionName,
                Copyright = $"© Copyright If {DateTime.Now.Year}. All Rights reserved.",
                Trademark = string.Empty,
                ComVisible = false,
                Version = versionInfo.AssemblyVersion,
                FileVersion = versionInfo.Version
            });
    }
});

Task("Rewrite-NuSpec-Version")
    .Description("Rewriting NuSpec version number")
    .Does(() =>
{
    var nuspecFiles = GetFiles("../**/*.deploy.nuspec");

    foreach (var nuspecFile in nuspecFiles)
    {
        Information($"Setting version {versionInfo.Version} in NuSpec: {nuspecFile}");

        XmlPoke(
            nuspecFile,
            "/package/metadata/version",
            versionInfo.Version);
    }
});

Task("Install")
    .Description("Installing deployable units")
    .IsDependentOn("Pack-Packages")
    .Does(() =>
{
    var nupkgs = GetFiles($"{packagesPath}/*.nupkg");

    foreach (var nupkg in nupkgs)
    {
        Unzip(nupkg, $"{packagesPath}/{nupkg.GetFilenameWithoutExtension()}");
    }

    var installScripts = GetFiles($"{packagesPath}/**/scripts/install.ps1");

    foreach (var installScript in installScripts)
    {
        StartPowershellFile(
            "installscriptwrapper.ps1",
            args =>
                args.AppendQuoted(MakeAbsolute(installScript).ToString()));
    }
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build");

Task("Test")
    .IsDependentOn("Unit-Test")
    .IsDependentOn("Integration-Test");

Task("Rewrite-Version")
    .IsDependentOn("Create-AssemblyInfo")
    .IsDependentOn("Rewrite-NuSpec-Version");

Task("CIBuild")
    .IsDependentOn("NuGet-Restore")
    .IsDependentOn("Npm-Install")
    .IsDependentOn("Webpack")
    .IsDependentOn("Rewrite-Version")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Pack-Packages")
    .IsDependentOn("Push-Packages");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);