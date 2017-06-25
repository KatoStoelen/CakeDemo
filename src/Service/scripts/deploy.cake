#addin "Cake.Services"

var target = Argument("target", "Install");

Task("Install")
    .Does(() =>
{
    InstallService(new InstallSettings
    {
        ServiceName = "CakeDemoSvc",
        DisplayName = "CakeDemoSvc",
        Description = "CakeDemo Service",
        StartMode = "auto",
        ExecutablePath = MakeAbsolute(Directory("../") + File("CakeDemoSvc.exe"))
    });
});

RunTarget(target);