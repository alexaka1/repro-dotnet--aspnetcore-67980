# Draft for dotnet/aspnetcore (🐞 Bug Report form)

**Title:** Blazor Web: exception during WASM root component activation is silently discarded — InteractiveAuto page stays a dead prerender with no error and no Server fallback

**Is there an existing issue for this?** → tick "I have searched the existing issues"
(search notes are under "Anything else?" below)

## Describe the bug

`WebAssemblyRenderer.OnUpdateRootComponents` fire-and-forgets the root component activation tasks:

https://github.com/dotnet/aspnetcore/blob/release/10.0/src/Components/WebAssembly/WebAssembly/src/Rendering/WebAssemblyRenderer.cs#L72-L84

```csharp
case RootComponentOperationType.Add:
    _ = webRootComponentManager.AddRootComponentAsync(...);
case RootComponentOperationType.Update:
    _ = webRootComponentManager.UpdateRootComponentAsync(...);
```

If activation throws (component constructor, DI resolution, a type-initializer failure in a
dependency that only fails under Mono WASM, …), the fault is completely swallowed:

- nothing in the browser console (no unhandled rejection, no `Blazor` error),
- no `blazor-error-ui`,
- no `ILogger` output,
- and for `InteractiveAuto`, **no fallback to Server**, even though the Server registration of the
  same page works (prerendering proves it).

The user is left on a permanently dead prerender. Worse: once Auto has committed to WebAssembly
(runtime cached), even a **full page reload** of the affected page re-attempts WASM activation and
dies the same way — there is no user-side recovery short of clearing the cache.

**Real-world impact:** we hit this in production when a `TypeInitializationException` was thrown
while constructing a ctor-injected DI service during activation of an `InteractiveAuto` page —
but any exception thrown during activation triggers it; the specific trigger is irrelevant.
Because the fault is discarded, diagnosing the underlying application-side bug took a multi-day
investigation across CI bisects and Playwright traces, and ultimately required attaching an
`AppDomain.FirstChanceException` handler in the WASM client — that handler is the only place the
exception is observable. With ordinary error surfacing, the root cause would have been visible in
the first failing run's browser console.

## Expected Behavior

Some combination of:

1. The activation fault is logged (console + `ILogger`) and surfaces `blazor-error-ui`, like
   unhandled exceptions elsewhere in Blazor.
2. For `InteractiveAuto`, a failed WebAssembly activation falls back to the Server render mode
   instead of leaving a dead prerender (the server was able to render the page seconds earlier).

## Steps To Reproduce

Minimal repro (unmodified `dotnet new blazor -int Auto` template plus only the changes listed in
its README): https://github.com/alexaka1/repro-dotnet--aspnetcore-wasm-silent-activation

A service is registered working on the server and with a throwing factory in the WASM client:

```csharp
// Client Program.cs — simulates a dependency that only fails on the WASM runtime
builder.Services.AddSingleton<IGreetingService>(_ =>
    throw new InvalidOperationException("Simulated: service construction fails on the WebAssembly runtime."));
```

1. `dotnet run`, open `/counter` — first interactive visit renders Interactive **Server**.
2. Navigate Home ↔ Counter until Counter reports **WebAssembly** (Auto committed to WASM).
3. Click the nav link to `/crash` (enhanced navigation), a page that injects the service.
4. Observe: page reports `RendererInfo: Static, IsInteractive: false` forever; button does
   nothing; browser console and server logs are clean. A `FirstChanceException` hook is the only
   evidence of the `InvalidOperationException`.
5. Full reload of `/crash`: same dead result (Auto stays on WASM once cached).

## Exceptions (if any)

Only observable via `AppDomain.CurrentDomain.FirstChanceException`:
`System.InvalidOperationException: Simulated: service construction fails on the WebAssembly runtime.`

## .NET Version

10.0.302

## Anything else?

- ASP.NET Core version: 10.0.10 (behavior also reproduced on 10.0.9).
- Existing-issue search: `AddRootComponentAsync`, `OnUpdateRootComponents`, "auto mode fallback",
  "interactive auto stuck static" — closest is #64508 ([Blazor] Unify root components
  initialization paths), which covers the divergent init paths but not the swallowed activation
  fault or the missing fallback.
- The same silent-discard exists for the `Update` operation.
- Happy to contribute a fix if you can indicate the preferred direction (await + log + error UI,
  vs. propagating the fault to enable Auto fallback).
