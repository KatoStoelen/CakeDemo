#tool "NUnit.ConsoleRunner"

#addin "Cake.Yarn"
#addin "Cake.Npm"
#addin "Cake.Powershell"


//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target                      = Argument("target", "Build");
var buildConfiguration          = Argument("configuration", "Release");
var verbosity                   = Argument("verbosity", "Verbose");
var nugetFeed                   = Argument("nugetFeed", (string) null);
var apiKey                      = Argument("apiKey", (string) null);

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var solutionName                = "CakeDemoService";
var srcDirectory                = Directory("../src");
var solution                    = srcDirectory + File($"{solutionName}.sln");
var webPath                     = srcDirectory + Directory("Web");
var artifactsPath               = Directory("../artifacts");
var packagesPath                = artifactsPath + Directory("packages");
var testReportsPath             = artifactsPath + Directory("Reports");
var version                     = "1.0.0.0";

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("NuGet-Restore")
    .Does(() =>
{
    NuGetRestore(solution);
});

Task("Yarn-Install")
    .Does(() =>
{
    Yarn.FromPath(webPath).Install(settings => 
    {
        settings.ArgumentCustomization = args => args.Append("--ignore-engines");
    });
});

Task("Build-Web")
    .IsDependentOn("Yarn-Install")
    .Does(() =>
{
    NpmRunScript(new NpmRunScriptSettings
    {
        WorkingDirectory = webPath,
        ScriptName = "build"
    });
});

Task("Build")
    .IsDependentOn("NuGet-Restore")
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
        });
});

Task("Unit-Test")
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
            Results = testReportsPath + File("UnitTest.xml")
        });
});

Task("Integration-Test")
    .IsDependentOn("Build")
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
            Results = testReportsPath + File("IntegrationTest.xml")
        });
});

Task("Restore-Deploy-Addins")
    .Does(() =>
{
    Information("Restoring deploy addins...");
    Information(string.Empty);

    var deployScripts = GetFiles($"{srcDirectory}/**/deploy.ps1");

    foreach (var deployScript in deployScripts)
    {
        StartPowershellFile(
            deployScript,
            new PowershellSettings()
                .UseWorkingDirectory(deployScript.GetDirectory())
                .WithArguments(args =>
                {
                    args.Append("DryRun", string.Empty);
                }));

        Information(string.Empty);
    }
});

Task("Pack-Packages")
    .IsDependentOn("Build-Web")
    .IsDependentOn("Build")
    .IsDependentOn("Restore-Deploy-Addins")
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
    .IsDependentOn("Pack-Packages")
    .WithCriteria(() => !string.IsNullOrEmpty(nugetFeed))
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

Task("Install")
    .IsDependentOn("Pack-Packages")
    .Does(() =>
{
    var nupkgs = GetFiles($"{packagesPath}/*.nupkg");

    foreach (var nupkg in nupkgs)
    {
        Unzip(nupkg, $"{packagesPath}/{nupkg.GetFilenameWithoutExtension()}");
    }

    var deployScripts = GetFiles($"{packagesPath}/**/scripts/deploy.cake");

    foreach (var deployScript in deployScripts)
    {
        CakeExecuteScript(deployScript);
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

Task("CIBuild")
    .IsDependentOn("NuGet-Restore")
    .IsDependentOn("Yarn-Install")
    .IsDependentOn("Build-Web")
    .IsDependentOn("Build")
    .IsDependentOn("Unit-Test")
    .IsDependentOn("Integration-Test")
    .IsDependentOn("Pack-Packages")
    .IsDependentOn("Push-Packages");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);