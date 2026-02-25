# Resources API Reference

## Overview

The Resources API provides endpoints for managing educational resources including resources, resource types, resource attributes, subjects, and activities within the Chronos service.

**Base Path:** `/api/resources`

**Required:** Organization context (via authentication token)

---

## Resource Controller

Manages resources, resource types, resource attributes, and their assignments.

**Route Prefix:** `/api/resources/resource`

### Resource Endpoints

#### Create Resource

Creates a new resource within the organization.

**Endpoint:** `POST /api/resources/resource`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Request Body:**

```json
{
    "id": "guid (optional)",
    "organizationId": "guid",
    "resourceTypeId": "guid",
    "location": "string",
    "identifier": "string",
    "capacity": "number"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "resourceTypeId": "guid",
    "location": "string",
    "identifier": "string",
    "capacity": "number",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/resources/resource/{resourceId}`

**Error Responses:**

- `400 Bad Request` - Invalid resource data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Resource

Retrieves a specific resource by ID.

**Endpoint:** `GET /api/resources/resource/{resourceId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `resourceId` (guid) - The unique identifier of the resource

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "resourceTypeId": "guid",
    "location": "string",
    "identifier": "string",
    "capacity": "number",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found

---

#### Get All Resources

Retrieves all resources within the organization.

**Endpoint:** `GET /api/resources/resource`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "resourceTypeId": "guid",
        "location": "string",
        "identifier": "string",
        "capacity": "number",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update Resource

Updates an existing resource.

**Endpoint:** `PATCH /api/resources/resource/{resourceId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `resourceId` (guid) - The unique identifier of the resource

**Request Body:**

```json
{
    "resourceTypeId": "guid (optional)",
    "location": "string (optional)",
    "identifier": "string (optional)",
    "capacity": "number (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid resource data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found

---

#### Delete Resource

Deletes a resource from the organization.

**Endpoint:** `DELETE /api/resources/resource/{resourceId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `resourceId` (guid) - The unique identifier of the resource

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found

---

### Resource Type Endpoints

#### Create Resource Type

Creates a new resource type within the organization.

**Endpoint:** `POST /api/resources/resource/types`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Request Body:**

```json
{
    "organizationId": "guid",
    "type": "string"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "type": "string",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/resources/resource/types/{resourceTypeId}`

**Error Responses:**

- `400 Bad Request` - Invalid resource type data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `409 Conflict` - Resource type already exists

---

#### Get Resource Type

Retrieves a specific resource type by ID.

**Endpoint:** `GET /api/resources/resource/types/{resourceTypeId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `resourceTypeId` (guid) - The unique identifier of the resource type

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "type": "string",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource type not found

---

#### Get All Resource Types

Retrieves all resource types within the organization.

**Endpoint:** `GET /api/resources/resource/types`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "type": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update Resource Type

Updates an existing resource type.

**Endpoint:** `PATCH /api/resources/resource/types/{resourceTypeId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `resourceTypeId` (guid) - The unique identifier of the resource type

**Request Body:**

```json
{
    "type": "string"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid resource type data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource type not found

---

#### Delete Resource Type

Deletes a resource type from the organization.

**Endpoint:** `DELETE /api/resources/resource/types/{resourceTypeId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `resourceTypeId` (guid) - The unique identifier of the resource type

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource type not found
- `409 Conflict` - Resource type is in use

---

### Resource Attribute Endpoints

#### Create Resource Attribute

Creates a new resource attribute.

**Endpoint:** `POST /api/resources/resource/attributes`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `resourceId` (guid) - The resource identifier (from route context)

**Request Body:**

```json
{
    "organizationId": "guid",
    "title": "string",
    "description": "string"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "title": "string",
    "description": "string",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/resources/resource/attributes/{resourceAttributeId}`

**Error Responses:**

- `400 Bad Request` - Invalid attribute data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Resource Attribute

Retrieves a specific resource attribute by ID.

**Endpoint:** `GET /api/resources/resource/attributes/{resourceAttributeId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `resourceAttributeId` (guid) - The unique identifier of the resource attribute

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "title": "string",
    "description": "string",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource attribute not found

---

#### Get All Resource Attributes

Retrieves all resource attributes within the organization.

**Endpoint:** `GET /api/resources/resource/attributes`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "title": "string",
        "description": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update Resource Attribute

Updates an existing resource attribute.

**Endpoint:** `PATCH /api/resources/resource/attributes/{resourceAttributeId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `resourceId` (guid) - The resource identifier (from route context)
- `resourceAttributeId` (guid) - The unique identifier of the resource attribute

**Request Body:**

```json
{
    "title": "string (optional)",
    "description": "string (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid attribute data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource attribute not found

---

#### Delete Resource Attribute

Deletes a resource attribute.

**Endpoint:** `DELETE /api/resources/resource/attributes/{resourceAttributeId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `resourceId` (guid) - The resource identifier (from route context)
- `resourceAttributeId` (guid) - The unique identifier of the resource attribute

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource attribute not found

---

### Resource Attribute Assignment Endpoints

#### Create Resource Attribute Assignment

Assigns an attribute to a resource.

**Endpoint:** `POST /api/resources/resource/attribute-assignments`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Request Body:**

```json
{
    "resourceId": "guid",
    "resourceAttributeId": "guid",
    "organizationId": "guid"
}
```

**Response:** `201 Created`

```json
{
    "resourceId": "guid",
    "resourceAttributeId": "guid",
    "organizationId": "guid",
    "assignedAt": "datetime"
}
```

**Location Header:** `/api/resources/resource/attribute-assignments/{resourceId}/{resourceAttributeId}`

**Error Responses:**

- `400 Bad Request` - Invalid assignment data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `409 Conflict` - Assignment already exists

---

#### Get Resource Attribute Assignment

Retrieves a specific resource attribute assignment.

**Endpoint:** `GET /api/resources/resource/attribute-assignments/{resourceId}/{resourceAttributeId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `resourceId` (guid) - The unique identifier of the resource
- `resourceAttributeId` (guid) - The unique identifier of the resource attribute

**Response:** `200 OK`

```json
{
    "resourceId": "guid",
    "resourceAttributeId": "guid",
    "organizationId": "guid",
    "assignedAt": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Assignment not found

---

#### Get All Resource Attribute Assignments

Retrieves all resource attribute assignments within the organization.

**Endpoint:** `GET /api/resources/resource/attribute-assignments`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "resourceId": "guid",
        "resourceAttributeId": "guid",
        "organizationId": "guid",
        "assignedAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update Resource Attribute Assignment

Updates an existing resource attribute assignment.

**Endpoint:** `PATCH /api/resources/resource/attribute-assignments/{resourceId}/{resourceAttributeId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `resourceId` (guid) - The unique identifier of the resource
- `resourceAttributeId` (guid) - The unique identifier of the resource attribute

**Request Body:**

```json
{
    "value": "string (optional - implementation specific)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Assignment not found

---

#### Delete Resource Attribute Assignment

Removes an attribute assignment from a resource.

**Endpoint:** `DELETE /api/resources/resource/attribute-assignments/{resourceId}/{resourceAttributeId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `resourceId` (guid) - The unique identifier of the resource
- `resourceAttributeId` (guid) - The unique identifier of the resource attribute

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Assignment not found

---

## Subject Controller

Manages subjects (courses/classes) and their associated activities within departments.

**Route Prefix:** `/api/department/{departmentId}/resources/subjects/subject`

### Subject Endpoints

#### Create Subject

Creates a new subject within a department.

**Endpoint:** `POST /api/department/{departmentId}/resources/subjects/subject`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `departmentId` (guid) - The department identifier

**Request Body:**

```json
{
    "organizationId": "guid",
    "departmentId": "guid",
    "schedulingPeriodId": "guid",
    "code": "string",
    "name": "string"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "departmentId": "guid",
    "schedulingPeriodId": "guid",
    "code": "string",
    "name": "string",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/department/{departmentId}/resources/subjects/subject/{subjectId}`

**Error Responses:**

- `400 Bad Request` - Invalid subject data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `409 Conflict` - Subject code already exists

---

#### Get Subject

Retrieves a specific subject by ID.

**Endpoint:** `GET /api/department/{departmentId}/resources/subjects/subject/{subjectId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `departmentId` (guid) - The department identifier
- `subjectId` (guid) - The unique identifier of the subject

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "departmentId": "guid",
    "schedulingPeriodId": "guid",
    "code": "string",
    "name": "string",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Subject not found

---

#### Get All Subjects

Retrieves all subjects within the organization.

**Endpoint:** `GET /api/department/{departmentId}/resources/subjects/subject`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `departmentId` (guid) - The department identifier

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "departmentId": "guid",
        "schedulingPeriodId": "guid",
        "code": "string",
        "name": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update Subject

Updates an existing subject.

**Endpoint:** `PATCH /api/department/{departmentId}/resources/subjects/subject/{subjectId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `departmentId` (guid) - The department identifier
- `subjectId` (guid) - The unique identifier of the subject

**Request Body:**

```json
{
    "departmentId": "guid (optional)",
    "schedulingPeriodId": "guid (optional)",
    "code": "string (optional)",
    "name": "string (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid subject data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Subject not found

---

#### Delete Subject

Deletes a subject from the organization.

**Endpoint:** `DELETE /api/department/{departmentId}/resources/subjects/subject/{subjectId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `departmentId` (guid) - The department identifier
- `subjectId` (guid) - The unique identifier of the subject

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Subject not found
- `409 Conflict` - Subject has associated activities

---

### Activity Endpoints

#### Create Activity

Creates a new activity for a subject.

**Endpoint:** `POST /api/department/{departmentId}/resources/subjects/subject/{subjectId}/activities`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `departmentId` (guid) - The department identifier
- `subjectId` (guid) - The subject identifier

**Request Body:**

```json
{
    "organizationId": "guid",
    "subjectId": "guid",
    "assignedUserId": "guid",
    "activityType": "string",
    "expectedStudents": "number"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "subjectId": "guid",
    "assignedUserId": "guid",
    "activityType": "string",
    "expectedStudents": "number",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/department/{departmentId}/resources/subjects/subject/{subjectId}/activities/{activityId}`

**Error Responses:**

- `400 Bad Request` - Invalid activity data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Activity

Retrieves a specific activity by ID.

**Endpoint:** `GET /api/department/{departmentId}/resources/subjects/subject/{subjectId}/activities/{activityId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `departmentId` (guid) - The department identifier
- `subjectId` (guid) - The subject identifier
- `activityId` (guid) - The unique identifier of the activity

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "subjectId": "guid",
    "assignedUserId": "guid",
    "activityType": "string",
    "expectedStudents": "number",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Activity not found

---

#### Get All Activities

Retrieves all activities within the organization.

**Endpoint:** `GET /api/department/{departmentId}/resources/subjects/subject/activities`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `departmentId` (guid) - The department identifier

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "subjectId": "guid",
        "assignedUserId": "guid",
        "activityType": "string",
        "expectedStudents": "number",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Activities by Subject

Retrieves all activities for a specific subject.

**Endpoint:** `GET /api/department/{departmentId}/resources/subjects/subject/{subjectId}/activities`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `departmentId` (guid) - The department identifier
- `subjectId` (guid) - The subject identifier

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "subjectId": "guid",
        "assignedUserId": "guid",
        "activityType": "string",
        "expectedStudents": "number",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update Activity

Updates an existing activity.

**Endpoint:** `PATCH /api/department/{departmentId}/resources/subjects/subject/activities/{activityId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `departmentId` (guid) - The department identifier
- `activityId` (guid) - The unique identifier of the activity

**Request Body:**

```json
{
    "subjectId": "guid (optional)",
    "assignedUserId": "guid (optional)",
    "activityType": "string (optional)",
    "expectedStudents": "number (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid activity data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Activity not found

---

#### Delete Activity

Deletes an activity.

**Endpoint:** `DELETE /api/department/{departmentId}/resources/subjects/subject/activities/{activityId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `departmentId` (guid) - The department identifier
- `activityId` (guid) - The unique identifier of the activity

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Activity not found
- `409 Conflict` - Activity is scheduled

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

1. **Resource Management:**
    - Define resource types before creating resources
    - Use meaningful identifiers and locations
    - Set appropriate capacity values

2. **Resource Attributes:**
    - Create reusable attributes for common properties
    - Use assignments to link attributes to specific resources
    - Keep attribute descriptions clear and concise

3. **Subject Organization:**
    - Use consistent subject codes across departments
    - Associate subjects with appropriate scheduling periods
    - Organize subjects by department for better management

4. **Activity Planning:**
    - Assign activities to qualified users
    - Set realistic expected student numbers
    - Use appropriate activity types for scheduling optimization
