# DRX.Environment - AI Coding Assistant Instructions

## Project Architecture

DRX.Environment is a modular .NET framework with three main layers:

### 1. Framework Layer (`Library/Environments/Framework/`)
Core frameworks providing foundational services:
- `DRX.Framework` - Main framework with WPF, ASP.NET Core, and Microsoft Graph integration
- `DRX.Framework.Media` - Media processing capabilities
- `DRX.Framework.Chat` - Chat/communication features
- `Drx.Framework.Memory` - Memory management utilities

### 2. SDK Layer (`Library/Environments/SDK/`)
Reusable components with specific functionality:
- `Drx.Sdk.Network` - Networking with TCP/UDP support and custom serialization
- `Drx.Sdk.Memory` - Memory management and pooling
- `Drx.Sdk.Text` - Text processing utilities
- `Drx.Sdk.Ui.Wpf` - WPF UI components
- `Drx.Sdk.Input` - Input handling
- `Drx.Sdk.Native` - Native interop functionality
- `Drx.Sdk.Shared` - Common utilities and serialization
- `Drx.Sdk.Style` - Styling components
- `Drx.Sdk.Shared.JavaScript` - JavaScript execution engine

### 3. Applications Layer (`Library/Applications/`)
End-user applications built on the SDK:
- **Clients**: Console applications and GUI tools
- **Servers**: Network servers like KaxSocket for TCP/UDP communication

## Development Conventions

### Language & Documentation
- **All comments and documentation in Chinese** - maintain this convention
- **Naming**: `Drx.Sdk.*` for SDK components, `DRX.Framework.*` for frameworks
- **Target Framework**: .NET 9.0-windows (with NativeAOT for release builds)

### Code Patterns
- **Serialization**: Use `DrxSerializationData` for type-safe key-value serialization
- **Networking**: Use `NetworkClient`/`NetworkServer` from `Drx.Sdk.Network.Tcp`
- **JavaScript Integration**: Export .NET classes to JavaScript using the script execution engine
- **Error Handling**: Comprehensive exception handling with detailed stack traces

### Build Configuration
- **Debug**: Standard .NET debugging
- **Release**: NativeAOT compilation for performance (`PublishAot=true`)
- **Dependencies**: Managed via project references and NuGet packages

## Critical Workflows

### Build Commands
```bash
# Build entire solution
dotnet build DRX.Environment.sln

# Publish with NativeAOT
dotnet publish DRX.Environment.sln

# Development with hot reload
dotnet watch run --project DRX.Environment.sln
```

### Testing
- Unit tests located in `Examples/` directories
- Example: `Examples/DrxSerializationExample/` demonstrates serialization features
- Example: `Examples/JavaScript/` shows JavaScript execution and auto-completion

### Development Flow
1. Modify SDK components in `Library/Environments/SDK/`
2. Test changes using example applications in `Examples/`
3. Build and run server applications in `Library/Applications/Servers/`

## Key Technical Features

### JavaScript Execution Engine
- **Location**: `Drx.Sdk.Shared.JavaScript` and `Drx.Sdk.Network`
- **Features**: Execute JavaScript with .NET interop, auto-completion, parameter hints
- **Usage**: Export .NET classes as `MathUtils`, `StringHelper`, `Person` etc.
- **API**: `JavaScript.Execute()`, `ExecuteAsync()`, `ExecuteFile()`

### Auto-Completion System
- **VSCode-style completion** in JavaScript console
- **Types**: Class names, member access, method parameters
- **Example**: `MathUtils.Add(StringUtils.Format("Hello"), 2)`

### Custom Serialization
- **Class**: `DrxSerializationData` in `Drx.Sdk.Shared.Serialization`
- **Features**: Type-safe key-value storage, binary serialization, nested objects
- **Methods**: `SetString()`, `SetInt()`, `SetObject()`, `TryGetString()`, `Serialize()`

### Networking (V2)
- **Classes**: `NetworkClient`, `NetworkServer` in `Drx.Sdk.Network.Tcp`
- **Protocols**: TCP and UDP support
- **Events**: `OnConnected`, `OnDataReceived`, `OnError`
- **Example**: Simple TCP/UDP server-client communication with serialization

## Integration Points

### External Dependencies
- **Microsoft.Graph**: Graph API integration
- **MailKit/MimeKit**: Email functionality
- **Microsoft.Data.Sqlite**: SQLite database support
- **Markdig**: Markdown processing
- **Costura.Fody**: Assembly embedding

### Cross-Component Communication
- **Serialization**: `DrxSerializationData` used across network and storage layers
- **Logging**: Unified logging through `ScriptLogger` and custom implementations
- **Events**: Delegate-based event system for network communications

## Common Patterns

### Error Handling
```csharp
try {
    // Operation that might fail
} catch (Exception ex) {
    // Log with full context
    logger.LogException(ex, context);
}
```

### Serialization Usage
```csharp
var data = new DrxSerializationData();
data.SetString("key", "value");
data.SetInt("number", 42);
var bytes = data.Serialize();
var restored = DrxSerializationData.Deserialize(bytes);
```

### Network Communication
```csharp
var server = new NetworkServer(endpoint, enableTcp: true, enableUdp: true);
server.OnDataReceived += (id, ep, data) => {
    // Handle received data
};
await server.StartAsync();
```

## File Organization Examples

### SDK Component Structure
```
Drx.Sdk.Network/
├── V2/Socket/
│   ├── NetworkClient.cs      # Client implementation
│   └── NetworkServer.cs      # Server implementation
├── DataBase/                 # Database utilities
└── Email/                    # Email functionality
```

### Application Structure
```
KaxSocket/
├── Program.cs               # Main entry point
├── KaxSocket.csproj         # Project configuration
└── bin/                     # Build output
```

Remember: This codebase emphasizes **modularity**, **type safety**, and **Chinese documentation**. When making changes, maintain these architectural boundaries and conventions.
