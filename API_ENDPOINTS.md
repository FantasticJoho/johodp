# 📡 API Endpoints Reference

## Base URL
```
https://localhost:5001/api
```

## Users Endpoints

### 1. Register a New User
**POST** `/users/register`

Enregistre un nouvel utilisateur dans le système.

#### Request Body
```json
{
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe"
}
```

#### Request Parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| email | string | ✅ | Adresse e-mail unique |
| firstName | string | ✅ | Prénom (max 50 caractères) |
| lastName | string | ✅ | Nom (max 50 caractères) |

#### Response 200 OK
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john.doe@example.com"
}
```

#### Response 409 Conflict
```json
{
  "message": "User with email john.doe@example.com already exists"
}
```

#### Response 400 Bad Request
```json
{
  "errors": [
    "Email is required",
    "Email format is invalid",
    "First name is required",
    "Last name is required"
  ]
}
```

#### Example cURL
```bash
curl -X POST https://localhost:5001/api/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe"
  }'
```

#### Example PowerShell
```powershell
$body = @{
  email = "john.doe@example.com"
  firstName = "John"
  lastName = "Doe"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:5001/api/users/register" `
  -Method Post `
  -Body $body `
  -ContentType "application/json"
```

#### Example C# (HttpClient)
```csharp
var client = new HttpClient();
var request = new {
  email = "john.doe@example.com",
  firstName = "John",
  lastName = "Doe"
};

var json = JsonConvert.SerializeObject(request);
var content = new StringContent(json, Encoding.UTF8, "application/json");

var response = await client.PostAsync(
  "https://localhost:5001/api/users/register", 
  content);
```

---

### 2. Get User by ID
**GET** `/users/{userId}`

Récupère les détails d'un utilisateur par son ID.

#### URL Parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| userId | GUID | ✅ | Identifiant unique de l'utilisateur |

#### Response 200 OK
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "emailConfirmed": false,
  "isActive": true,
  "createdAt": "2025-11-17T14:30:00Z"
}
```

#### Response 404 Not Found
```json
{
  "message": "User with ID 550e8400-e29b-41d4-a716-446655440000 not found"
}
```

#### Response 400 Bad Request
```json
{
  "message": "Invalid user ID format"
}
```

#### Example cURL
```bash
curl -X GET https://localhost:5001/api/users/550e8400-e29b-41d4-a716-446655440000 \
  -H "Accept: application/json"
```

#### Example PowerShell
```powershell
Invoke-RestMethod -Uri "https://localhost:5001/api/users/550e8400-e29b-41d4-a716-446655440000" `
  -Method Get `
  -ContentType "application/json"
```

#### Example C# (HttpClient)
```csharp
var client = new HttpClient();
var userId = "550e8400-e29b-41d4-a716-446655440000";

var response = await client.GetAsync(
  $"https://localhost:5001/api/users/{userId}");

if (response.IsSuccessStatusCode)
{
  var json = await response.Content.ReadAsStringAsync();
  var user = JsonConvert.DeserializeObject<UserDto>(json);
}
```

---

## Response Status Codes

| Code | Meaning | Description |
|------|---------|-------------|
| 200 | OK | Requête réussie |
| 201 | Created | Ressource créée avec succès |
| 400 | Bad Request | Requête invalide (validation échouée) |
| 404 | Not Found | Ressource non trouvée |
| 409 | Conflict | Conflit (ex: email déjà existant) |
| 500 | Internal Server Error | Erreur serveur |

---

## Data Models

### UserDto
```json
{
  "id": "GUID",
  "email": "string",
  "firstName": "string",
  "lastName": "string",
  "emailConfirmed": "boolean",
  "isActive": "boolean",
  "createdAt": "ISO 8601 DateTime"
}
```

### RegisterUserResponse
```json
{
  "userId": "GUID",
  "email": "string"
}
```

---

## Validation Rules

### Email
- ✅ Requis
- ✅ Format valide (contient @)
- ✅ Unique en base de données
- ✅ Converti en minuscules

### First Name
- ✅ Requis
- ✅ Maximum 50 caractères

### Last Name
- ✅ Requis
- ✅ Maximum 50 caractères

---

## Authentication & Authorization

Actuellement, l'API n'a pas d'authentification.

**À faire** : Implémenter la sécurité JWT/OAuth2 via IdentityServer4

```csharp
// Exemple futur avec authentification
Authorization: Bearer {token}
```

---

## Pagination

Non implémentée actuellement.

---

## Rate Limiting

Non implémentée actuellement.

---

## Changelog des Endpoints

### v1.0 (2025-11-17)
- ✅ POST /api/users/register - Enregistrer un utilisateur
- ✅ GET /api/users/{userId} - Récupérer un utilisateur
- ⏳ GET /api/users - Lister tous les utilisateurs (à faire)
- ⏳ PUT /api/users/{userId} - Mettre à jour un utilisateur (à faire)
- ⏳ DELETE /api/users/{userId} - Supprimer un utilisateur (à faire)
- ⏳ POST /api/users/{userId}/confirm-email - Confirmer l'email (à faire)

---

## Swagger/OpenAPI

La documentation interactive est disponible sur:
```
https://localhost:5001/swagger
```

---

## Exemples de flux complet

### Scénario 1: Création et récupération d'un utilisateur

```bash
# 1. Enregistrer un nouvel utilisateur
curl -X POST https://localhost:5001/api/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "alice@example.com",
    "firstName": "Alice",
    "lastName": "Smith"
  }'

# Response:
# {
#   "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
#   "email": "alice@example.com"
# }

# 2. Récupérer l'utilisateur créé
curl -X GET https://localhost:5001/api/users/a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  -H "Accept: application/json"

# Response:
# {
#   "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
#   "email": "alice@example.com",
#   "firstName": "Alice",
#   "lastName": "Smith",
#   "emailConfirmed": false,
#   "isActive": true,
#   "createdAt": "2025-11-17T15:45:30Z"
# }
```
