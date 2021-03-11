using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

/*
 * see README.md for reasoning behind this code
 */
namespace blazorfun {
    public class Program {
        private HtmlElement _clickableDiv;
        private int _clickCount = 0;

        private void ActualProgramCode() {
            _clickableDiv = Document.CreateElement("div");
            _clickableDiv.TextContent = "click me";
            _clickableDiv.AddEventListener("click", OnClickableDivClicked);

            Document.Body.AppendChild(_clickableDiv);
        }
        
        private void OnClickableDivClicked(object ev) {
            _clickCount++;
            Console.Log($"'click me' clicked {_clickCount} times. Param={ev}");

            if (_clickCount < 5) {
                _clickableDiv.TextContent = $"clicked {_clickCount} times. click me again";
            } else if (_clickCount < 6) {
                _clickableDiv.TextContent = "click to show farewell alert";
            } else {
                Window.Alert("cya");
                _clickableDiv.TextContent = "not clickable anymore";
                _clickableDiv.RemoveEventListener("click", OnClickableDivClicked);
            }
        }

        public static async Task Main(string[] args) {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            
            builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            var host = builder.Build();
            
            Document.Js = (IJSInProcessRuntime)host.Services.GetRequiredService<IJSRuntime>();
            Console.Log("Log message originating from dotnet");

            await Task.Run(() => new Program().ActualProgramCode());

            await host.RunAsync();
        }
    }
}
