using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SilentActivation.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Simulates a service whose construction fails only on the WebAssembly runtime
// (e.g. a type initializer that dies with a Mono TypeLoadException). The server
// project registers a working implementation, so prerendering and Server
// interactivity are fine — only WASM activation of a component injecting this
// service faults.
builder.Services.AddSingleton<IGreetingService>(_ =>
    throw new InvalidOperationException(
        "Simulated: service construction fails on the WebAssembly runtime."));

// Proof that the exception exists but is discarded by the framework: this is the
// ONLY way to observe it. Nothing else logs it — not the browser console, not
// window.onerror, not Blazor's error UI.
AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
    Console.Error.WriteLine($"[FirstChance] {e.Exception.GetType().Name}: {e.Exception.Message}");

await builder.Build().RunAsync();
