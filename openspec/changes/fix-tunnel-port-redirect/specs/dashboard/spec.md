## ADDED Requirements

### Requirement: Configured tunnel listener is exempt from the HTTPS redirect
The HTTPS redirect SHALL NOT apply to the tunnel listener, whose port SHALL be read from `Kestrel:Endpoints:TunnelHttp:Url` and default to 5002 when not configured. No other listener SHALL be exempt.

#### Scenario: Development tunnel port serves content over HTTP
- **GIVEN** `Kestrel:Endpoints:TunnelHttp:Url` is `http://127.0.0.1:5102`
- **WHEN** a request arrives on local port 5102
- **THEN** the response is served without a redirect to HTTPS

#### Scenario: Public HTTP listener still redirects
- **GIVEN** the tunnel listener is on port 5002
- **WHEN** a request arrives on local port 5000
- **THEN** the response is a redirect to the equivalent `https://` URL, with no page content served

#### Scenario: Tunnel port not configured
- **GIVEN** `Kestrel:Endpoints:TunnelHttp:Url` is missing or malformed
- **WHEN** a request arrives on local port 5002
- **THEN** the request is served without a redirect, as before this change
