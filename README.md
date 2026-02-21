# EazeCad License Server

A production-ready REST API for managing user accounts and software licenses, built with .NET 9 and Clean Architecture.

## Tech Stack

- **.NET 9** / ASP.NET Core Web API
- **PostgreSQL 17** — primary data store
- **Redis 7** — caching, session management, rate-limit counters
- **Entity Framework Core 9** — ORM with code-first migrations
- **Docker** — multi-stage build with docker-compose orchestration

## Features

| Area | Details |
|------|---------|
| **Authentication** | JWT access + refresh tokens, email verification, password reset |
| **User Management** | CRUD, role-based access (Admin / User / Manager), status lifecycle |
| **License Management** | Create, revoke, activate/deactivate, heartbeat, machine fingerprint tracking |
| **Rate Limiting** | Tiered throttling (Global / Auth / User) with progressive delay and penalty |
| **Caching** | Redis-backed user cache with pub/sub invalidation and versioning |
| **Audit Trail** | Tracks all admin and user actions with IP and metadata |
| **Observability** | Serilog structured logging, Prometheus `/metrics` endpoint, health checks |
| **Security** | Security headers middleware, CORS, HTTPS redirection, BCrypt password hashing |
| **API Docs** | Swagger/OpenAPI with JWT auth support (Development mode) |

## Project Structure

```
UserLicenseServer.sln
├── Api/            → Controllers, middleware, filters, DI config
├── Core/           → Entities, DTOs, interfaces, specifications, enums
├── Infrastructure/ → EF DbContext, repositories, services, migrations
└── Tests/          → Unit + integration tests (xUnit, FluentAssertions)
```

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- PostgreSQL 17+
- Redis 7+

### Local Development

1. **Clone and configure:**
   ```bash
   git clone https://github.com/sayyarahmad1995/UserLicenseServer.git
   cd UserLicenseServer
   cp Api/appsettings.example Api/appsettings.Development.json
   ```
   Edit `appsettings.Development.json` with your database and Redis connection strings.

2. **Run migrations and start:**
   ```bash
   dotnet ef database update --project Infrastructure --startup-project Api
   dotnet run --project Api
   ```
   The API starts at `https://localhost:5001`. Swagger UI is available at `/swagger`.

### Docker Compose

```bash
cp .env.example .env
# Edit .env with your secrets
docker-compose up -d
```

This starts the API (port 3000), PostgreSQL, and Redis with health checks.

## Running Tests

```bash
dotnet test                  # Debug mode — 260 tests
dotnet test -c Release       # Release mode — 255 tests (debug-only tests excluded)
```

## API Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/api/v1/auth/register` | — | Register a new user |
| `POST` | `/api/v1/auth/login` | — | Login, returns JWT tokens |
| `POST` | `/api/v1/auth/refresh` | — | Refresh access token |
| `POST` | `/api/v1/auth/revoke` | Yes | Revoke refresh token |
| `GET`  | `/api/v1/auth/notifications` | Yes | Get notification preferences |
| `PUT`  | `/api/v1/auth/notifications` | Yes | Update notification preferences |
| `GET`  | `/api/v1/users` | Admin | List users (paginated, filterable) |
| `GET`  | `/api/v1/users/{id}` | Admin | Get user by ID |
| `PUT`  | `/api/v1/users/{id}/status` | Admin | Update user status |
| `DELETE`| `/api/v1/users/{id}` | Admin | Soft-delete user |
| `GET`  | `/api/v1/licenses` | Admin | List licenses |
| `POST` | `/api/v1/licenses` | Admin | Create license |
| `POST` | `/api/v1/licenses/activate` | — | Activate license on machine |
| `POST` | `/api/v1/licenses/validate` | — | Validate active license |
| `POST` | `/api/v1/licenses/heartbeat` | — | License heartbeat |
| `POST` | `/api/v1/licenses/deactivate` | — | Deactivate license |
| `GET`  | `/api/v1/audit` | Admin | Audit log (paginated) |
| `GET`  | `/api/v1/stats` | Admin | Dashboard statistics |
| `GET`  | `/api/v1/health` | — | Health check (DB + Redis) |
| `GET`  | `/metrics` | — | Prometheus metrics |

## Configuration

See [`appsettings.example`](Api/appsettings.example) for all available settings including JWT, cache TTLs, throttling tiers, SMTP email, and seed data.

## License

[MIT](LICENSE) — Sayyar Ahmad
