# Authentication API Reference

## Overview

The Authentication API provides endpoints for user registration, login, token management, and user profile operations within the Chronos service.

**Base Path:** `/api`

---

## Auth Controller

Handles authentication operations including registration, login, token refresh, and password management.

**Route Prefix:** `/api/auth`

### Endpoints

#### Register User

Creates a new customer account in the service.

**Endpoint:** `POST /api/auth/register`

**Authorization:** None (Public)

**Request Body:**

```json
{
    "email": "string",
    "password": "string",
    "firstName": "string",
    "lastName": "string"
}
```

**Response:** `200 OK`

```json
{
    "userId": "guid",
    "token": "string",
    "expiresAt": "datetime"
}
```

**Error Responses:**

- `400 Bad Request` - Invalid registration data
- `409 Conflict` - User already exists

---

#### Login

Authenticates a user and returns an access token.

**Endpoint:** `POST /api/auth/login`

**Authorization:** None (Public)

**Request Body:**

```json
{
    "email": "string",
    "password": "string"
}
```

**Response:** `200 OK`

```json
{
    "userId": "guid",
    "token": "string",
    "expiresAt": "datetime"
}
```

**Error Responses:**

- `400 Bad Request` - Invalid credentials
- `401 Unauthorized` - Authentication failed

---

#### Refresh Token

Refreshes the authentication token for the current user.

**Endpoint:** `POST /api/auth/refresh`

**Authorization:** Required (Bearer Token)

**Request Body:** None

**Response:** `200 OK`

```json
{
    "userId": "guid",
    "token": "string",
    "expiresAt": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Invalid or expired token

---

#### Verify Token

Verifies that the current authentication token is valid and not expired.

**Endpoint:** `POST /api/auth/verify`

**Authorization:** Required (Bearer Token)

**Request Body:** None

**Response:** `200 OK`

**Error Responses:**

- `401 Unauthorized` - Invalid or expired token

---

#### Update Password

Updates the password for the authenticated user.

**Endpoint:** `PUT /api/auth/password`

**Authorization:** Required (Bearer Token)

**Request Body:**

```json
{
    "oldPassword": "string",
    "newPassword": "string"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid password format or old password incorrect
- `401 Unauthorized` - Not authenticated

---

## User Controller

Manages user accounts within an organization context.

**Route Prefix:** `/api/user`

**Required:** Organization context (via `X-Organization-Id` header or similar)

### Endpoints

#### Create User

Creates a new user within the organization.

**Endpoint:** `POST /api/user`

**Authorization:** Required - Role: `OrgRole:UserManager`

**Request Body:**

```json
{
    "email": "string",
    "firstName": "string",
    "lastName": "string",
    "password": "string",
    "avatarUrl": "string (optional)"
}
```

**Response:** `201 Created`

```json
{
    "userId": "guid",
    "email": "string",
    "firstName": "string",
    "lastName": "string",
    "avatarUrl": "string",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/user/{userId}`

**Error Responses:**

- `400 Bad Request` - Invalid user data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `409 Conflict` - User already exists

---

#### Get User

Retrieves a specific user by ID within the organization.

**Endpoint:** `GET /api/user/{userId}`

**Authorization:** Required

**Path Parameters:**

- `userId` (guid) - The unique identifier of the user

**Response:** `200 OK`

```json
{
    "userId": "guid",
    "email": "string",
    "firstName": "string",
    "lastName": "string",
    "avatarUrl": "string",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `404 Not Found` - User not found

---

#### Get All Users

Retrieves all users within the organization.

**Endpoint:** `GET /api/user`

**Authorization:** Required

**Response:** `200 OK`

```json
[
    {
        "userId": "guid",
        "email": "string",
        "firstName": "string",
        "lastName": "string",
        "avatarUrl": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated

---

#### Update User Profile

Updates a user's profile information (by User Manager).

**Endpoint:** `PUT /api/user/{userId}`

**Authorization:** Required - Role: `OrgRole:UserManager`

**Path Parameters:**

- `userId` (guid) - The unique identifier of the user

**Request Body:**

```json
{
    "firstName": "string (optional)",
    "lastName": "string (optional)",
    "avatarUrl": "string (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid update data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - User not found

---

#### Update My Profile

Updates the authenticated user's own profile information.

**Endpoint:** `PUT /api/user`

**Authorization:** Required

**Request Body:**

```json
{
    "firstName": "string (optional)",
    "lastName": "string (optional)",
    "avatarUrl": "string (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid update data
- `401 Unauthorized` - Not authenticated

---

#### Delete User

Deletes a user from the organization.

**Endpoint:** `DELETE /api/user/{userId}`

**Authorization:** Required - Role: `OrgRole:UserManager`

**Path Parameters:**

- `userId` (guid) - The unique identifier of the user

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Cannot delete yourself
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - User not found

**Notes:**

- Users cannot delete themselves
- This operation may be a soft delete depending on system configuration

---

## Common Headers

All authenticated endpoints require the following headers:

```
Authorization: Bearer {token}
X-Organization-Id: {organizationId}
Content-Type: application/json
```

---

## Error Response Format

All error responses follow a consistent format:

```json
{
    "error": "string",
    "message": "string",
    "statusCode": "number",
    "timestamp": "datetime"
}
```

---

## Security Notes

1. **Password Requirements:**
    - Minimum length: 8 characters
    - Must contain at least one uppercase letter
    - Must contain at least one lowercase letter
    - Must contain at least one number
    - Must contain at least one special character

2. **Token Expiration:**
    - Access tokens expire after a configured period
    - Use the refresh endpoint to obtain a new token
    - Tokens are JWT format with user claims

3. **Rate Limiting:**
    - Authentication endpoints may be rate-limited
    - Check response headers for rate limit information

4. **Organization Context:**
    - Most user operations require an organization context
    - Provide organization ID via header or ensure proper authentication context
