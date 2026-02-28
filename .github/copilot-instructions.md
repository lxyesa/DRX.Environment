# DRX.Environment Codebase Instructions

This document guides AI agents working on the DRX framework ecosystem, a comprehensive .NET 9.0 platform for building distributed applications with HTTP servers, serialization, memory operations, and custom networking.

## Architecture Overview

### Three-Layer Stack

1. **Framework Layer** (`Library/Environments/Framework/`)
   - `DRX.Framework` - Core component system (IComponentSystem, DataModel patterns)
   - `DRX.Framework.Chat` - Chat functionality
   - `DRX.Framework.Media` - Media handling
   - `Drx.Framework.Memory` - Memory utilities

2. **SDK Layer** (`Library/Environments/SDK/`)
   - **Core**: `Drx.Sdk.Shared` (serialization, DMixin injection, utilities)
   - **Network**: `Drx.Sdk.Network` (HTTP server, SQLite V2 ORM, TCP, Sessions)
   - **Native**: `Drx.Sdk.Native` - P/Invoke bindings
   - **Memory**: `Drx.Sdk.Memory` - Process memory operations, hooking
   - **Input/Text/Style/UI**: Utility SDKs

3. **Applications Layer** (`Library/Applications/`)
   - **Servers**: KaxSocket (HTTP API server), KaxHub, Web.KaxServer2
   - **Clients**: AI, DSTools, DLTools, KaxClientTest

### Key Entry Point: KaxSocket Server

Located in `Library/Applications/Servers/KaxSocket/`, this HTTP REST API server demonstrates framework usage:
- Runs on `http://+:8462/`
- Uses `DrxHttpServer` for HTTP handling and routing
- Partial class pattern separates concerns: Authentication, UserProfile, UserAssets, CdkManagement, AssetManagement, Shopping
- Implements user management, CDK (redemption codes), and asset management
- SQLite V2 ORM for persistence
- JWT authentication via `JwtHelper`

## Critical Development Patterns

### 1. HTTP Handler Pattern (KaxHttp Model)

Use **partial classes** to organize APIs by feature domain:
```csharp
// KaxHttp.Authentication.cs
public partial class KaxHttp {
    [HttpHandle("POST", "/api/login")]
    public static async Task<HttpResponse> Login(HttpRequest request) { ... }
}
```

**Key attributes**:
- `[HttpHandle("METHOD", "path")]` - Declares HTTP endpoint (auto-discovered by framework)
- `[HttpMiddleware]` - Global request/response interceptor
- `[RateLimitMaxRequests]` / `[RateLimitWindowSeconds]` - Rate limiting

**Response types** (`Drx.Sdk.Network.Http.Results`):
- `HttpResponse` - Plain text/JSON
- `HtmlResultFromFile` - Static HTML files
- `FileDownloadResult` - File streaming
- `OkResult`, `BadRequestResult` - Status helpers

### 2. Data Model Hierarchy

Base class pattern for database entities:
```csharp
public abstract class DataModel : IDataBase {
    public int Id { get; set; }  // Primary key
}

public class UserData : DataModel {
    public string UserName { get; set; }
    public string PasswordHash { get; set; }
    public UserPermissionGroup PermissionGroup { get; set; }
}
```

Models go in `Model/` directory; use `SqliteV2<T>` for CRUD operations.

### 3. SQLite V2 ORM (Drx.Sdk.Network.DataBase)

Query pattern:
```csharp
// Select all users named "Alice"
var results = await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", "Alice");

// Insert/Update/Delete
var user = new UserData { UserName = "Bob", ... };
await database.InsertAsync(user);
```

Key types:
- `SqliteV2<T>` - Main ORM for CRUD
- `TableList<T>` - One-to-many relational support
- `SqliteUnitOfWork<T>` - Transaction management

See `Library/Environments/SDK/Drx.Sdk.Network/Database/Docs/` for full SQLite V2 documentation.

### 4. Serialization: DrxSerializationData (DSD)

Lightweight binary key-value container for cross-process communication:
```csharp
var data = new DrxSerializationData();
data.SetString("name", "Alice");
data.SetInt("age", 30);
data.SetObject("nested", new DrxSerializationData());

var bytes = data.Serialize();
var restored = DrxSerializationData.Deserialize(bytes);
```

Supports: Null, Int64, Double, Bool, String, Bytes, nested Objects.
Thread-safe with `ReaderWriterLockSlim`. See `Library/Environments/SDK/Drx.Sdk.Shared/Serialization/README.md`.

### 5. Runtime Method Injection: DMixin

Minimal AOP framework using Harmony for static method injection:
```csharp
[DMixin(typeof(TargetClass))]
public class MyMixin {
    [DMixinInject(targetMethod: "SomeMethod", at: InjectAt.Prefix)]
    public static void Prefix(CallbackInfo ci) {
        // Control execution: ci.Continue() or ci.Cancel()
    }
}
```

Single static methods only; parameter compatibility required. See `Drx.Sdk.Shared.DMixin`.

## Build & Development Workflow

### Available Tasks (in `.vscode/tasks.json`)

```powershell
# Build entire solution
dotnet build DRX.Environment.sln

# Publish projects
dotnet publish DRX.Environment.sln

# Watch mode (rebuild on file changes)
dotnet watch run --project DRX.Environment.sln
```

Run via: `Ctrl+Shift+B` (build), or use `run_task` tool.

### Admin Rights Requirement

KaxSocket enforces admin privileges at startup:
```csharp
if (!GlobalUtility.IsAdministrator()) {
    await GlobalUtility.RestartAsAdministratorAsync();
    Environment.Exit(0);
}
```

When debugging/developing KaxSocket locally, run VS Code as Administrator.

### Configuration Files

Convention: `configs.ini` in app base directory managed by `ConfigUtility`:
```csharp
var uploadToken = ConfigUtility.Read("configs.ini", "upload_token", "general");
ConfigUtility.Push("configs.ini", "uploadToken", value, "general");
```

## Project-Specific Conventions

1. **File Structure**: Place 404.html, static assets in `Views/html/`; configs in app root
2. **Error Handling**: Use `Logger.Warn()`, `Logger.Info()` (custom logging extension)
3. **JWT Auth**: Static configuration in handler static constructor; validate with `JwtHelper.ValidateToken()`
4. **Rate Limiting**: Auto-bans users exceeding constraints; check before sensitive operations
5. **Async/Await**: Consistent throughout; use `Task<T>` return types
6. **Partial Classes**: Split handler files by feature area (Authentication, Shopping, etc.)

## Cross-Component Communication

- **HTTP APIs** ↔ JavaScript frontends via JSON (structured responses from handlers)
- **Services** ↔ **Databases** via SQLite V2 ORM (typesafe CRUD)
- **Native Interop** via `Drx.Sdk.Native` P/Invoke bindings (memory ops, hooks)
- **Binary Protocol** via `DrxSerializationData` (inter-process communication)

## Testing & Debugging

Examples reference `Library/Examples/`:
- `JsonSerializationExample.cs` - HTTP + serialization patterns
- `SessionExample/` - Cookie/session management
- `StackedMapExample/` - Advanced ORM usage

Navigate to relevant subdirectory, compile with `dotnet build`, and inspect code.

## Key References

- **Serialization**: [Drx.Sdk.Shared/Serialization/README.md](../Library/Environments/SDK/Drx.Sdk.Shared/Serialization/README.md)
- **Database**: [Drx.Sdk.Network/Database/Docs/](../Library/Environments/SDK/Drx.Sdk.Network/Database/Docs/)
- **HTTP Framework**: [KaxSocket Architecture](../KaxSocket_Architecture_Analysis.md)
- **Network Socket Export**: [Drx.Sdk.Network/Legacy/Socket/README.md](../Library/Environments/SDK/Drx.Sdk.Network/Legacy/Socket/Exports/README.md)
