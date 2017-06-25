#addin "Cake.IIS"

var target = Argument("target", "Install");

Task("Create-AppPool")
    .Does(() =>
{
    CreatePool(new ApplicationPoolSettings()
    {
        Name = "CakeDemoPool"
    });
});

Task("Create-WebSite")
    .IsDependentOn("Create-AppPool")
    .Does(() =>
{
    CreateWebsite(new WebsiteSettings()
    {
        Name = "CakeDemo.Web",
        PhysicalDirectory = MakeAbsolute(Directory("../")),

        ApplicationPool = new ApplicationPoolSettings()
        {
            Name = "CakeDemoPool"
        }
    });
});

Task("Install")
    .IsDependentOn("Create-WebSite");

RunTarget(target);