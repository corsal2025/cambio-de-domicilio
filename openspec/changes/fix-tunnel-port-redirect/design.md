## Context

`Program.cs` registers the HTTPS redirect with:

```csharp
app.UseWhen(
    context => context.Connection.LocalPort != 5002,
    branch => branch.UseHttpsRedirection());
```

The intent (per the comment above it) is that the tunnel listener, bound to loopback, is the only listener allowed to serve content over plain HTTP. The port is a configuration value (`Kestrel:Endpoints:TunnelHttp:Url`), and the code must read that value rather than repeat it.

## Decision

Add `TunnelPortPolicy` in `Configuration/`:

- `static int ResolveTunnelPort(string? tunnelHttpUrl)`: parses the port from the configured URL. When the value is null, empty, or not a valid absolute URL, returns the default `5002`.
- `static bool ShouldRedirectToHttps(int localPort, int tunnelPort)`: returns `false` only when `localPort == tunnelPort`.

`Program.cs` computes the port once from `builder.Configuration["Kestrel:Endpoints:TunnelHttp:Url"]` and uses `ShouldRedirectToHttps` inside the `UseWhen` predicate.

## Alternatives considered

- **Read the port from `IConfiguration` inside the predicate.** Works, but keeps the logic untestable without a host. Rejected.
- **Use an environment-specific constant.** Duplicates configuration and drifts again. Rejected.

## Risks

- A malformed `TunnelHttp` URL falls back to 5002 silently. This matches the production default and avoids aborting startup for an optional listener. The startup log does not report it; acceptable because the value is loopback-only.
- If Kestrel is configured with several HTTP listeners, only the tunnel port is exempt, so this does not widen exposure.
