var target = Argument("target", "Install");

Task("Install")
    .Does(() =>
{
    Information("Installing database...");
});

RunTarget(target);