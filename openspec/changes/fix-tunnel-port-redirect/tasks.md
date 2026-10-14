## 1. Tunnel port resolution (tests first)

- [x] 1.1 Write failing tests in `TunnelPortPolicyTests` for `ResolveTunnelPort`: returns 5102 for `http://127.0.0.1:5102`, returns 5002 for `http://127.0.0.1:5002`, returns 5002 for null, empty, and malformed input.
- [x] 1.2 Implement `ResolveTunnelPort` in `src/CambioDeDomicilio/Configuration/TunnelPortPolicy.cs` so the tests from 1.1 pass.

## 2. Redirect exemption (tests first)

- [x] 2.1 Write failing tests for `ShouldRedirectToHttps`: returns false when the local port equals the tunnel port (5102/5102), returns true for the public HTTP port (5000) and for the HTTPS port (5101), and returns true for any other port.
- [x] 2.2 Implement `ShouldRedirectToHttps` so the tests from 2.1 pass.

## 3. Wire into Program.cs

- [x] 3.1 Replace the hard-coded `5002` in `Program.cs` with `TunnelPortPolicy.ResolveTunnelPort(builder.Configuration["Kestrel:Endpoints:TunnelHttp:Url"])` and `TunnelPortPolicy.ShouldRedirectToHttps` in the `UseWhen` predicate.
- [x] 3.2 Update the comment above the `UseWhen` block to describe the configured tunnel port instead of 5002.

## 4. Verification

- [x] 4.1 Run `dotnet build -c Release` and `dotnet test -c Release` and confirm all tests pass.
- [x] 4.2 Run the app with the Development tunnel configuration (`Kestrel__Endpoints__TunnelHttp__Url=http://127.0.0.1:5102`) and confirm `http://127.0.0.1:5102/` returns 200 with the dashboard, not 307.
- [x] 4.3 Run the app with the production configuration (tunnel on 5002) and confirm `http://127.0.0.1:5000/` still redirects to HTTPS, and `http://127.0.0.1:5002/` serves content.
