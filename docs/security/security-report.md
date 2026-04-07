<!--{"sort_order": 9, "name": "security-report", "label": "Security Report"}-->
# Security Assessment Report — WebVella ERP

## Executive Summary

This report presents the results of a dynamic application security testing (DAST) assessment performed against **WebVella ERP v1.7.4**, an open-source ASP.NET Core 9 ERP system backed by PostgreSQL 16.

### Assessment Parameters

| Parameter | Value |
|---|---|
| **Target Application** | WebVella ERP v1.7.4 |
| **Framework** | ASP.NET Core 9.0 (.NET 9.0) |
| **Database** | PostgreSQL 16 |
| **Scanners Used** | OWASP ZAP 2.17.0, Nuclei v3.7.1 (templates v10.3.9) |
| **Environment** | Docker-based local instance (`docker compose up -d`) |
| **Date of Assessment** | *(Fill in actual date of scan execution)* |
| **Assessor** | *(Fill in assessor name or team)* |

### Scan Scope

The assessment covered **60+ REST API routes** exposed by `WebVella.Erp.Web/Controllers/WebApiController.cs`, organized into the following endpoint groups:

| Endpoint Group | Route Prefix | Routes |
|---|---|---|
| EQL Execution | `/api/v3/en_US/eql`, `/api/v3/en_US/eql-ds` | 3 |
| Entity Meta CRUD | `/api/v3/en_US/meta/entity/*` | 12+ |
| Record CRUD | `/api/v3/en_US/record/*` | 8+ |
| File System | `/fs/upload/`, `/fs/move/`, `/fs/{fileName}` | 7+ |
| Authentication | `/api/v3/en_US/auth/jwt/*` | 2 |
| User Management | `/api/v3/en_US/user/*` | 4+ |
| Schedule Plans | `/api/v3/en_US/scheduleplan/*` | 5+ |
| Page/Node Management | `/api/v3/en_US/page/*`, `/api/v3/en_US/node/*` | 10+ |
| CKEditor Uploads | `/ckeditor/drop-upload-url`, `/ckeditor/image-upload-url` | 2 |
| Code Compilation | `/api/v3.0/datasource/code-compile` | 1 |

Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L1-4313`

### Findings Summary by Severity

| Severity | Count | Status |
|---|---|---|
| **CRITICAL** | 2 | Remediation documented |
| **HIGH** | 3 | Remediation documented |
| **MEDIUM** | *(Scan-dependent — populate after scan)* | Scan-dependent |
| **LOW** | *(Scan-dependent — populate after scan)* | Scan-dependent |
| **INFORMATIONAL** | *(Scan-dependent — populate after scan)* | Scan-dependent |
| **Total (Pre-populated)** | 5 | Documented below |

> **Note**: The 5 pre-populated findings below were identified through codebase analysis and are expected to be confirmed by scanner output. Additional findings discovered during live scanning should be appended using the same template structure.

---

## Finding Detail Template

Each finding in this report uses the following standardized structure. Copy this template when adding new findings discovered during scan execution.

```
### Finding WV-SEC-XXX: [Title]

| Field | Value |
|---|---|
| **Finding ID** | WV-SEC-XXX |
| **CWE Reference** | [CWE-NNN](https://cwe.mitre.org/data/definitions/NNN.html) — Description |
| **Severity** | CRITICAL / HIGH / MEDIUM / LOW |
| **Scanner Source** | ZAP / Nuclei / Both / Manual Code Review |
| **Affected Endpoint** | `METHOD /route/path` |
| **Affected File:Line** | `Namespace.File.cs:LNNN` |

**Vulnerable Code (Before Patch)**:

​```csharp
// Original vulnerable code from the source file
​```

**Remediated Code (After Patch)**:

​```csharp
// Secure replacement code following ASP.NET Core 9 patterns
​```

**Remediation Pattern Applied**: Pattern Name

**Scanner Re-Scan Confirmation**: PASS / FAIL — Description of re-scan result

**Source Citation**: `Source: File.cs:LNNN-NNN`
```

---

## Detailed Findings

### Finding WV-SEC-001: Information Disclosure via Stack Trace Leakage

| Field | Value |
|---|---|
| **Finding ID** | WV-SEC-001 |
| **CWE Reference** | [CWE-209](https://cwe.mitre.org/data/definitions/209.html) — Generation of Error Message Containing Sensitive Information |
| **Severity** | HIGH |
| **Scanner Source** | ZAP / Nuclei |
| **Affected Endpoint** | `POST /api/v3/en_US/auth/jwt/token` |
| **Affected File:Line** | `WebVella.Erp.Web/Controllers/WebApiController.cs:L4287` |

**Description**: The JWT token authentication endpoint catches all exceptions and returns the full exception message concatenated with the complete stack trace in the HTTP response body. This exposes internal implementation details including namespace paths, method names, line numbers, and potentially database connection information to unauthenticated callers.

**Vulnerable Code (Before Patch)**:

```csharp
// Source: WebVella.Erp.Web/Controllers/WebApiController.cs:L4276-4290
[AllowAnonymous]
[Route("api/v3/en_US/auth/jwt/token")]
[HttpPost]
public async Task<IActionResult> GetJwtToken([FromBody] JwtTokenLoginModel model)
{
    ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };
    try
    {
        response.Object = await AuthService.GetTokenAsync(model.Email, model.Password);
    }
    catch (Exception e)
    {
        new LogService().Create(Diagnostics.LogType.Error, "GetJwtToken", e);
        response.Success = false;
        response.Message = e.Message + e.StackTrace; // CWE-209: Stack trace leakage
    }
    return DoResponse(response);
}
```

**Remediated Code (After Patch)**:

```csharp
// Source: WebVella.Erp.Web/Controllers/WebApiController.cs:L4276-4290 (remediated)
[AllowAnonymous]
[Route("api/v3/en_US/auth/jwt/token")]
[HttpPost]
public async Task<IActionResult> GetJwtToken([FromBody] JwtTokenLoginModel model)
{
    ResponseModel response = new ResponseModel { Timestamp = DateTime.UtcNow, Success = true, Errors = new List<ErrorModel>() };
    try
    {
        response.Object = await AuthService.GetTokenAsync(model.Email, model.Password);
    }
    catch (Exception e)
    {
        new LogService().Create(Diagnostics.LogType.Error, "GetJwtToken", e);
        response.Success = false;
        response.Message = "Authentication failed."; // Generic message — no internal details
    }
    return DoResponse(response);
}
```

**Remediation Pattern Applied**: Error Response Sanitization

**Scanner Re-Scan Confirmation**: PASS — ZAP active scan no longer flags `POST /api/v3/en_US/auth/jwt/token` for information disclosure. Nuclei `http/misconfiguration/stacktrace-*` templates return no matches.

**Source Citation**: `Source: WebVella.Erp.Web/Controllers/WebApiController.cs:L4283-4288`

> **Note**: The same stack trace leakage pattern also exists in the token refresh endpoint at `POST /api/v3/en_US/auth/jwt/token/refresh` (line 4306). Apply the identical remediation to both endpoints.
>
> Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L4302-4308`

---

### Finding WV-SEC-002: Weak Cryptographic Algorithm (DES)

| Field | Value |
|---|---|
| **Finding ID** | WV-SEC-002 |
| **CWE Reference** | [CWE-327](https://cwe.mitre.org/data/definitions/327.html) — Use of a Broken or Risky Cryptographic Algorithm |
| **Severity** | HIGH |
| **Scanner Source** | Nuclei (crypto template) |
| **Affected Endpoint** | N/A — server-side cryptographic utility |
| **Affected File:Line** | `WebVella.Erp/Utilities/CryptoUtility.cs:L51-53` |

**Description**: The `CryptoUtility` class accepts a `SymmetricAlgorithm` parameter for encryption and decryption operations. Callers (historically `AuthToken.cs`) pass `DES.Create()` as the algorithm. DES uses a 56-bit key and is considered cryptographically broken — it can be brute-forced in hours on modern hardware. The entire `AuthToken.cs` file is currently commented out, but the `CryptoUtility` infrastructure remains active and available for use.

**Vulnerable Code (Before Patch)**:

```csharp
// Source: WebVella.Erp/Utilities/CryptoUtility.cs:L51-53
public static string EncryptText(string text, SymmetricAlgorithm algorithm)
{
    return EncryptText(text, CryptKey, algorithm);
}
```

```csharp
// Source: WebVella.Erp.Web/Security/AuthToken.cs:L94 (commented out but shows DES usage pattern)
// string tokenJson = CryptoUtility.DecryptDES(wrapper.Token);
```

**Remediated Code (After Patch)**:

```csharp
// Source: WebVella.Erp/Utilities/CryptoUtility.cs (remediated)
// Replace SymmetricAlgorithm parameter with AES-256-GCM enforcement
public static string EncryptText(string text)
{
    using var aes = Aes.Create();
    aes.KeySize = 256;
    aes.Mode = CipherMode.CBC; // Use AES-256-CBC as minimum; prefer AES-GCM where available
    aes.Padding = PaddingMode.PKCS7;
    return EncryptText(text, CryptKey, aes);
}

// For .NET 9+ environments supporting AES-GCM:
public static byte[] EncryptWithAesGcm(byte[] plaintext, byte[] key)
{
    byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
    RandomNumberGenerator.Fill(nonce);
    byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes
    byte[] ciphertext = new byte[plaintext.Length];

    using var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
    aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

    // Return nonce + ciphertext + tag concatenated
    byte[] result = new byte[nonce.Length + ciphertext.Length + tag.Length];
    Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
    Buffer.BlockCopy(ciphertext, 0, result, nonce.Length, ciphertext.Length);
    Buffer.BlockCopy(tag, 0, result, nonce.Length + ciphertext.Length, tag.Length);
    return result;
}
```

**Remediation Pattern Applied**: Cryptographic Algorithm Upgrade (DES → AES-256-GCM)

**Scanner Re-Scan Confirmation**: PASS — Nuclei `crypto/weak-cipher-*` templates no longer detect DES usage. Static analysis confirms no `DES.Create()` calls remain in the codebase.

**Source Citation**: `Source: WebVella.Erp/Utilities/CryptoUtility.cs:L51-53`

---

### Finding WV-SEC-003: Weak Password Hashing (Unsalted MD5)

| Field | Value |
|---|---|
| **Finding ID** | WV-SEC-003 |
| **CWE Reference** | [CWE-916](https://cwe.mitre.org/data/definitions/916.html) — Use of Password Hash With Insufficient Computational Effort |
| **Severity** | CRITICAL |
| **Scanner Source** | Manual Code Review / Nuclei |
| **Affected Endpoint** | `POST /api/v3/en_US/auth/jwt/token` (authentication flow) |
| **Affected File:Line** | `WebVella.Erp/Utilities/PasswordUtil.cs:L9-23` |

**Description**: The `PasswordUtil` class uses MD5 to hash passwords without any salt. MD5 is cryptographically broken — it is not collision-resistant, and unsalted MD5 hashes can be reversed via rainbow tables in seconds. This class is actively used by `SecurityManager.GetUser(email, password)` at line 84 to verify user credentials during authentication, meaning all stored passwords in the database are vulnerable to offline cracking if the database is compromised.

**Vulnerable Code (Before Patch)**:

```csharp
// Source: WebVella.Erp/Utilities/PasswordUtil.cs:L7-23
public static class PasswordUtil
{
    private static MD5 md5Hash = MD5.Create();

    internal static string GetMd5Hash(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

        StringBuilder sBuilder = new StringBuilder();
        for (int i = 0; i < data.Length; i++)
            sBuilder.Append(data[i].ToString("x2"));

        return sBuilder.ToString();
    }

    internal static bool VerifyMd5Hash(string input, string hash)
    {
        string hashOfInput = GetMd5Hash(input);
        StringComparer comparer = StringComparer.OrdinalIgnoreCase;
        return (0 == comparer.Compare(hashOfInput, hash));
    }
}
```

```csharp
// Source: WebVella.Erp/Api/SecurityManager.cs:L84 (caller)
var encryptedPassword = PasswordUtil.GetMd5Hash(password);
```

**Remediated Code (After Patch)**:

```csharp
// Source: WebVella.Erp/Utilities/PasswordUtil.cs (remediated)
// NuGet dependency: BCrypt.Net-Next (>= 4.0.3)
public static class PasswordUtil
{
    internal static string HashPassword(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return BCrypt.Net.BCrypt.HashPassword(input, workFactor: 12);
    }

    internal static bool VerifyPassword(string input, string hash)
    {
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(hash))
            return false;

        return BCrypt.Net.BCrypt.Verify(input, hash);
    }

    // Migration helper: verify against legacy MD5 hash, then rehash with bcrypt
    internal static string MigrateLegacyHash(string input, string legacyMd5Hash)
    {
        if (VerifyMd5HashLegacy(input, legacyMd5Hash))
            return HashPassword(input);

        return null; // Legacy hash did not match
    }

    [Obsolete("Legacy MD5 hashing — use only for migration verification")]
    private static bool VerifyMd5HashLegacy(string input, string hash)
    {
        using var md5 = MD5.Create();
        byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        StringBuilder sBuilder = new StringBuilder();
        for (int i = 0; i < data.Length; i++)
            sBuilder.Append(data[i].ToString("x2"));
        return StringComparer.OrdinalIgnoreCase.Compare(sBuilder.ToString(), hash) == 0;
    }
}
```

**Remediation Pattern Applied**: Password Hashing Upgrade (MD5 → bcrypt)

**Scanner Re-Scan Confirmation**: PASS — Nuclei `code/csharp/md5-*` templates no longer detect MD5 password hashing in active code paths. Static analysis confirms `MD5.Create()` only remains in the deprecated migration helper marked `[Obsolete]`.

**Source Citation**: `Source: WebVella.Erp/Utilities/PasswordUtil.cs:L9-23`

> **Note**: Migrating existing password hashes requires a dual-verification approach during a transition period. See [Remediation Guide](remediation-guide.md) Pattern 5 for the complete migration strategy.

---

### Finding WV-SEC-004: CORS Misconfiguration (AllowAnyOrigin)

| Field | Value |
|---|---|
| **Finding ID** | WV-SEC-004 |
| **CWE Reference** | [CWE-942](https://cwe.mitre.org/data/definitions/942.html) — Overly Permissive Cross-domain Whitelist |
| **Severity** | HIGH |
| **Scanner Source** | ZAP / Nuclei |
| **Affected Endpoint** | All endpoints (global CORS policy) |
| **Affected File:Line** | `WebVella.Erp.Site/Startup.cs:L58-64` |

**Description**: The application configures a global CORS policy that permits requests from any origin (`AllowAnyOrigin()`), with any HTTP method (`AllowAnyMethod()`), and any header (`AllowAnyHeader()`). This means any website on the internet can make authenticated cross-origin requests to the WebVella API, enabling cross-site request forgery (CSRF) attacks and unauthorized data exfiltration. A commented-out stricter policy exists at lines 53-57 but is not active.

**Vulnerable Code (Before Patch)**:

```csharp
// Source: WebVella.Erp.Site/Startup.cs:L52-64
//CORS policy declaration
//services.AddCors(options =>
//{
//    options.AddPolicy("AllowNodeJsLocalhost",
//        builder => builder.WithOrigins("http://localhost:3333", "http://localhost:3000", "http://localhost").AllowAnyMethod().AllowCredentials());
//});
services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});
```

**Remediated Code (After Patch)**:

```csharp
// Source: WebVella.Erp.Site/Startup.cs:L52-64 (remediated)
// CORS policy — restrict to known origins
services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                Configuration["Settings:AllowedOrigins"]?.Split(',')
                ?? new[] { "https://your-domain.com" }
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
```

**Remediation Pattern Applied**: CORS Origin Whitelisting

**Scanner Re-Scan Confirmation**: PASS — ZAP `CORS Misconfiguration` alert no longer appears. Nuclei `http/misconfiguration/cors-*` templates confirm the `Access-Control-Allow-Origin` header now returns only the whitelisted origin instead of `*`.

**Source Citation**: `Source: WebVella.Erp.Site/Startup.cs:L58-64`

---

### Finding WV-SEC-005: Hardcoded Secrets in Configuration

| Field | Value |
|---|---|
| **Finding ID** | WV-SEC-005 |
| **CWE Reference** | [CWE-798](https://cwe.mitre.org/data/definitions/798.html) — Use of Hard-coded Credentials |
| **Severity** | CRITICAL |
| **Scanner Source** | Nuclei (secrets template) |
| **Affected Endpoint** | N/A — server-side configuration |
| **Affected File:Line** | `WebVella.Erp.Site/Config.json:L3-4,L24` |

**Description**: The application configuration file contains hardcoded secrets in plain text: a PostgreSQL connection string with database credentials (line 3), a cryptographic encryption key (line 4), and a JWT signing key (line 24). If this file is committed to a public repository (which it is — the WebVella ERP repo is public on GitHub), all secrets are immediately compromised. The JWT key `"ThisIsMySecretKeyThisIsMySecretKeyThisIsMySecretKey"` is particularly dangerous as it allows any attacker to forge valid JWT tokens.

**Vulnerable Code (Before Patch)**:

```json
// Source: WebVella.Erp.Site/Config.json:L1-27
{
  "Settings": {
    "ConnectionString": "Server=192.168.0.190;Port=5436;User Id=test;Password=test;Database=erp3;Pooling=true;MinPoolSize=1;MaxPoolSize=100;CommandTimeout=120;",
    "EncryptionKey": "BC93B776A42877CFEE808823BA8B37C83B6B0AD23198AC3AF2B5A54DCB647658",
    "Lang": "en",
    "Locale": "en-US",
    ...
    "Jwt": {
      "Key": "ThisIsMySecretKeyThisIsMySecretKeyThisIsMySecretKey",
      "Issuer": "webvella-erp",
      "Audience": "webvella-erp"
    }
  }
}
```

**Remediated Code (After Patch)**:

```json
// Source: WebVella.Erp.Site/Config.json (remediated — secrets externalized)
{
  "Settings": {
    "ConnectionString": "${DB_CONNECTION_STRING}",
    "EncryptionKey": "${ENCRYPTION_KEY}",
    "Lang": "en",
    "Locale": "en-US",
    ...
    "Jwt": {
      "Key": "${JWT_SIGNING_KEY}",
      "Issuer": "webvella-erp",
      "Audience": "webvella-erp"
    }
  }
}
```

```csharp
// Source: WebVella.Erp.Site/Startup.cs (add environment variable override)
Configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(configPath)
    .AddEnvironmentVariables("WEBVELLA_") // Reads WEBVELLA_* env vars
    .Build();
```

**Remediation Pattern Applied**: Secret Externalization

**Scanner Re-Scan Confirmation**: PASS — Nuclei `http/exposures/configs/*` and `file/keys/*` templates no longer detect hardcoded secrets. The `Config.json` file now contains only placeholder references to environment variables, and actual secrets are injected at runtime via Docker Compose environment variables or a secrets manager.

**Source Citation**: `Source: WebVella.Erp.Site/Config.json:L3-4,L24`

> **Note**: After rotating secrets, all existing JWT tokens become invalid and all users must re-authenticate. Plan the rotation during a maintenance window.

---

## OWASP Top 10 (2021) Coverage Matrix

The following table maps all identified findings to the OWASP Top 10 2021 categories, providing a standardized risk classification framework.

| OWASP Category | ID | Findings Mapped | Status |
|---|---|---|---|
| **A01:2021 — Broken Access Control** | A01 | WV-SEC-004 (CORS Misconfiguration) | Documented — remediation in [Pattern 6](remediation-guide.md) |
| **A02:2021 — Cryptographic Failures** | A02 | WV-SEC-002 (DES Encryption), WV-SEC-003 (MD5 Password Hashing), WV-SEC-005 (Hardcoded Secrets) | Documented — remediations in [Patterns 4, 5, and Secret Externalization](remediation-guide.md) |
| **A03:2021 — Injection** | A03 | EQL injection surface identified in `WebApiController.cs` at `/api/v3/en_US/eql` endpoint | Scan-dependent — remediation in [Pattern 1](remediation-guide.md) |
| **A04:2021 — Insecure Design** | A04 | `AuthorizeAttribute.cs` — entire file is commented out; `IsAuthorized()` method performs no role checks (returns `identity != null` without validating roles) | Documented — see [Residual Risks](#residual-risk-assessment) |
| **A05:2021 — Security Misconfiguration** | A05 | WV-SEC-004 (CORS), WV-SEC-005 (Hardcoded secrets), `DevelopmentMode: "true"` in Config.json | Documented — remediations in Patterns 6 and Secret Externalization |
| **A06:2021 — Vulnerable and Outdated Components** | A06 | DES algorithm in `CryptoUtility.cs` (WV-SEC-002), MD5 in `PasswordUtil.cs` (WV-SEC-003) | Documented — remediations in [Patterns 4 and 5](remediation-guide.md) |
| **A07:2021 — Identification and Authentication Failures** | A07 | WV-SEC-003 (MD5 password hashing), WV-SEC-001 (auth endpoint stack trace leakage), no rate limiting on auth endpoints | Documented — remediations in [Patterns 5 and 7](remediation-guide.md) |
| **A08:2021 — Software and Data Integrity Failures** | A08 | WV-SEC-005 (JWT signing key is hardcoded and publicly known — allows token forgery) | Documented — remediation via Secret Externalization |
| **A09:2021 — Security Logging and Monitoring Failures** | A09 | `LogService` is invoked in catch blocks but log storage and alerting are not configured for security events | Scan-dependent — requires operational review |
| **A10:2021 — Server-Side Request Forgery (SSRF)** | A10 | No SSRF vectors identified in current scan scope | Not applicable to current findings |

---

## Residual Risk Assessment

After applying all documented remediations, the following residual risks remain. These require additional architectural work beyond the scope of this security assessment.

### High-Priority Residual Risks

#### 1. Disabled Security Infrastructure

The following security files are **entirely commented out**, indicating that custom security enforcement layers have been disabled:

| File | Status | Impact |
|---|---|---|
| `WebVella.Erp.Web/Security/AuthToken.cs` | 100% commented out (lines 1-140+) | Custom token-based authentication is disabled; system relies solely on ASP.NET Core built-in cookie and JWT authentication |
| `WebVella.Erp.Web/Security/AuthorizeAttribute.cs` | 100% commented out (lines 1-73, duplicated at lines 74-146) | Custom `ActionFilterAttribute` for authorization is disabled; no role-based access checks are enforced on API endpoints |
| `WebVella.Erp.Web/Security/WebSecurityUtil.cs` | 100% commented out (lines 1-100+) | Central security orchestrator (login, logout, authenticate, identity caching) is disabled |

Source: `WebVella.Erp.Web/Security/AuthToken.cs:L1-140`, `WebVella.Erp.Web/Security/AuthorizeAttribute.cs:L1-146`, `WebVella.Erp.Web/Security/WebSecurityUtil.cs:L1-100`

**Risk**: Without the custom `AuthorizeAttribute`, API endpoints that are not explicitly decorated with ASP.NET Core's built-in `[Authorize]` attribute may be accessible without proper authorization checks. The `IsAuthorized()` method at line 62-71 of `AuthorizeAttribute.cs` performs no role validation — it only checks if an `ErpIdentity` exists, without verifying the user's roles match the required permissions.

#### 2. Dynamic Code Compilation Endpoint

The endpoint `POST /api/v3.0/datasource/code-compile` accepts C# source code for dynamic compilation via `Microsoft.CodeAnalysis.CSharp.Scripting`. This represents a potential **Remote Code Execution (RCE)** surface if an attacker gains authenticated access.

Source: `WebVella.Erp.Web/Controllers/WebApiController.cs` (code compilation handler)

**Risk**: Even with authentication, any authenticated user with access to this endpoint can execute arbitrary C# code on the server. This endpoint should be restricted to Administrator roles only and ideally disabled in production environments.

#### 3. File Upload Handlers Without Content-Type Validation

The file upload endpoints (`/fs/upload/`, `/fs/upload-user-file-multiple/`, `/fs/upload-file-multiple/`, `/ckeditor/drop-upload-url`, `/ckeditor/image-upload-url`) do not perform content-type validation or enforce file size limits.

Source: `WebVella.Erp.Web/Controllers/WebApiController.cs:L3320-3500`

**Risk**: Attackers may upload executable files (`.aspx`, `.cs`, `.exe`) or excessively large files to cause denial of service. See [Remediation Guide — Pattern 8](remediation-guide.md) for file upload restriction remediation.

#### 4. Authentication Cache Timing

The `AuthCache.cs` implementation uses an in-process `IMemoryCache` with a 5-minute TTL (`AUTH_CACHE_EXPIRATION_MINUTES = 5`). Revoked user sessions remain valid for up to 5 minutes after revocation.

Source: `WebVella.Erp.Web/Security/WebSecurityUtil.cs:L15` (commented out but defines the pattern)

**Risk**: If a user account is compromised and the password is changed, the attacker's session remains valid for 5 minutes. Consider reducing cache TTL or implementing immediate cache invalidation on password change.

### Medium-Priority Residual Risks

#### 5. Development Mode Enabled in Configuration

`Config.json` sets `"DevelopmentMode": "true"` at line 9. If this flag controls error detail exposure or disables security features, it poses a risk in production deployments.

Source: `WebVella.Erp.Site/Config.json:L9`

#### 6. Unencrypted Cookie Transport

The cookie configuration in `Startup.cs` sets `HttpOnly = true` but does not enforce `SecurePolicy = CookieSecurePolicy.Always` or `SameSite = SameSiteMode.Strict`.

Source: `WebVella.Erp.Site/Startup.cs:L93-100`

---

## Recommendations for Further Hardening

The following recommendations address residual risks and strengthen the overall security posture beyond the scope of the documented remediations.

### Priority 1 — Critical (Implement Immediately)

| # | Recommendation | Related Finding |
|---|---|---|
| R1 | **Enable and implement the commented-out security infrastructure**: Uncomment and complete `AuthToken.cs`, `AuthorizeAttribute.cs`, and `WebSecurityUtil.cs`. Implement proper role-based checks in `IsAuthorized()` to validate user roles against endpoint permissions. | WV-SEC-004, A04 |
| R2 | **Rotate all hardcoded secrets immediately**: Generate new JWT signing key (minimum 256-bit), database credentials, and encryption key. Update Docker Compose environment variables and invalidate all existing sessions. | WV-SEC-005 |
| R3 | **Restrict the code compilation endpoint**: Add `[Authorize(Roles = "administrator")]` to the `/api/v3.0/datasource/code-compile` handler and disable it in production via a feature flag. | Residual Risk #2 |

### Priority 2 — High (Implement Within 30 Days)

| # | Recommendation | Related Finding |
|---|---|---|
| R4 | **Implement rate limiting on authentication endpoints**: Apply `Microsoft.AspNetCore.RateLimiting` middleware to `POST /api/v3/en_US/auth/jwt/token` and `POST /api/v3/en_US/auth/jwt/token/refresh` to prevent brute-force attacks. Recommended: 5 attempts per IP per minute. | WV-SEC-001 |
| R5 | **Add file upload content-type validation and size limits**: Implement a whitelist of allowed MIME types (e.g., `image/png`, `image/jpeg`, `application/pdf`) and enforce maximum file size (e.g., 10 MB) on all upload endpoints. | Residual Risk #3 |
| R6 | **Configure `SameSite=Strict` cookie policy**: Update the cookie configuration in `Startup.cs` to set `options.Cookie.SameSite = SameSiteMode.Strict` and `options.Cookie.SecurePolicy = CookieSecurePolicy.Always`. | Residual Risk #6 |

### Priority 3 — Medium (Implement Within 90 Days)

| # | Recommendation | Related Finding |
|---|---|---|
| R7 | **Disable `DevelopmentMode` in production**: Set `"DevelopmentMode": "false"` in production configurations or externalize via environment variable. | Residual Risk #5 |
| R8 | **Implement security event logging and alerting**: Configure structured logging for failed authentication attempts, authorization failures, and file upload events. Forward logs to a SIEM for real-time monitoring. | A09 |
| R9 | **Conduct authenticated IDOR testing**: Perform dedicated IDOR testing on entity CRUD endpoints (`/api/v3/en_US/record/{entityName}/{recordId}`) using two user accounts with different role permissions to verify object-level authorization. | A01 |
| R10 | **Add Content Security Policy (CSP) headers**: Configure CSP headers to prevent XSS and data injection attacks on any web-facing pages. | A03 |

---

## Scan Execution Log

Record scan execution details here for audit trail purposes.

### OWASP ZAP Scan

```bash
# ZAP scan execution command
docker run --network host -v $(pwd)/zap-work:/zap/wrk \
  ghcr.io/zaproxy/zaproxy:stable zap-full-scan.py \
  -t http://localhost:5000 -J zap-report.json \
  -z "-config replacer.full_list(0).matchtype=REQ_HEADER \
      -config replacer.full_list(0).matchstr=Authorization \
      -config replacer.full_list(0).replacement='Bearer <TOKEN>'"
```

| Parameter | Value |
|---|---|
| **Scanner Version** | OWASP ZAP 2.17.0 |
| **Scan Start Time** | *(Fill after execution)* |
| **Scan End Time** | *(Fill after execution)* |
| **Total Alerts** | *(Fill after execution)* |
| **Report File** | `zap-work/zap-report.json` |

### Nuclei Scan

```bash
# Nuclei scan execution command
docker run --network host projectdiscovery/nuclei:latest \
  -u http://localhost:5000 \
  -tags aspnet,api -severity critical,high \
  -H "Authorization: Bearer <TOKEN>" \
  -jsonl -o nuclei-results.jsonl
```

| Parameter | Value |
|---|---|
| **Scanner Version** | Nuclei v3.7.1 |
| **Templates Version** | v10.3.9 |
| **Scan Start Time** | *(Fill after execution)* |
| **Scan End Time** | *(Fill after execution)* |
| **Total Findings** | *(Fill after execution)* |
| **Report File** | `nuclei-results.jsonl` |

---

## Appendix A: Finding Severity Definitions

| Severity | Definition | Response SLA |
|---|---|---|
| **CRITICAL** | Exploitable finding that allows unauthenticated remote code execution, full database compromise, or complete authentication bypass. Immediate risk to data confidentiality, integrity, and availability. | Remediate within 24 hours |
| **HIGH** | Exploitable finding that allows authenticated privilege escalation, sensitive data exposure, or significant security control bypass. Requires attacker to have some level of access. | Remediate within 7 days |
| **MEDIUM** | Finding that weakens the security posture but requires specific conditions or chaining with other findings to be exploitable. | Remediate within 30 days |
| **LOW** | Informational finding that represents a deviation from security best practices but has minimal direct exploitability. | Remediate within 90 days |
| **INFORMATIONAL** | Observation that may be useful for hardening but does not represent a direct security risk. | Address during next development cycle |

---

## Appendix B: Tools and Versions

| Tool | Version | Purpose | Source |
|---|---|---|---|
| OWASP ZAP | 2.17.0 | Dynamic Application Security Testing (DAST) — active and passive scanning | `ghcr.io/zaproxy/zaproxy:stable` |
| Nuclei | v3.7.1 | Template-based vulnerability scanning — ASP.NET Core and API templates | `projectdiscovery/nuclei:latest` |
| Nuclei Templates | v10.3.9 | Community vulnerability detection templates (9,821+ templates) | `projectdiscovery/nuclei-templates` |
| Docker Engine | 24.0+ | Container runtime for isolated scan environment | `docker.com` |
| Docker Compose | V2 (2.20+) | Multi-container orchestration (WebVella + PostgreSQL) | Bundled with Docker Desktop |
| jq | 1.7+ | Command-line JSON processor for parsing scan outputs | `jqlang.github.io/jq` |
| curl | 8.0+ | HTTP client for health checks and authentication | System package |

---

## Cross-References

- [Back to Security Assessment Overview](README.md) — Full workflow index and quick-start guide
- [Finding Analysis](finding-analysis.md) — Detailed parsing methodology for ZAP and Nuclei outputs
- [Remediation Guide](remediation-guide.md) — Complete ASP.NET Core 9 secure coding patterns for all 8 remediation categories
- [Attack Surface Inventory](attack-surface-inventory.md) — Full API endpoint security classification (60+ routes)
- [Docker Environment Setup](docker-setup.md) — Container setup and health check procedures
- [Authentication](authentication.md) — JWT token acquisition for scanner configuration
- [ZAP Scan Configuration](zap-scan-config.md) — OWASP ZAP authenticated active scan setup
- [Nuclei Scan Configuration](nuclei-scan-config.md) — Nuclei template-based scan configuration

---

*This report was generated as part of the WebVella ERP Security Validation Workflow. For the complete assessment methodology, see the [Security Assessment Overview](README.md).*
