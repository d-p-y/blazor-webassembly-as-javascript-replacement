# Blazor WebAssembly without components

## Goal
Check feasibility of using Blazor WebAssembly as replacement of TypeScript or [Bridge.NET](https://github.com/bridgedotnet/Bridge/) to interact directly with DOM. In other words: try to leverage Blazor WebAssembly without hosted components model, [life cycle events resembling classic aspnet](https://blazor-university.com/javascript-interop/calling-javascript-from-dotnet/passing-html-element-references/), routing, DI etc.

### Outcome as of 2020-01 
WebAssembly cannot reference DOM objects or references to javascript functions. Because of this, hacks (such as Document._callbacks as seen in Program.cs) are currently needed. Because of this routing+components model of Blazor WebAssembly is at this stage not only a convenience but is a necessity. References to non primitive objects are stored within `window` object so that WebAssembly can find it back later by using some primitive key (such as string). Existence of such collections mean that there needs to be some explicit cleanup process to get rid of not-used-anymore references. Blazor WebAssembly's routing+components works around this problem by introducing a lot of abstractions and [discouraging to try to access DOM directly](https://github.com/dotnet/aspnetcore/issues/15830).

### Non-goal observations

Dotnet compiler as such is impressive - it is compiling quickly (especially when using `<BlazorLinkOnBuild>false</BlazorLinkOnBuild>` in csproj). Interop methods are simple yet powerful. 

### JS-DotNet interop links

https://chrissainty.com/using-javascript-interop-in-razor-components-and-blazor/

https://www.c-sharpcorner.com/article/understand-javascript-interop-in-blazor/
