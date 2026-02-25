# Management API Reference

## Overview

The Management API provides endpoints for managing organizational structures including departments, role assignments, and organization-level operations within the Chronos service.

**Base Path:** `/api/management`

**Required:** Organization context (via authentication token)

---

## Department Controller

Manages department entities within an organization.

**Route Prefix:** `/api/management/department`

### Endpoints

#### Get All Departments

Retrieves all departments within the organization.

**Endpoint:** `GET /api/management/department`

**Authorization:** Required

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "name": "string",
        "organizationId": "guid",
        "createdAt": "datetime",
        "isDeleted": "boolean"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated

---

#### Get Department by ID

Retrieves a specific department by its identifier.

**Endpoint:** `GET /api/management/department/{departmentId}`

**Authorization:** Required

**Path Parameters:**

- `departmentId` (guid) - The unique identifier of the department

**Response:** `200 OK`

```json
{
    "id": "guid",
    "name": "string",
    "organizationId": "guid",
    "createdAt": "datetime",
    "lastModified": "datetime",
    "isDeleted": "boolean"
}
```

**Error Responses:**

- `400 Bad Request` - Invalid department ID format
- `401 Unauthorized` - Not authenticated
- `404 Not Found` - Department not found

---

#### Create Department

Creates a new department within the organization.

**Endpoint:** `POST /api/management/department`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Request Body:**

```json
{
    "name": "string"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "name": "string",
    "organizationId": "guid",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/management/department/{departmentId}`

**Error Responses:**

- `400 Bad Request` - Invalid department data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `409 Conflict` - Department name already exists

---

#### Update Department

Updates an existing department's information.

**Endpoint:** `PATCH /api/management/department/{departmentId}`

**Authorization:** Required - Role: `DeptRole:Administrator`

**Path Parameters:**

- `departmentId` (guid) - The unique identifier of the department

**Request Body:**

```json
{
    "name": "string"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid department ID format or data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Department not found

---

#### Delete Department

Soft deletes a department (marks for deletion).

**Endpoint:** `DELETE /api/management/department/{departmentId}`

**Authorization:** Required - Role: `DeptRole:Administrator`

**Path Parameters:**

- `departmentId` (guid) - The unique identifier of the department

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid department ID format
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Department not found

**Notes:**

- This is a soft delete operation
- Department can be restored using the restore endpoint

---

#### Restore Department

Restores a previously deleted department.

**Endpoint:** `POST /api/management/department/restore/{departmentId}`

**Authorization:** Required - Role: `DeptRole:Administrator`

**Path Parameters:**

- `departmentId` (guid) - The unique identifier of the department

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid department ID format
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Department not found

---

## Role Controller

Manages role assignments for users within departments and the organization.

**Route Prefix:** `/api/management/role`

### Endpoints

#### Get All Role Assignments

Retrieves all role assignments within the organization.

**Endpoint:** `GET /api/management/role`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "userId": "guid",
        "departmentId": "guid",
        "role": "Administrator | ResourceManager | Operator | Viewer | UserManager",
        "assignedAt": "datetime",
        "organizationId": "guid"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Role Assignments Summary

Retrieves a summary of role assignments grouped by user.

**Endpoint:** `GET /api/management/role/summary`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "userId": "guid",
        "userName": "string",
        "email": "string",
        "roleAssignments": [
            {
                "id": "guid",
                "role": "string",
                "departmentId": "guid",
                "departmentName": "string"
            }
        ]
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get User Role Assignments

Retrieves all role assignments for a specific user.

**Endpoint:** `GET /api/management/role/user/{userId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `userId` (guid) - The unique identifier of the user

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "userId": "guid",
        "departmentId": "guid",
        "role": "string",
        "assignedAt": "datetime"
    }
]
```

**Error Responses:**

- `400 Bad Request` - Invalid user ID format
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - User not found

---

#### Get Role Assignment by ID

Retrieves a specific role assignment.

**Endpoint:** `GET /api/management/role/{roleAssignmentId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `roleAssignmentId` (guid) - The unique identifier of the role assignment

**Response:** `200 OK`

```json
{
    "id": "guid",
    "userId": "guid",
    "departmentId": "guid",
    "role": "string",
    "assignedAt": "datetime",
    "organizationId": "guid"
}
```

**Error Responses:**

- `400 Bad Request` - Invalid role assignment ID format
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Role assignment not found

---

#### Add Role Assignment

Assigns a role to a user for a specific department or organization.

**Endpoint:** `POST /api/management/role`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Request Body:**

```json
{
    "userId": "guid",
    "departmentId": "guid (nullable)",
    "role": "Administrator | ResourceManager | Operator | Viewer | UserManager"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "userId": "guid",
    "departmentId": "guid",
    "role": "string",
    "assignedAt": "datetime",
    "organizationId": "guid"
}
```

**Location Header:** `/api/management/role/{roleAssignmentId}`

**Error Responses:**

- `400 Bad Request` - Invalid role assignment data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `409 Conflict` - Role assignment already exists

**Notes:**

- If `departmentId` is null, the role is assigned at the organization level
- Department-level roles override organization-level roles for that department

---

#### Remove Role Assignment

Removes a role assignment from a user.

**Endpoint:** `DELETE /api/management/role/{roleAssignmentId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `roleAssignmentId` (guid) - The unique identifier of the role assignment

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid role assignment ID format
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Role assignment not found

---

## Organization Controller

Manages organization-level operations and information.

**Route Prefix:** `/api/management/organization`

### Endpoints

#### Get Organization Information

Retrieves information about the user's current organization.

**Endpoint:** `GET /api/management/organization/info`

**Authorization:** Required

**Response:** `200 OK`

```json
{
    "organizationId": "guid",
    "name": "string",
    "createdAt": "datetime",
    "memberCount": "number",
    "departmentCount": "number",
    "isDeleted": "boolean",
    "userRole": "string"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `404 Not Found` - Organization not found

---

#### Delete Organization

Soft deletes the organization (marks for deletion).

**Endpoint:** `DELETE /api/management/organization`

**Authorization:** Required - Role: `OrgRole:Administrator`

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

**Notes:**

- This is a soft delete operation
- Organization can be restored using the restore endpoint
- All associated data is retained but marked as inactive

---

#### Restore Organization

Restores a previously deleted organization.

**Endpoint:** `POST /api/management/organization/restore`

**Authorization:** Required - Role: `OrgRole:Administrator`

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Organization not found or not deleted

---

## Role Types

The following roles are available in the system:

### Organization Roles

- **Administrator** - Full access to organization settings and all resources
- **ResourceManager** - Can manage resources, departments, and users
- **UserManager** - Can manage users and their profiles
- **Operator** - Can manage operational aspects like scheduling and constraints
- **Viewer** - Read-only access to organization data

### Department Roles

- **Administrator** - Full access to department settings and resources
- **ResourceManager** - Can manage department resources
- **Operator** - Can manage department operations
- **Viewer** - Read-only access to department data

**Notes:**

- Organization-level roles apply to all departments unless overridden
- Department-level roles only apply to the specific department
- Role hierarchy: Administrator > ResourceManager > Operator > Viewer

---

## Common Headers

All endpoints require the following headers:

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

## Best Practices

1. **Department Management:**
    - Use meaningful department names
    - Soft delete instead of hard delete to maintain data integrity
    - Restore departments if accidentally deleted

2. **Role Assignment:**
    - Assign roles at the appropriate level (organization vs. department)
    - Review role assignments regularly
    - Use the summary endpoint for auditing

3. **Organization Operations:**
    - Organization deletion is a critical operation
    - Ensure proper backup before deletion
    - Verify all stakeholders before restoring
