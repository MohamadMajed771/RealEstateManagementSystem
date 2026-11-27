# Real Estate Marketplace & Property Ownership Transfer

Two independent ASP.NET Core MVC applications that talk only through RabbitMQ.

| Project | What it is | Database | URL (dev) |
|---|---|---|---|
| `RealEstateMarketplace` | System 1: listings, search, chat, transactions | `RealEstateMarketplaceDB` | https://localhost:7287 |
| `PropertyTransferManagement` | System 2: ownership-transfer cases, documents, officers | `PropertyTransferDB` | https://localhost:7041 |
| `SharedContracts` | Message contracts + shared RabbitMQ topology/consumer | none | none |

## Run locally

### Windows (SQL Server Express)
`appsettings.json` already points at `.\SQLEXPRESS`. Install RabbitMQ (or run only the
`rabbitmq` service from `docker-compose.yml`), then run both projects.

### macOS / Linux (Docker)
```bash
docker compose up -d
cp RealEstateMarketplace/appsettings.Development.example.json RealEstateMarketplace/appsettings.Development.json
cp PropertyTransferManagement/appsettings.Development.example.json PropertyTransferManagement/appsettings.Development.json
dotnet run --project RealEstateMarketplace --launch-profile https
dotnet run --project PropertyTransferManagement --launch-profile https
```

In Development, both apps apply EF Core migrations at startup and seed:
- System 1: the `Admin` role, plus the `SeedAdmin` account from `appsettings.Development.json`.
- System 2: the `Admin` and `Officer` roles, the document types, plus the `SeedUsers` accounts.

**Email:** if `SmtpSettings` are not configured (keep them in user-secrets, never in git),
emails are printed to the app console. Copy the confirmation link from there.

RabbitMQ management UI: http://localhost:15672 (guest / guest).

## RabbitMQ message catalogue

Exchange `realestate.events` (topic, durable). Dead letters go to `realestate.events.dlx` and then the `*.dlq` queues.

| Message | Direction | Routing key | Queue |
|---|---|---|---|
| `TransferRequested` | 1 → 2 | `marketplace.transfer.requested` | `transfer-service.requests` |
| `TransferCancelled` | 1 → 2 | `marketplace.transfer.cancelled` | `transfer-service.requests` |
| `TransferCaseCreated` | 2 → 1 | `transfer.case.created` | `marketplace.transfer-updates` |
| `TransferStatusChanged` | 2 → 1 | `transfer.case.status-changed` | `marketplace.transfer-updates` |
| `AdditionalDocumentsRequired` | 2 → 1 | `transfer.case.documents-required` | `marketplace.transfer-updates` |
| `TransferCompleted` | 2 → 1 | `transfer.case.completed` | `marketplace.transfer-updates` |
| `TransferRejected` | 2 → 1 | `transfer.case.rejected` | `marketplace.transfer-updates` |

How the messaging stays reliable:
- **Outbox:** the business change and its `OutboxMessages` row are saved in one transaction. A background
  publisher sends pending rows with publisher confirms and retries while RabbitMQ is down.
- **Durable queues + persistent messages:** both apps declare the whole topology, so messages wait in
  the queue while the other app is offline.
- **Consumers:** `BackgroundService` with manual acks and prefetch 1. A failed message is retried 3 times,
  then dead-lettered.
- **Inbox:** `InboxMessages.MessageId` (unique) prevents processing the same message twice.
- Events carry only names, emails, property data and statuses. ID images, documents and chats never leave their system.

## Demo flow

1. System 1: a seller publishes a sale listing, and the admin approves it.
2. The buyer sends a purchase request, the seller accepts it, and both confirm the transaction in **My Transactions**.
3. The buyer clicks **Request Ownership Transfer**. This publishes `TransferRequested`.
4. System 2 creates case `TR-2026-0001` and replies with `TransferCaseCreated`. System 1 shows the case number.
5. System 2 admin (**Case Queue**) assigns the officer. The status becomes *Awaiting Documents*.
6. Buyer and seller register in System 2 **with the same email** and confirm it. The case is linked automatically. Each uploads their documents.
7. The officer accepts the documents, or marks one *Replacement required*. That sends `AdditionalDocumentsRequired` to System 1.
8. The officer moves the case to *Ready for Processing*, then *External Processing in Progress*, then **Record official completion**.
   This sends `TransferCompleted`: the System 1 transaction becomes *Completed* and the listing *Sold*.
9. Resilience: stop System 2, request another transfer in System 1, restart System 2. The queued message is processed.
