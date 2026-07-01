# Certificate-Based Authentication Configuration

This API uses Certificate Forwarding Middleware to authenticate clients via TLS certificates passed through HTTP headers from a reverse proxy.

## Configuration

### Middleware Setup

The application is configured to:

1. **Certificate Forwarding** - Extracts client certificates from the `X-Client-Cert` header
2. **Certificate Authentication** - Validates the certificate and creates authenticated claims
3. **Authorization** - Requires valid certificates for protected endpoints

### Headers Expected

The reverse proxy should forward the following headers:

- `X-Client-Cert`: Base64-encoded client certificate (with or without PEM markers)
- `X-Client-Subject-DN`: Subject Distinguished Name (optional, for logging)

### Configuration Files

#### appsettings.json (Production)
```json
{
  "CertificateAuthentication": {
    "CertificateHeader": "X-Client-Cert",
    "SubjectDNHeader": "X-Client-Subject-DN",
    "AllowSelfSigned": false,
    "RevocationMode": "Online"
  }
}
```

#### appsettings.Development.json (Development)
```json
{
  "CertificateAuthentication": {
    "AllowSelfSigned": true,
    "RevocationMode": "NoCheck"
  }
}
```

## Protected Endpoints

### POST /api/mes/medically-frail

This endpoint requires certificate authentication via the `[Authorize]` attribute.

**Request:**
```http
POST /api/mes/medically-frail HTTP/1.1
Host: localhost:5001
X-Client-Cert: -----BEGIN CERTIFICATE-----MIIDdTCCAl2gAwIBAgI...-----END CERTIFICATE-----
X-Client-Subject-DN: CN=client.example.com, OU=Dev, O=Company, L=Seattle, ST=WA, C=US
Content-Type: application/json

{
  "requestId": "REQ-2026-000001",
  "mmisEnrolleeId": "1234567890"
}
```

**Response:**
```json
{
  "request": "REQ-2026-000001",
  "medicallyFrail": true,
  "circumstanceStartDate": "2026-03-01",
  "circumstanceEndDate": null,
  "circumstances": [
    "Pregnancy",
    "Disabling Mental Disorder"
  ]
}
```

## Authentication Flow

1. Reverse proxy terminates TLS and extracts client certificate
2. Proxy forwards certificate in `X-Client-Cert` header (Base64-encoded)
3. Certificate Forwarding Middleware converts header to X509Certificate2 object
4. Certificate Authentication validates the certificate
5. Claims are created from certificate (Subject, Thumbprint)
6. Controller accesses authenticated user via `User.Claims`

## Testing

### Without Authentication (Will Fail)
```bash
curl -X POST https://localhost:5001/api/mes/medically-frail \
  -H "Content-Type: application/json" \
  -d '{"requestId": "REQ-001", "mmisEnrolleeId": "1234567890"}'
```
**Expected:** 401 Unauthorized

### With Valid Certificate Header
```bash
curl -X POST https://localhost:5001/api/mes/medically-frail \
  -H "Content-Type: application/json" \
  -H "X-Client-Cert: <base64-encoded-certificate>" \
  -H "X-Client-Subject-DN: CN=test-client" \
  -d '{"requestId": "REQ-001", "mmisEnrolleeId": "1234567890"}'
```
**Expected:** 200 OK with response payload

## Logging

The controller logs:
- Request ID and MMIS Enrollee ID
- Authenticated certificate subject and thumbprint
- Subject DN from header (if provided)

Example log output:
```
info: MES_EDWS.Controllers.MedicallyFrailController[0]
      Received medically frail request. RequestId: REQ-2026-000001, MmisEnrolleeId: 1234567890
info: MES_EDWS.Controllers.MedicallyFrailController[0]
      Authenticated client certificate - Subject: CN=client.example.com, Thumbprint: A1B2C3D4...
info: MES_EDWS.Controllers.MedicallyFrailController[0]
      Client Subject DN from header: CN=client.example.com, OU=Dev, O=Company, L=Seattle, ST=WA, C=US
```

## Production Considerations

1. **Revocation Checking**: Enable `RevocationMode: "Online"` in production
2. **Certificate Validation**: Add custom validation in `OnCertificateValidated` event:
   - Validate issuer
   - Check allowed certificate thumbprints
   - Verify certificate expiration
   - Check certificate purpose/EKU
3. **Reverse Proxy Configuration**: Ensure proxy is configured to:
   - Require client certificates
   - Forward certificate in correct format
   - Set appropriate headers
4. **Security**: Only accept certificate headers from trusted proxy sources

## Additional Security

To restrict access to specific certificates, modify the `OnCertificateValidated` event in `Program.cs`:

```csharp
OnCertificateValidated = context =>
{
    // Example: Only allow specific thumbprints
    var allowedThumbprints = new[] { "THUMBPRINT1", "THUMBPRINT2" };
    
    if (!allowedThumbprints.Contains(context.ClientCertificate.Thumbprint))
    {
        context.Fail("Certificate not authorized");
        return Task.CompletedTask;
    }
    
    // Create claims and succeed
    // ...
}
```
