# Swagger UI

Swagger UI is available at `<Deployment:BaseUrl>/swagger/index.html` in every
environment. It documents the two API controllers:

- `POST /api/mes/medically-frail` (MedicallyFrailController)
- `POST /api/nvh/verification-request` (ClientInfoController)

Expand an operation, select **Try it out**, edit the JSON request body, and select
**Execute**. Requests use `Deployment:BaseUrl` from the active environment's
appsettings file, not the hostname used to open Swagger UI.

| ASPNETCORE_ENVIRONMENT | Swagger UI | Request server |
| --- | --- | --- |
| Development | https://vaedws-dev.optum.com/swagger/index.html | https://vaedws-dev.optum.com |
| SIT | https://vaedws-dev.optum.com/SIT/swagger/index.html | https://vaedws-dev.optum.com/SIT |
| UAT | https://vaedws-stage.optum.com/swagger/index.html | https://vaedws-stage.optum.com |
| Production | https://vaedws.optum.com/swagger/index.html | https://vaedws.optum.com |

`WebApplication.CreateBuilder` loads `appsettings.json` followed by
`appsettings.{ASPNETCORE_ENVIRONMENT}.json`. The URL can also be overridden using
the `Deployment__BaseUrl` environment variable. A missing or invalid absolute
HTTP(S) base URL prevents startup; there is no localhost fallback. Keep any
application path (such as `/SIT`) in the base URL, but do not include `/swagger`,
the API route, a query string, or a fragment.

The OpenAPI document is available at `<Deployment:BaseUrl>/swagger/v1/swagger.json`.
Swagger UI uses a relative document URL so deployments under IIS application
paths work without removing their path prefix. The status endpoint remains
excluded from the document.

When running locally, open the local application's `/swagger/index.html`. The
**Servers** selector and generated requests still point to the configured remote
environment. Browsers enforce CORS for cross-origin requests; use Swagger UI on
the deployed environment's own URL for same-origin requests rather than enabling
permissive CORS locally.

**Execute sends real requests.** ClientInfo writes to the configured database,
and MedicallyFrail also records requests and responses. Use approved test data
and the appropriate environment. Swagger does not bypass any proxy, network, or
certificate requirements; protect access to the documentation through the
deployment's existing access controls, especially in Production.
