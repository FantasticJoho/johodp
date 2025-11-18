# API Login Endpoint

## Overview

A JSON-based login endpoint has been added to enable programmatic authentication with cookie-based sessions. This endpoint creates a session cookie that can be used for subsequent authenticated requests.

## Endpoint

**POST** `/api/auth/login`

## Request

```http
POST /api/auth/login HTTP/1.1
Host: localhost:5000
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "P@ssw0rd!"
}
```

## Response

### Success (200 OK)
```json
{
  "message": "Login successful",
  "email": "user@example.com"
}
```
- Sets `HttpOnly` cookie for subsequent requests
- Session lasts 7 days with sliding expiration

### User Not Found → Auto-Register (200 OK)
If the email doesn't exist, the endpoint automatically creates a new user and signs them in:
```json
{
  "message": "Login successful",
  "email": "newuser@example.com"
}
```

### Invalid Credentials (401 Unauthorized)
```json
{
  "error": "Invalid email or password"
}
```

### Two-Factor Required (401 Unauthorized)
```json
{
  "error": "Two-factor authentication required"
}
```

### Registration Failed (400 Bad Request)
```json
{
  "error": "Registration failed",
  "details": "Password must contain at least one special character"
}
```

## Usage Examples

### cURL
```bash
# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"P@ssw0rd!"}' \
  -c cookies.txt

# Subsequent requests with session cookie
curl http://localhost:5000/api/users/profile \
  -b cookies.txt
```

### PowerShell
```powershell
$loginUrl = "http://localhost:5000/api/auth/login"
$credentials = @{
    email = "user@example.com"
    password = "P@ssw0rd!"
} | ConvertTo-Json

$response = Invoke-WebRequest -Uri $loginUrl `
  -Method POST `
  -ContentType "application/json" `
  -Body $credentials `
  -SessionVariable session

# Now use $session in subsequent requests
Invoke-WebRequest http://localhost:5000/api/users/profile `
  -WebSession $session
```

### JavaScript / Fetch
```javascript
// Login
const response = await fetch('/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    email: 'user@example.com',
    password: 'P@ssw0rd!'
  }),
  credentials: 'include'  // Include cookies
});

const data = await response.json();
if (response.ok) {
  console.log('Login successful:', data.message);
  // Cookie automatically set, can now make authenticated requests
}

// Subsequent authenticated request
const profileResponse = await fetch('/api/users/profile', {
  credentials: 'include'  // Include cookies
});
```

### C# HttpClient
```csharp
using var handler = new HttpClientHandler { UseCookies = true };
using var client = new HttpClient(handler);

var loginRequest = new { 
    email = "user@example.com", 
    password = "P@ssw0rd!" 
};

var json = JsonSerializer.Serialize(loginRequest);
var content = new StringContent(json, Encoding.UTF8, "application/json");

var response = await client.PostAsync(
    "http://localhost:5000/api/auth/login", 
    content);

if (response.IsSuccessStatusCode)
{
    var jsonResponse = await response.Content.ReadAsStringAsync();
    Console.WriteLine(jsonResponse);
    // Cookies automatically managed by HttpClientHandler
}
```

## Session Behavior

- **Duration:** 7 days from last activity
- **Sliding Expiration:** Yes (renews on each request)
- **HttpOnly:** Yes (prevents JavaScript access)
- **Secure:** Yes in production (HTTPS only)
- **SameSite:** Lax (CSRF protection)

## Security Notes

- Credentials are validated via `UserManager.CheckPasswordAsync`
- Passwords are hashed with PBKDF2 (default ASP.NET Core Identity)
- Auto-registration uses default first/last names ("User", "Login")
- MFA is enforced if user's roles require it (returns 401 with TwoFactorRequired)
- Failed login does not create or reveal user information
- Email is not enumerated (same response for non-existent and wrong password)

## Integration with UI

The cookie created by this endpoint works across both:
- **Web Pages:** `/account/login`, `/account/register`, etc.
- **API Endpoints:** Any endpoint decorated with `[Authorize]`

## Testing

```bash
# Terminal 1: Start the API
cd c:\Users\jonat\repo\johodp
dotnet run --project src/Johodp.Api

# Terminal 2: Test the endpoint
curl -X POST http://localhost:5000/api/auth/login `
  -H "Content-Type: application/json" `
  -d '{"email":"test@example.com","password":"TestPassword123"}'
```

Expected output:
```json
{"message":"Login successful","email":"test@example.com"}
```
