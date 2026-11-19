using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace WebBanking.Tests.Infrastructure;

public sealed class E2EApiFactory(string clientFolder) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("JWT_SECRET", "integration-tests-only-secret-key-32b!");
        builder.UseSetting("JWT_ISSUER", "WebBankingApp.Tests");

        builder.ConfigureTestServices(services =>
            services.AddTransient<IStartupFilter>(_ => new ServeClientFilesFilter(clientFolder)));
    }

    private sealed class ServeClientFilesFilter(string folder) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            var files = new PhysicalFileProvider(folder);

            app.Use(async (context, nextMiddleware) =>
            {
                var path = context.Request.Path;
                var isClientRoute = HttpMethods.IsGet(context.Request.Method)
                    && !Path.HasExtension(path.Value)
                    && !path.StartsWithSegments("/api")
                    && !path.StartsWithSegments("/openapi");

                if (isClientRoute)
                {
                    context.Request.Path = "/index.html";
                }

                await nextMiddleware();
            });

            app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ServeUnknownFileTypes = true });

            next(app);
        };
    }
}
