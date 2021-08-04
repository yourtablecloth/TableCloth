using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TableCloth.Contracts;
using TableCloth.Implementations;
using TableCloth.Implementations.WinForms;

namespace TableCloth
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);

            using var serviceProvider = services.BuildServiceProvider();
            var startup = serviceProvider.GetService<IAppStartup>();
            startup.StartApplication(args);
        }

        public static void ConfigureServices(ServiceCollection services)
        {
            // Add Logging
            services.AddLogging(c =>
            {
                c.AddSerilog(dispose: true);
            });

            // Add Services
            services.AddTransient<IX509CertPairScanner, X509CertPairScanner>();
            services.AddTransient<ICatalogDeserializer, CatalogDeserializer>();
            services.AddTransient<ISandboxSpecSerializer, SandboxSpecSerializer>();
            services.AddTransient<ISandboxBuilder, SandboxBuilder>();

            // Windows Forms UI
            services.AddSingleton<IAppStartup, WinFormAppStartup>();
        }
    }
}
