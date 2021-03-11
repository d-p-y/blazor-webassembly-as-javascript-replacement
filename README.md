# Blazor WebAssembly without components

## Goal
Check feasibility of using Blazor WebAssembly as replacement of TypeScript or [Bridge.NET](https://github.com/bridgedotnet/Bridge/) to interact directly with DOM. In other words: try to leverage Blazor WebAssembly without hosted components model, [life cycle events resembling classic aspnet](https://blazor-university.com/javascript-interop/calling-javascript-from-dotnet/passing-html-element-references/), routing, DI etc.

### Outcome as of 2021-03-05 afternoon

Thanks to [help of aspnet maintainers](https://github.com/dotnet/aspnetcore/issues/30687) I was able to make passing JS references work. Good! It seems that biggest pain is gone. Now it looks workable. Still maybe somewhat annoying is to have to create JS stubs for callbacks-to-dotnet but that is how WASM's access to 'outside world' works nowadays.

### Outcome as of 2021-03-05 morning

It seems that JavaScript reference is passed to dotnet. Such reference, by design, is supposed to be opaque - WASM may not get its properties or invoke it in any way. All I can tell, is that `document.body` that I pass from JavaScript to C#/mono/WASM is not null. Afterwards I pass it back to JavaScript hoping that in JavaScript it will be indistinguishable from `document.body`. For sake of test I just try to get `id` property. What I actually see passed to JavaScript in my test is some sort of `Object` that definitively is not a document.body. It resembles simplest object constructed by calling `eval('{}')`

Chrome console output:

```
dom is ready
(index):49 document.body.id is 1
blazor.webassembly.js:1 Debugging hotkey: Shift+Alt+D (when application has focus)
blazor.webassembly.js:1 blazor Loaded 8.62 MB resourcesThis application was built with linking (tree shaking) disabled. Published applications will be significantly smaller.
blazor.webassembly.js:1 Log message originating from dotnet
2(index):15 constructing function with code=[return document.body.id] args=[undefined;undefined;undefined]
(index):15 constructing function with code=[return window.getElementRefById('1').tagName] args=[undefined;undefined;undefined]
blazor.webassembly.js:1 body has id=1 and tagname is BODY
(index):15 constructing function with code=[return document.body] args=[undefined;undefined;undefined]
blazor.webassembly.js:1 is body a-not-null-object=True toString={}
blazor.webassembly.js:1 now passing body back to JavaScript asking it to retrieve 'id' property
(index):15 constructing function with code=[return arguments[0].id] args=[[object Object];undefined;undefined]
blazor.webassembly.js:1 failed: can not use bodyAsObj as opaque reference to HTMLElement because JS gets something that is not an original reference having id property
```

If test _would_ succeed I would expect last line to be 
```
blazor.webassembly.js:1 success: can use bodyAsObj as opaque reference to HTMLElement. Got body.id value=1
```

### Outcome as of 2020-10 

Firefox and Chrome now both support [reference types spec](https://github.com/WebAssembly/reference-types). Still latest Blazor Webassembly doesn't seem to use it.

### Outcome as of 2020-01 
WebAssembly cannot reference DOM objects or references to javascript functions. Because of this, hacks (such as Document._callbacks as seen in Program.cs) are currently needed. Because of this routing+components model of Blazor WebAssembly is at this stage not only a convenience but is a necessity. References to non primitive objects are stored within `window` object so that WebAssembly can find it back later by using some primitive key (such as string). Existence of such collections mean that there needs to be some explicit cleanup process to get rid of not-used-anymore references. Blazor WebAssembly's routing+components works around this problem by introducing a lot of abstractions and [discouraging to try to access DOM directly](https://github.com/dotnet/aspnetcore/issues/15830).

### Non-goal observations

Dotnet compiler as such is impressive - it is compiling quickly (especially when using `<BlazorLinkOnBuild>false</BlazorLinkOnBuild>` in csproj). Interop methods are simple yet powerful. 

### JS-DotNet interop links

https://chrissainty.com/using-javascript-interop-in-razor-components-and-blazor/

https://www.c-sharpcorner.com/article/understand-javascript-interop-in-blazor/
