namespace SilentActivation.Client;

/// <summary>
/// Stands in for any service whose construction succeeds on the server but
/// throws on the WebAssembly runtime — a throwing DI factory, a type-initializer
/// failure, a trimming-induced load failure, or any other exception raised while
/// the root component is activated.
/// </summary>
public interface IGreetingService
{
    string Greet();
}

public sealed class GreetingService : IGreetingService
{
    public string Greet() => "Hello from a successfully constructed service.";
}
