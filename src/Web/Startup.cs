using System.IO;
using System.Web.Hosting;
using System.Web.Http;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Owin;

namespace Web
{
    public class Startup
    {
        public void Configuration(IAppBuilder builder)
        {
            var config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();

            ConfigureJsonFormatting(config);

            builder
                .UseWebApi(config)
                .UseFileServer(new FileServerOptions
                {
                    StaticFileOptions =
                    {
                        ServeUnknownFileTypes = true
                    },
                    FileSystem = new PhysicalFileSystem(Path.Combine(HostingEnvironment.ApplicationPhysicalPath, "dist"))
                })
                .UseStageMarker(PipelineStage.MapHandler);
        }

        private static void ConfigureJsonFormatting(HttpConfiguration config)
        {
            var settings = config.Formatters.JsonFormatter.SerializerSettings;
            settings.Converters.Add(new IsoDateTimeConverter());
            settings.Formatting = Formatting.Indented;
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        }
    }
}