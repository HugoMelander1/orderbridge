# Development guide

## Development

You need .NET 10 SDK, Node.js 24 and Docker Desktop. Open `OrderBridge.slnx` in Visual Studio or the repository folder in VS Code.

Start PostgreSQL and RabbitMQ:

```sh
docker compose up -d postgres rabbit
dotnet restore
dotnet tool restore
```

Run these in separate terminals. Start the Order API first so it can apply the database migrations:

```sh
dotnet run --project src/OrderBridge.Api --launch-profile http
dotnet run --project src/OrderBridge.Inventory --launch-profile http
dotnet run --project src/OrderBridge.Worker
```

Start the frontend in another terminal:

```sh
cd frontend
npm ci
npm run dev
```

Vite proxies `/api` to port 5080. On Windows, use `npm.cmd` or `npx.cmd` if PowerShell blocks the scripts.

The development settings use the same local credentials as Compose. If you change `.env`, update `ConnectionStrings__Database` and `RabbitUri` for the .NET processes too: .NET does not load `.env` automatically.

To create a database migration:

```sh
dotnet ef migrations add YourChange --project src/OrderBridge.Core --output-dir Migrations
```

## Tests

```sh
dotnet build
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
cd frontend
npm ci
npm run build
npm test
```

Integration tests use PostgreSQL and RabbitMQ through Testcontainers and require Docker. They cover reservations, duplicate delivery, retries, dead letters and replay.

For browser tests, start the full application with Compose, then run from `frontend`:

```sh
npx playwright install chromium
npm run e2e -- --workers=1
```

Browser tests create orders and change the warehouse demo mode. GitHub Actions runs the backend, frontend and browser tests; results are available in the repository's [Actions tab](https://github.com/HugoMelander1/orderbridge/actions).

## API

Create an order:

```sh
curl -X POST http://localhost:5080/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customer":"Acme Studio","items":[{"sku":"KB-01","quantity":1}]}'
```

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/orders` | Create an order |
| GET | `/api/orders?search=&status=&page=1` | Search and filter orders |
| GET | `/api/orders/{id}` | Order details and timeline |
| GET | `/api/dashboard` | Order counts by status |
| GET | `/api/products` | Products and current stock |
| GET | `/api/dependencies` | Dependency health |
| POST | `/api/orders/{id}/replay` | Replay a failed order |
| GET/POST | `/api/demo` | Read or change the warehouse mode |
| GET | `/api/reservations/{id}` | Successful reservation count |

Replay, demo mode and reservation count endpoints are available only in Development.


