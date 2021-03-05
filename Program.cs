using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

/* see README.md for reasoning behind this code */
namespace blazorfun {
    public static class StringExtensions {
        public static string EscapeJsStringForSingleQuote(this string self) => self.Replace("'", "\\'");
    }
    public class HtmlElement {
        /// <summary>id is actually a number</summary>
        public string Id { get; private set; }

        public string tagName => Document.Js.Invoke<string>("evalDebug", $"window.getElementRefById('{Id}').tagName");
        public string textContent {
            get => Document.Js.Invoke<string>("evalDebug", $"window.getElementRefById('{Id}').textContent");
            set => Document.Js.Invoke<string>("evalDebug", $"window.getElementRefById('{Id}').textContent = '{value.EscapeJsStringForSingleQuote()}'");
        }

        public static HtmlElement FromId(string id) => new HtmlElement { Id = id };

        /// <summary>returns callbackId needed for removeEventListener</summary>
        public int addEventListener(string eventName, Action<object> callback, bool useCapture = false) {
            var cbId = Document.callbackId++;
            Document._callbacks.Add(cbId, callback);

            Document.Js.Invoke<string>("evalDebug", $@"window.getElementRefById('{Id}')
                .addEventListener(
                    '{eventName}', function (x) {{
                    DotNet.invokeMethodAsync('{nameof(blazorfun)}', '{nameof(Program.JsCallback)}', '{cbId}');
                }}, 
                {(useCapture ? "true" : "false")} 
            )");
            return cbId;
        }

        public void removeEventListener(int callbackId) {
            //assumption - I only care about webassembly references as browser will use GC to get rid of non referenced DOM elements
            Document._callbacks.Remove(callbackId);
        }

        public void appendChild(HtmlElement el) {
            Document.Js.InvokeVoid("evalDebug", $"window.getElementRefById('{Id}').appendChild(window.getElementRefById('{el.Id}'))");
        }
    }

    public static class Document {
        public static IJSInProcessRuntime Js;
        public static int callbackId = 1;

        public static IDictionary<int, Action<object>> _callbacks = new Dictionary<int, Action<object>>();

        public static object bodyAsObj => Js.Invoke<object>("evalDebug", "document.body");
        public static HtmlElement body => HtmlElement.FromId(Js.Invoke<string>("evalDebug", "document.body.id"));

        public static HtmlElement createElement(string name) {
            var nameStr = "'" + name.EscapeJsStringForSingleQuote() + "'";

            return HtmlElement.FromId(Js.Invoke<string>("evalDebug", $@"(function () {{
                var x = document.createElement({nameStr});
                x.id = window.generateNextId();
                
                window.elementsRef.set(x.id, x);

                return x.id;
            }})()"));
        }
    }

    public static class Window {
        public static void alert(string msg) => Document.Js.Invoke<object>("evalDebug", $"window.alert('{msg.EscapeJsStringForSingleQuote()}')");
    }

    public class Program {
        private static IJSInProcessRuntime _js;

        public static void Log(string msg) => _js.InvokeVoid("console.log", new object[] { msg });

        [JSInvokable]
        public static string JsCallback(string cbId) {
            if (Document._callbacks.TryGetValue(Convert.ToInt32(cbId), out var cb)) {
                Log($"webassembly JsCallback successfully called from JS with callbackId={cbId}");
                cb(null);
            } else {
                Log($"webassembly JsCallback failed as it got unknown callbackId={cbId}");
            }

            return "";
        }

        private static int _onClickedCallbackId;

        public static async Task Main(string[] args) {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            var host = builder.Build();
            
            _js = (IJSInProcessRuntime)host.Services.GetRequiredService<IJSRuntime>();
            Document.Js = _js;
            Log("Log message originating from dotnet");

            await Task.Run(async () => {
                Log($"body has id={Document.body.Id} and tagname is {Document.body.tagName}");
                
                var body = Document.bodyAsObj;
                Log($"is body a-not-null-object={body != null} toString={body}");
                Log("now passing body back to JavaScript asking it to retrieve 'id' property");

                var gotId = false;
                try {
                    var idVal = Document.Js.Invoke<string>("evalDebug", "arguments[0].id", body);
                    if (idVal != null) {
                        Log($"success: can use bodyAsObj as opaque reference to HTMLElement. Got body.id value={idVal}");
                    } else {
                        Log("failed: can not use bodyAsObj as opaque reference to HTMLElement because JS gets something that is not an original reference having id property");
                    }

                } catch (Exception ex) {
                    Log($"fail: can not use bodyAsObj as reference. Got exception={ex}");
                }
                
                var y = Document.createElement("div");
                y.textContent = "click me";
                var cnt = 0;
                _onClickedCallbackId = y.addEventListener("click", x => {
                    cnt++;
                    Log($"'click me' clicked {cnt} times");
                    
                    if (cnt <= 5) {
                        y.textContent = $"clicked {cnt} times. click me again";
                    } else {
                        y.textContent = "click to show alert";
                        Window.alert("hi there");
                    }
                });
                Document.body.appendChild(y);
            });

            await host.RunAsync();
        }
    }
}
