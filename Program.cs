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
 *
 * naming convention is the same as in javascript - TBC
 */

namespace blazorfun {
    public class EventHandlerInfo {
        public string eventName;
        public bool useCapture;
        public IJSObjectReference nativeFunc;
        public Action<object> dotnetCallback;
    }

    public class CallbackFromJs {
        private static IDictionary<int, EventHandlerInfo> _idToCallback = new Dictionary<int, EventHandlerInfo>();
        private static IDictionary<object, List<int>> _requestorToCallbackIds = new Dictionary<object, List<int>>();

        private static int _callbackId = 1;

        public static (int callbackId, EventHandlerInfo) RegisterReturningId(IJSObjectReference requestor, Action<object> callback) {
            var cbId = _callbackId++;

            var cbInfo = new EventHandlerInfo { dotnetCallback = callback};

            _idToCallback.Add(cbId, cbInfo);
            
            List<int> ids;
            if (!_requestorToCallbackIds.TryGetValue(requestor, out ids)) {
                ids = new List<int>();
                _requestorToCallbackIds.Add(requestor, ids);
            }
            ids.Add(cbId);
            
            return (cbId, cbInfo);
        }
        
        [JSInvokable]
        public static string JsCallback(string rawCbId, object prm) {
            var cbId = Convert.ToInt32(rawCbId);

            if (_idToCallback.TryGetValue(cbId, out var cb)) {
                Console.Log($"webassembly JsCallback successfully called from JS with callbackId={cbId} with parameter={prm}");
                cb.dotnetCallback(prm);
            } else {
                Console.Log($"webassembly JsCallback failed as it got unknown callbackId={cbId}");
            }

            return "";
        }
        
        public static void UnregisterAllEventListeners(IJSObjectReference requestor) {
            if (_requestorToCallbackIds.TryGetValue(requestor, out var ids)) {
                _requestorToCallbackIds.Remove(requestor);

                ids.ForEach(cbId => _idToCallback.Remove(cbId));
            }

            //TODO clear JavaScript side (despite GC?)
        }

        public static void UnregisterEventListener(IJSObjectReference requestor, string eventName, Action<object> callback, bool useCapture) {
            List<int> callbackIds;

            if (!_requestorToCallbackIds.TryGetValue(requestor, out callbackIds)) {
                throw new Exception("no events registered for given requestor");
            }

            var copy = callbackIds.ToList();

            foreach (var cbId in copy) {
                if (_idToCallback.TryGetValue(cbId, out var ehi)) {
                    Console.Log($"is the same callback? {ehi.eventName} =?= {eventName};  {ehi.useCapture} =?= {useCapture};  {ehi.dotnetCallback} =?= {callback}");

                    if (ehi.eventName != eventName || ehi.useCapture != useCapture || ehi.dotnetCallback != callback) {
                        Console.Log("not the same callback");
                        continue;
                    }
                    
                    callbackIds.Remove(cbId);
                    _idToCallback.Remove(cbId);
                    
                    Document.Js.InvokeVoid("evalDebug", $@"arguments[0].removeEventListener(
                        '{eventName}', 
                        arguments[1].wrapped, 
                        {(useCapture ? "true" : "false")});", requestor, ehi.nativeFunc);
                    return;
                }
            }

            throw new Exception("requestor doesn't have such callback registered when looking by (eventType, func, useCapture)");
        }

        public static void RegisterEventListener(IJSObjectReference requestor, string eventName, Action<object> callback, bool useCapture) {
            var (cbId,cbInfo) = RegisterReturningId(requestor, callback);

            /*
             NOTE: need to wrap function with a dummy object to avoid:
                 blazor.webassembly.js:1 Microsoft.JSInterop.JSException: Cannot create a JSObjectReference from the value 'function (ev) { DotNet.invokeMethodAsync('blazorfun', 'JsCallback', '1', ev); }'.
                 blazor.webassembly.js:1 Error: Cannot create a JSObjectReference from the value 'function (ev) { DotNet.invokeMethodAsync('blazorfun', 'JsCallback', '1', ev); }'.
             */

            var nativeCallback = Document.Js.Invoke<IJSObjectReference>("evalDebug",
                $@"(function() {{
                    let func = function (ev) {{ DotNet.invokeMethodAsync('{nameof(blazorfun)}', '{nameof(CallbackFromJs.JsCallback)}', '{cbId}', ev); }};
                    
                    arguments[0].addEventListener(
                        '{eventName}', 
                        func, 
                        {(useCapture ? "true" : "false")});

                    return {{'wrapped' : func}};
                }}).apply(null, arguments)",
                requestor);

            cbInfo.eventName = eventName;
            cbInfo.useCapture = useCapture;
            cbInfo.nativeFunc = nativeCallback;
        }
    }

    public class HtmlElement : IDisposable {
        public IJSObjectReference Native { get; private set; }
        public string Id => Document.Js.Invoke<string>("evalDebug", "arguments[0].id", Native);
        
        public string TagName => Document.Js.Invoke<string>("evalDebug", "arguments[0].tagName", Native);
        public string TextContent {
            get => Document.Js.Invoke<string>("evalDebug", "arguments[0].textContent", Native);
            set => Document.Js.Invoke<string>("evalDebug", "arguments[0].textContent = arguments[1]", Native, value);
        }

        public static HtmlElement FromNative(IJSObjectReference native) => new() { Native = native };
        
        public void AddEventListener(string eventName, Action<object> callback, bool useCapture = false) =>
            CallbackFromJs.RegisterEventListener(Native, eventName, callback, useCapture);

        public void RemoveEventListener(string eventName, Action<object> callback, bool useCapture = false) =>
            CallbackFromJs.UnregisterEventListener(Native, eventName, callback, useCapture);

        public void AppendChild(HtmlElement el) =>
            Document.Js.InvokeVoid("evalDebug", 
                "arguments[0].appendChild(arguments[1])", Native, el.Native);
        
        public void RemoveChild(HtmlElement el) =>
            Document.Js.InvokeVoid("evalDebug",
                "arguments[0].removeChild(arguments[1])", Native, el.Native);

        public void Dispose() => CallbackFromJs.UnregisterAllEventListeners(Native);
    }

    public static class Document {
        public static IJSInProcessRuntime Js;
      
        public static HtmlElement Body => HtmlElement.FromNative(Js.Invoke<IJSObjectReference>("evalDebug", "document.body"));

        public static HtmlElement CreateElement(string name) {
            return HtmlElement.FromNative(Js.Invoke<IJSObjectReference>("evalDebug", 
                "document.createElement(arguments[0])", 
                name));
        }
    }

    public static class Console {
        public static void Log(string msg) => Document.Js.InvokeVoid("console.log", new object[] { msg });
    }

    public class Window {
        public static void Alert(string msg) => Document.Js.Invoke<object>("evalDebug", "window.alert(arguments[0])", msg);
    }
    
    public class Program {
        private readonly HtmlElement _clickableDiv;
        private int _clickCount = 0;

        private Program() {
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
            
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            var host = builder.Build();
            
            Document.Js = (IJSInProcessRuntime)host.Services.GetRequiredService<IJSRuntime>();
            Console.Log("Log message originating from dotnet");

            await Task.Run(async () => {
                new Program();
            });

            await host.RunAsync();
        }
    }
}
