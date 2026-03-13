# Trade Imports GMR Processor

The GMR Processor consumes Matched GMRs from the Finder and data events from the Trade Data API and then performs the following:

- `GTO` - Places or removes holds on GMRs via GVMS depending on the InspectionRequired ImportPreNotification status
- `ETA` - Provides an updated time of arrival to Ipaffs when a GMR is marked as Embarked
- `ImportMatchedGmrs` - Provides an indication to Ipaffs that a CHED reference was matched with a GMR from GVMS

The solution includes:

- `src/GmrProcessor` – The GMR Processor service
- `tests/GmrProcessor.Tests` – Unit tests
- `tests/GmrProcessor.IntegrationTests` – Integration tests
- `tests/TestFixtures` – Shared fixture payloads for tests
- `compose` / `compose.yml` – The service and related service dependencies to run via Docker

## Quick Start

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/) and Docker.
2. Copy `.env.example` to `.env` and fill in the secrets.
3. Run the full stack:
   ```bash
   docker compose up --build -d
   ```
4. Run tests:
   ```bash
   dotnet test
   ```

## Local Development

### Run dependencies + API locally

Start supporting services:

```bash
docker compose up localstack mongodb asb wiremock
```

Run the API via Docker (`docker compose up gmr-processor`) or via your IDE using the launchSettings.json provided as a
base environment configuration, adding the relevant environment variables from `.env`.


## Data Flows

The `GtoDataEventsQueueConsumer` routes messages to other processors by the `ResourceType`:

```
SQS (trade_imports_data_upserted_gmr_processor_gto)
  │
  ▼
DataEventsQueueConsumer
  │  Routes by ResourceType:
  ├─ ImportPreNotification → GtoImportPreNotificationProcessor
  └─ CustomsDeclaration   → MrnChedMatchProcessor
```

**1. Mrn:CHED Match Processor - CustomsDeclaration events from BTMS and creates Mrn:CHED matches for later use **

```
Receives CustomsDeclaration events from BTMS

MrnChedMatchProcessor
  ├─ Extract MRN (ResourceId) + validate format
  ├─ Extract CHED references from ClearanceDecision.Results[].ImportPreNotification
  ├─ Skip if invalid MRN or no CHED references
  └─ Upsert MrnChedMatch { Id: MRN, ChedReferences: [...] } → MongoDB
```

**2. GTO Flow — Hold or Release Decisions**

```
Receives ImportPreNotifications events from BTMS

GtoImportPreNotificationProcessor
  ├─ TransitValidation.IsTransit()
  │    Check ProvideCtcMrn == "YES" + valid NCTS MRN reference
  │    If not a transit → skip
  ├─ TransitOverride.IsTransitOverrideRequired()
  │    Complete status (rejected/validated/partially_rejected) → no hold
  │    Inspection required/inconclusive → hold
  ├─ Create/Update ImportTransit record with required hold status → MongoDB
  ├─ Lookup the provided NCTS MRN to find any associated GMRs
  └─ For each matched GMR:
       ▼
     GvmsHoldService.PlaceOrReleaseHold()
       ├─ Find all ImportTransits for MRNs related to GMR
       ├─ Any ImportTransits requires hold? Compare with current stored state
       ├─ Call GVMS API if a hold or release is required
       └─ Update the HoldStatus → MongoDB
```

**3. Import Matching Flow — CHED-GMR Match Notification**

```
Receives MatchedGmrs from GMR Finder

SQS (import_matched_gmrs)
  │  MatchedGmr { GmrId, Mrn }
  ▼
ImportMatchedGmrsQueueConsumer
  ▼
ImportMatchedGmrsProcessor
  ├─ MatchReferenceRepository.GetChedsByMrn()
  │    Combines MrnChedMatches (from flow 1) + ImportTransits (from flow 2)
  ├─ Find previously matched CHEDs → MongoDB (MatchedImportNotifications)
  ├─ Identify newly matched CHEDs
  ├─ Store new MatchedImportNotification → MongoDB
  └─ Publish ImportMatchMessage { referenceNumber, match: true }
       ▼
     Azure Service Bus → IPAFFS
```

**4. ETA Flow — Arrival Time Updates**

```
Receives MatchedGmrs from GMR Finder

SQS (eta_matched_gmrs)
  │  MatchedGmr { GmrId, Mrn }
  ▼
EtaMatchedGmrsQueueConsumer
  ▼
EtaMatchedGmrProcessor
  ├─ Only processes GMRs with state = EMBARKED
  ├─ MatchReferenceRepository.GetChedsByMrn()
  │    Same lookup as Import Matching flow
  └─ Publish IpaffsUpdatedTimeOfArrivalMessage
     { referenceNumber, entryReference, localDateTimeOfArrival }
       ▼
     Azure Service Bus → IPAFFS
```

## Configuration

Configuration is provided via `appsettings*.json` and overridden by environment variables. Key settings:

| Environment Variable                                          | Purpose                                                      |
|---------------------------------------------------------------|--------------------------------------------------------------|
| `Mongo__DatabaseUri`                                          | Mongo connection string                                      |
| `Mongo__DatabaseName`                                         | Mongo database name                                          |
| `EtaMatchedGmrsQueueConsumer__QueueName`                      | SQS queue containing Matched GMRs for ETA                    |
| `GtoDataEventsQueueConsumer__QueueName`                       | SQS queue containing Data API Events for GTO                 |
| `GtoMatchedGmrsQueueConsumer__QueueName`                      | SQS queue containing Matched GMRs for GTO                    |
| `ImportMatchedGmrsQueueConsumer__QueueName`                   | SQS queue containing Matched GMRs for ImportMatchedGMRs      |
| `GvmsApi__BaseUri`                                            | GVMS API base URL                                            |
| `GvmsApi__ClientId`                                           | GVMS API Authentication Client ID                            |
| `GvmsApi__ClientSecret`                                       | GVMS API Authentication Client Secret                        |
| `DataApi__BaseAddress`                                        | Trade Imports Data API base URL                              |
| `DataApi__Username` / `DataApi__Password`                     | Basic auth for Data API                                      |
| `TradeImportsServiceBus__Eta__ConnectionString`               | Azure Service Bus connection string for ETA updates          |
| `TradeImportsServiceBus__Eta__QueueName`                      | Service Bus queue for ETA updates                            |
| `TradeImportsServiceBus__ImportMatchResult__ConnectionString` | Azure Service Bus connection string for Import Match results |
| `TradeImportsServiceBus__ImportMatchResult__QueueName`        | Service Bus queue for Import Match results                   |

## Feature Flags

| Variable                                  | Purpose                                                                             |
|-------------------------------------------|-------------------------------------------------------------------------------------|
| `ENABLE_MATCHED_GMR_SQS_CONSUMERS`        | Enables or disables the Matched GMR SQS queue consumers                             |
| `ENABLE_DATA_EVENT_SQS_CONSUMER`          | Enables or disables the Data Event SQS queue consumer                               |
| `ENABLE_TRADE_IMPORTS_MESSAGING`          | Enables or disables Azure Service Bus messaging                                     |
| `ENABLE_STORE_OUTBOUND_MESSAGES`          | Enables or disables storing outbound messages to MongoDB                            |
| `ENABLE_GVMS_API_CLIENT_HOLD`             | Enables or disables GVMS API hold endpoint requests for placing/releasing GMR holds |
| `ENABLE_GVMS_API_CLIENT_IGNORE_NOT_FOUND` | Enables or disables ignoring 404 responses from the GVMS API hold endpoint |
| `ENABLE_DEV_ENDPOINTS`                    | Enables or disables development message endpoints                                   |
| `DEV_ENDPOINT_USERNAME`                   | Username for basic authentication on development endpoints                          |
| `DEV_ENDPOINT_PASSWORD`                   | Password for basic authentication on development endpoints                          |

## Testing

- Unit tests: `dotnet test tests/GmrProcessor.Tests`
- Integration tests: `dotnet test tests/GmrProcessor.IntegrationTests`

## Linting & Formatting

We use CSharpier. To run:

```bash
dotnet tool restore
dotnet csharpier format .
```

## License

The Open Government Licence (OGL) permits reuse of public-sector information with minimal conditions. See `LICENSE` for
full text.
