## Why

The HTTPS redirect in `Program.cs` skips the loopback tunnel listener only when the local port is 5002, a value hard-coded in the condition. The tunnel port is configured elsewhere: `appsettings.json` uses `TunnelHttp` on 5002, but the Development profile in `.vscode/launch.json` sets `Kestrel__Endpoints__TunnelHttp__Url` to 5102. In that profile, requests that reach the tunnel listener on 5102 are redirected to `https://localhost:5101`, which breaks port-forwarding tools (VS Code Dev Tunnels, Codespaces) that proxy to the local HTTP port. This was confirmed by running the app: `http://127.0.0.1:5102/` returns 307 to `https://127.0.0.1:5101/`.

## What Changes

- The tunnel listener port used by the HTTPS redirect exemption is read from configuration (`Kestrel:Endpoints:TunnelHttp:Url`) instead of a hard-coded 5002.
- If the tunnel listener is not configured, the exemption falls back to port 5002 so the production behavior does not change.
- The redirect exemption logic is extracted into a small, unit-tested type (`TunnelPortPolicy`) so it can be tested without starting the web host.
- The comment in `Program.cs` describing the tunnel listener is updated to match.

## Non-goals

- No change to which ports serve data: the public HTTP listener (5000) still only redirects, and the HTTPS listener is unchanged.
- No change to `appsettings.json` values or to the Development profile ports.
- No change to certificates, authentication, or the single-instance mutex.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `dashboard`: the HTTPS-only requirement gains a scenario stating that the configured tunnel listener is exempt from the redirect, and only that listener.

## Impact

- **Code**: new `src/CambioDeDomicilio/Configuration/TunnelPortPolicy.cs`; `src/CambioDeDomicilio/Program.cs` reads the tunnel port from configuration and passes it to the policy.
- **Tests**: new `tests/CambioDeDomicilio.Tests/Configuration/TunnelPortPolicyTests.cs`.
- **Production**: none. With the production `appsettings.json` (5002) the behavior is identical.
- **Development**: the tunnel listener on 5102 serves over plain HTTP without redirecting, as the Development profile intends.
