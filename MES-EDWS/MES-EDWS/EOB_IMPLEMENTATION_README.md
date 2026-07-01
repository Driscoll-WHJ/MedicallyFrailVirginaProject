# EOB (Explanation of Benefits) Implementation

This document describes the implementation of the Patient Access EOB data model and API endpoints.

## Overview

The EOB implementation provides access to Explanation of Benefits data from the Oracle database view `KN1APIViews.PATIENT_ACCESS_EOB`. The implementation includes:

- **Data Model**: `PatientAccessEob.cs` with 64 mapped columns
- **Database Context**: `MesEdwsDbContext.cs` using Entity Framework Core
- **Service Layer**: `IEobService` and `EobService` for business logic
- **API Controller**: `EobController` with multiple query endpoints
- **Authentication**: Certificate-based authentication required for all endpoints

## Database Schema

### Source
- **Schema**: `KN1APIViews`
- **Table/View**: `PATIENT_ACCESS_EOB`
- **Primary Key**: `IDENTIFIER`

### Column Mapping

The `PatientAccessEob` model includes all 64 columns from the database view:

| Database Column | C# Property | Type |
|----------------|-------------|------|
| IDENTIFIER | Identifier | string |
| META_LAST_UPDATED | MetaLastUpdated | DateTime? |
| CLM_TYPE | ClmType | string? |
| PATIENT | Patient | string? |
| RELATED_RELATIONSHIP_CODE_1 | RelatedRelationshipCode1 | string? |
| RELATED_RELATIONSHIP_CODE_2 | RelatedRelationshipCode2 | string? |
| RELATED_RELATIONSHIP_DISPLAY | RelatedRelationshipDisplay | string? |
| ITEM_SEQUENCE | ItemSequence | int? |
| TYPE_CODE | TypeCode | string? |
| TYPE_DISPLAY | TypeDisplay | string? |
| SUB_TYPE_DISPLAY | SubTypeDisplay | string? |
| BILLABLE_PERIOD_START | BillablePeriodStart | DateTime? |
| BILLABLE_PERIOD_END | BillablePeriodEnd | DateTime? |
| CREATED | Created | DateTime? |
| PROVIDER | Provider | string? |
| CARE_TEAM_PROVIDER | CareTeamProvider | string? |
| CARE_TEAM_ROLE | CareTeamRole | string? |
| CARE_TEAM_QUALIFICATION_1 | CareTeamQualification1 | string? |
| CARE_TEAM_QUALIFICATION_2 | CareTeamQualification2 | string? |
| PRIMARY_DIAGNOSIS_CODE | PrimaryDiagnosisCode | string? |
| SECONDARY_DIAGNOSIS_CODE | SecondaryDiagnosisCode | string? |
| ADMIT_DIAGNOSIS_CODE | AdmitDiagnosisCode | string? |
| DIAGNOSIS_ON_ADMISSION | DiagnosisOnAdmission | string? |
| PRIMARY_DIAGNOSIS_DISPLAY | PrimaryDiagnosisDisplay | string? |
| SECONDARY_DIAGNOSIS_DISPLAY | SecondaryDiagnosisDisplay | string? |
| ADMIT_DIAGNOSIS_DISPLAY | AdmitDiagnosisDisplay | string? |
| ITEM_PRODUCTORSERVICE_CODE_1 | ItemProductOrServiceCode1 | string? |
| ITEM_PRODUCTORSERVICE_DISPLAY_1 | ItemProductOrServiceDisplay1 | string? |
| PROCEDURE_DATE_1 | ProcedureDate1 | DateTime? |
| PROCEDURE_DATE_2 | ProcedureDate2 | DateTime? |
| ITEM_REVENUE_CODE | ItemRevenueCode | string? |
| ITEM_REVENUE_DISPLAY | ItemRevenueDisplay | string? |
| ITEM_MODIFIER_CODE | ItemModifierCode | string? |
| ITEM_MODIFIER_SYSTEM | ItemModifierSystem | string? |
| ITEM_PRODUCTORSERVICE_CODE_2 | ItemProductOrServiceCode2 | string? |
| ITEM_PRODUCTORSERVICE_DISPLAY_2 | ItemProductOrServiceDisplay2 | string? |
| SUPPORTING_INFO_CODE_1 | SupportingInfoCode1 | string? |
| SUPPORTING_INFO_CODE_2 | SupportingInfoCode2 | string? |
| SUPPORTING_INFO_DISPLAY | SupportingInfoDisplay | string? |
| SUPPORTING_INFO_VALUE_1 | SupportingInfoValue1 | string? |
| SUPPORTING_INFO_VALUE_2 | SupportingInfoValue2 | string? |
| SUPPORTING_INFO_1 | SupportingInfo1 | string? |
| SUPPORTING_INFO_2 | SupportingInfo2 | string? |
| SUPPORTING_INFO_3 | SupportingInfo3 | string? |
| SUPPORTING_INFO_4 | SupportingInfo4 | string? |
| SUPPORTING_INFO_5 | SupportingInfo5 | string? |
| SUPPORTING_INFO_6 | SupportingInfo6 | string? |
| SUPPORTING_INFO_7 | SupportingInfo7 | string? |
| ITEM_QUANTITY | ItemQuantity | decimal? |
| ITEM_ADJUDICATION_REASON_CODE | ItemAdjudicationReasonCode | string? |
| ITEM_ADJUDICATION_REASON_DISPLAY | ItemAdjudicationReasonDisplay | string? |
| ITEM_ADJUDICATION_AMOUNT_1 | ItemAdjudicationAmount1 | decimal? |
| ITEM_ADJUDICATION_AMOUNT_2 | ItemAdjudicationAmount2 | decimal? |
| PAYMENT_DATE | PaymentDate | DateTime? |
| SUPPORTING_INFO_VALUE | SupportingInfoValue | string? |
| ITEM_NOTE_NUMBER | ItemNoteNumber | int? |
| SUPPORTING_INFO_TIMING | SupportingInfoTiming | string? |
| SUPPORTING_INFO | SupportingInfo | string? |
| SUPPORTING_INFO_CATEGORY | SupportingInfoCategory | string? |

## Configuration

### Connection String

Update `appsettings.json` with your Oracle database connection details:

```json
{
  "ConnectionStrings": {
    "OracleConnection": "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=your-oracle-host)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=your-service-name)));User Id=your-username;Password=your-password;"
  }
}
```

### Development Settings

For development with different credentials, update `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "OracleConnection": "Data Source=localhost:1521/XEPDB1;User Id=dev_user;Password=dev_password;"
  }
}
```

## API Endpoints

All endpoints require certificate-based authentication via the `X-Client-Cert` header.

### 1. Get EOB by Identifier

**Endpoint**: `GET /api/eob/{identifier}`

**Description**: Retrieves a single EOB record by its unique identifier.

**Example**:
```http
GET /api/eob/EOB-12345 HTTP/1.1
Host: localhost:5001
X-Client-Cert: -----BEGIN CERTIFICATE-----...-----END CERTIFICATE-----
```

**Response**:
```json
{
  "identifier": "EOB-12345",
  "patient": "PAT-67890",
  "clmType": "institutional",
  "billablePeriodStart": "2026-01-15T00:00:00",
  "billablePeriodEnd": "2026-01-20T00:00:00",
  "created": "2026-01-25T10:30:00",
  "primaryDiagnosisCode": "Z79.4",
  "primaryDiagnosisDisplay": "Long term (current) use of insulin",
  ...
}
```

### 2. Get EOBs by Patient

**Endpoint**: `GET /api/eob/patient/{patientId}`

**Description**: Retrieves all EOB records for a specific patient, ordered by creation date (newest first).

**Example**:
```http
GET /api/eob/patient/PAT-67890 HTTP/1.1
Host: localhost:5001
X-Client-Cert: -----BEGIN CERTIFICATE-----...-----END CERTIFICATE-----
```

**Response**:
```json
[
  {
    "identifier": "EOB-12345",
    "patient": "PAT-67890",
    ...
  },
  {
    "identifier": "EOB-12346",
    "patient": "PAT-67890",
    ...
  }
]
```

### 3. Get EOBs by Date Range

**Endpoint**: `GET /api/eob/daterange?startDate={startDate}&endDate={endDate}`

**Description**: Retrieves EOB records within a specific billable period date range.

**Query Parameters**:
- `startDate` (required): Start date in ISO 8601 format
- `endDate` (required): End date in ISO 8601 format

**Example**:
```http
GET /api/eob/daterange?startDate=2026-01-01&endDate=2026-01-31 HTTP/1.1
Host: localhost:5001
X-Client-Cert: -----BEGIN CERTIFICATE-----...-----END CERTIFICATE-----
```

**Response**:
```json
[
  {
    "identifier": "EOB-12345",
    "billablePeriodStart": "2026-01-15T00:00:00",
    "billablePeriodEnd": "2026-01-20T00:00:00",
    ...
  }
]
```

### 4. Get All EOBs

**Endpoint**: `GET /api/eob`

**Description**: Retrieves all EOB records (use with caution - may return large dataset).

**Example**:
```http
GET /api/eob HTTP/1.1
Host: localhost:5001
X-Client-Cert: -----BEGIN CERTIFICATE-----...-----END CERTIFICATE-----
```

**Response**:
```json
[
  { ... },
  { ... }
]
```

## Service Layer

### IEobService Interface

The service interface defines the contract for EOB data access:

```csharp
public interface IEobService
{
    Task<PatientAccessEob?> GetEobByIdentifierAsync(string identifier);
    Task<IEnumerable<PatientAccessEob>> GetEobsByPatientAsync(string patientId);
    Task<IEnumerable<PatientAccessEob>> GetEobsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<PatientAccessEob>> GetAllEobsAsync();
}
```

### EobService Implementation

The service implementation includes:
- Dependency injection of `MesEdwsDbContext` and `ILogger`
- Async database operations
- Error logging
- LINQ queries for filtering and sorting

## Testing

### Prerequisites
1. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

2. Update the connection string in `appsettings.json`

3. Ensure the Oracle database is accessible

4. Have a valid client certificate for authentication

### Testing with curl

```bash
# Get EOB by identifier
curl -X GET https://localhost:5001/api/eob/EOB-12345 \
  -H "X-Client-Cert: -----BEGIN CERTIFICATE-----...-----END CERTIFICATE-----"

# Get EOBs by patient
curl -X GET https://localhost:5001/api/eob/patient/PAT-67890 \
  -H "X-Client-Cert: -----BEGIN CERTIFICATE-----...-----END CERTIFICATE-----"

# Get EOBs by date range
curl -X GET "https://localhost:5001/api/eob/daterange?startDate=2026-01-01&endDate=2026-01-31" \
  -H "X-Client-Cert: -----BEGIN CERTIFICATE-----...-----END CERTIFICATE-----"
```

### Testing with Postman

1. Create a new GET request
2. Add header: `X-Client-Cert` with certificate value
3. Set the endpoint URL
4. Send the request

## Error Handling

### HTTP Status Codes

- `200 OK`: Successful request
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Missing or invalid certificate
- `404 Not Found`: EOB record not found
- `500 Internal Server Error`: Server or database error

### Example Error Response

```json
{
  "message": "EOB with identifier 'EOB-99999' not found"
}
```

## Logging

The implementation logs the following events:

- EOB retrieval requests (identifier, patient ID, date range)
- Warnings when EOB records are not found
- Errors during database operations

Example log output:
```
info: MES_EDWS.Controllers.EobController[0]
      Retrieving EOB with identifier: EOB-12345
      
warn: MES_EDWS.Controllers.EobController[0]
      EOB not found with identifier: EOB-99999
      
error: MES_EDWS.Services.EobService[0]
      Error retrieving EOB by identifier: EOB-12345
      System.Data.OracleClient.OracleException: Connection timeout
```

## Performance Considerations

1. **Indexing**: Ensure the database has appropriate indexes on:
   - `IDENTIFIER` (primary key)
   - `PATIENT` (for patient lookups)
   - `BILLABLE_PERIOD_START` and `BILLABLE_PERIOD_END` (for date range queries)

2. **Pagination**: The "Get All EOBs" endpoint should be enhanced with pagination for production use:
   ```csharp
   public async Task<IEnumerable<PatientAccessEob>> GetEobsPagedAsync(int page, int pageSize)
   {
       return await _context.PatientAccessEobs
           .OrderByDescending(e => e.Created)
           .Skip((page - 1) * pageSize)
           .Take(pageSize)
           .ToListAsync();
   }
   ```

3. **Caching**: Consider implementing response caching for frequently accessed data

4. **Connection Pooling**: Oracle connection pooling is enabled by default in the Oracle provider

## Security

- All endpoints require certificate-based authentication
- Connection strings should be stored securely (Azure Key Vault, AWS Secrets Manager, etc.)
- Database user should have read-only access to the `PATIENT_ACCESS_EOB` view
- Consider implementing field-level encryption for sensitive data

## Future Enhancements

1. Add pagination support to all list endpoints
2. Implement filtering capabilities (by diagnosis, provider, etc.)
3. Add response caching
4. Create DTOs to control what fields are returned in responses
5. Add OpenAPI/Swagger documentation
6. Implement rate limiting
7. Add health checks for database connectivity
8. Create integration tests

## Dependencies

- `Microsoft.EntityFrameworkCore` (v10.0.0)
- `Microsoft.EntityFrameworkCore.Design` (v10.0.0)
- `Oracle.EntityFrameworkCore` (v9.0.0)
- `Microsoft.AspNetCore.Authentication.Certificate` (v10.0.0)

## Troubleshooting

### Common Issues

1. **Connection String Issues**
   - Verify host, port, and service name
   - Test connection with Oracle SQL Developer or similar tool
   - Check network connectivity and firewall rules

2. **Certificate Authentication Failures**
   - Ensure certificate is properly formatted in the header
   - Verify certificate is not expired
   - Check that certificate is trusted

3. **Column Mapping Errors**
   - Verify that database column names match exactly
   - Check data types match between database and model
   - Ensure nullable columns are marked as nullable in the model

4. **Oracle Provider Issues**
   - Ensure Oracle Instant Client is installed if required
   - Check Oracle.EntityFrameworkCore version compatibility
   - Verify Oracle database version is supported
