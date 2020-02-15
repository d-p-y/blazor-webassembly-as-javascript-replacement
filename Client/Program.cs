using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Blazor.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace blazorfun.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            //builder.RootComponents.Add<App>("app");
            
            var host = builder.Build();

            await Task.Run(async () =>
            {
                var js = host.Services.GetRequiredService<IJSRuntime>();
                await js.InvokeVoidAsync("console.log", new object[] {"!@#!@#!@#!@#"});
            });

            await host.RunAsync();
        }
    }
}
