# Johodp.Contracts - API Consumer Library

## Overview

The `Johodp.Contracts` project has been created to enable external .NET applications to consume the Johodp API without needing to reference the entire Application layer and its dependencies.

## Architecture

```
┌─────────────────────┐
│  External Client    │
│  (Your .NET App)    │
└──────────┬──────────┘
           │ references
           ▼
┌─────────────────────┐
│  Johodp.Contracts   │  ← Public DTOs only
└─────────────────────┘

┌─────────────────────┐
│   Johodp.Api        │  ← API endpoints
└──────────┬──────────┘
           │ references
           ▼
┌─────────────────────┐
│ Johodp.Application  │  ← Business logic
└──────────┬──────────┘
           │ references
           ▼
┌─────────────────────┐
│  Johodp.Contracts   │  ← Shared DTOs
└─────────────────────┘
```

## Benefits

1. **Lightweight dependency**: Client apps only need `Johodp.Contracts.dll` (no Domain, Infrastructure, or Application references)
2. **Clean API surface**: Only public DTOs are exposed, internal implementation remains private
3. **Versioning**: DTOs can be versioned independently from implementation
4. **NuGet publishable**: Can be packaged and distributed as a NuGet package

## Available Contracts

### Users (`Johodp.Contracts.Users`)
- `UserDto` - User information
- `RegisterUserResponse` - Registration result

### Tenants (`Johodp.Contracts.Tenants`)
- `TenantDto` - Tenant information
- `CreateTenantDto` - Create new tenant
- `UpdateTenantDto` - Update existing tenant

### Clients (`Johodp.Contracts.Clients`)
- `ClientDto` - OAuth2/OIDC client information
- `CreateClientDto` - Create new client
- `UpdateClientDto` - Update existing client

### Custom Configurations (`Johodp.Contracts.CustomConfigurations`)
- `CustomConfigurationDto` - Branding and localization settings
- `CreateCustomConfigurationDto` - Create new configuration
- `UpdateCustomConfigurationDto` - Update existing configuration

## Usage Example

In your external .NET application:

```bash
# Reference only the Contracts project
dotnet add reference path/to/Johodp.Contracts.csproj
# OR install from NuGet (when published)
dotnet add package Johodp.Contracts
```

```csharp
using Johodp.Contracts.Users;
using Johodp.Contracts.Tenants;

// Call your API and deserialize responses using Contracts DTOs
var client = new HttpClient { BaseAddress = new Uri("https://your-api-url") };

var response = await client.GetAsync("/api/users/123");
var user = await response.Content.ReadFromJsonAsync<UserDto>();

var tenant = await client.GetFromJsonAsync<TenantDto>("/api/tenants/456");
```

## Project Structure

```
Johodp.Contracts/
├── Users/
│   └── UserContracts.cs
├── Tenants/
│   └── TenantContracts.cs
├── Clients/
│   └── ClientContracts.cs
└── CustomConfigurations/
    └── CustomConfigurationContracts.cs
```

## Migration Summary

✅ Created `Johodp.Contracts` project (net8.0 classlib)
✅ Migrated 10 DTO classes from `Johodp.Application` to `Johodp.Contracts`
✅ Updated 16 files in Application layer to reference Contracts
✅ Updated 4 controllers in Api layer to reference Contracts
✅ Removed old DTO files from Application layer
✅ Solution builds successfully

## Next Steps

If you plan to publish this API for external consumption:

1. **Version the DTOs**: Add versioning strategy (e.g., `/v1/`, `/v2/`)
2. **Package as NuGet**: Create `.nuspec` file and publish to NuGet
3. **API Documentation**: Generate OpenAPI/Swagger with DTO schemas
4. **Breaking change policy**: Document your versioning and deprecation strategy
