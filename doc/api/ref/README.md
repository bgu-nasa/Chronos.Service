# Chronos API Reference

## Overview

The Chronos API is a comprehensive scheduling and resource management platform designed for educational institutions and organizations. This API reference provides detailed documentation for all available endpoints, organized by functional area.

**Current Version:** 1.0  
**Base URL:** `https://api.chronos.example.com`  
**Protocol:** HTTPS  
**Authentication:** Bearer Token (JWT)

---

## Quick Links

- [Authentication API](./authentication.md) - User registration, login, and authentication
- [Management API](./management.md) - Organization, department, and role management
- [Resources API](./resources.md) - Resource, subject, and activity management
- [Schedule API](./schedule.md) - Scheduling periods, slots, assignments, and constraints
- [Health API](./health.md) - Service health monitoring

---

## API Modules

### 🔐 [Authentication](./authentication.md)

Handles user authentication, registration, and profile management.

**Key Endpoints:**

- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Authenticate user
- `POST /api/auth/refresh` - Refresh authentication token
- `GET /api/user` - Get all users in organization
- `PUT /api/user/{userId}` - Update user profile

**Use Cases:**

- User onboarding and registration
- Authentication and session management
- User profile management
- Password management

---

### 🏢 [Management](./management.md)

Manages organizational structures, departments, and role-based access control.

**Key Endpoints:**

- `GET /api/management/organization/info` - Get organization information
- `GET /api/management/department` - List all departments
- `POST /api/management/department` - Create department
- `GET /api/management/role` - List role assignments
- `POST /api/management/role` - Assign role to user

**Use Cases:**

- Organizational structure setup
- Department creation and management
- Role-based access control
- User permission management

---

### 📚 [Resources](./resources.md)

Manages educational resources, subjects, activities, and their attributes.

**Key Endpoints:**

- `GET /api/resources/resource` - List all resources
- `POST /api/resources/resource` - Create resource
- `GET /api/resources/resource/types` - List resource types
- `GET /api/department/{departmentId}/resources/subjects/subject` - List subjects
- `POST /api/department/{departmentId}/resources/subjects/subject/{subjectId}/activities` - Create activity

**Use Cases:**

- Classroom and facility management
- Course/subject management
- Activity planning
- Resource allocation

---

### 📅 [Schedule](./schedule.md)

Manages scheduling periods, time slots, assignments, and various constraints.

**Key Endpoints:**

- `GET /api/schedule/scheduling/periods` - List scheduling periods
- `POST /api/schedule/scheduling/periods` - Create scheduling period
- `GET /api/schedule/scheduling/slots` - List time slots
- `POST /api/schedule/scheduling/assignments` - Create assignment
- `GET /api/schedule/constraints/activityConstraint` - List activity constraints
- `GET /api/schedule/constraints/userConstraint` - List user constraints

**Use Cases:**

- Academic term/semester planning
- Timetable creation
- Resource scheduling
- Constraint management
- Preference handling

---

### ❤️ [Health](./health.md)

Provides service health monitoring and status endpoints.

**Key Endpoints:**

- `GET /api/health` - Public health check
- `GET /api/health/test` - Authenticated health check

**Use Cases:**

- Load balancer health checks
- Monitoring and alerting
- Container orchestration probes
- Service discovery

---

## Getting Started

### Prerequisites

1. **Organization Account**: Register an organization account
2. **User Account**: Create or obtain user credentials
3. **API Access**: Obtain authentication token

### Authentication Flow

1. **Register or Login**

    ```http
    POST /api/auth/login
    Content-Type: application/json

    {
      "email": "user@example.com",
      "password": "SecurePassword123!"
    }
    ```

2. **Receive Token**

    ```json
    {
        "userId": "123e4567-e89b-12d3-a456-426614174000",
        "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        "expiresAt": "2026-01-26T13:00:00Z"
    }
    ```

3. **Use Token in Requests**
    ```http
    GET /api/user
    Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
    X-Organization-Id: 123e4567-e89b-12d3-a456-426614174001
    ```

---

## Common Concepts

### Organization Context

Most API endpoints require an organization context, provided via:

- **Authentication Token**: Contains organization ID in JWT claims
- **Header**: `X-Organization-Id` header (when applicable)

### Role-Based Access Control

The API implements fine-grained role-based access control:

**Organization Roles:**

- `Administrator` - Full access to organization
- `ResourceManager` - Manage resources and schedules
- `UserManager` - Manage users
- `Operator` - Manage operational aspects
- `Viewer` - Read-only access

**Department Roles:**

- `Administrator` - Full access to department
- `ResourceManager` - Manage department resources
- `Operator` - Manage department operations
- `Viewer` - Read-only access to department

### Data Types

**Common Field Types:**

- `guid` - UUID format (e.g., `123e4567-e89b-12d3-a456-426614174000`)
- `datetime` - ISO 8601 format (e.g., `2026-01-25T13:00:00Z`)
- `timespan` - Time duration (e.g., `14:30:00` for 2:30 PM)
- `number` - Integer or decimal
- `string` - Text data

---

## HTTP Response Codes

### Success Codes

- `200 OK` - Request successful, response body included
- `201 Created` - Resource created successfully
- `204 No Content` - Request successful, no response body

### Client Error Codes

- `400 Bad Request` - Invalid request data
- `401 Unauthorized` - Authentication required or failed
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `409 Conflict` - Resource conflict (e.g., duplicate)

### Server Error Codes

- `500 Internal Server Error` - Server-side error occurred

---

## Error Response Format

All error responses follow a consistent format:

```json
{
    "error": "BadRequest",
    "message": "Invalid email format",
    "statusCode": 400,
    "timestamp": "2026-01-25T13:00:00Z"
}
```

**Fields:**

- `error` - Error type/category
- `message` - Human-readable error description
- `statusCode` - HTTP status code
- `timestamp` - When the error occurred

---

## Rate Limiting

API requests are subject to rate limiting to ensure fair usage:

- **Default Limit**: 1000 requests per hour per user
- **Burst Limit**: 100 requests per minute
- **Health Endpoints**: Separate, higher limits for monitoring

**Rate Limit Headers:**

```http
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 987
X-RateLimit-Reset: 1706187600
```

---

## Pagination

List endpoints support pagination using query parameters:

**Query Parameters:**

- `page` - Page number (default: 1)
- `pageSize` - Items per page (default: 50, max: 100)

**Example Request:**

```http
GET /api/user?page=2&pageSize=25
```

**Response Headers:**

```http
X-Page: 2
X-Page-Size: 25
X-Total-Count: 150
X-Total-Pages: 6
```

---

## Filtering and Sorting

Some endpoints support filtering and sorting:

**Filter Parameters:**

```http
GET /api/resources/resource?type=classroom&capacity[gte]=30
```

**Sort Parameters:**

```http
GET /api/user?sort=lastName&order=asc
```

---

## Best Practices

### 1. Authentication

- Store tokens securely (encrypted storage)
- Refresh tokens before expiration
- Use HTTPS for all requests
- Never expose tokens in URLs or logs

### 2. Error Handling

- Handle all documented error codes
- Implement retry logic with exponential backoff
- Log errors for debugging
- Provide user-friendly error messages

### 3. Performance

- Use pagination for large datasets
- Cache responses when appropriate
- Minimize unnecessary API calls
- Use batch operations when available

### 4. Data Validation

- Validate data client-side before submission
- Handle validation errors gracefully
- Provide clear feedback to users

### 5. Organization Context

- Always include organization context
- Verify organization access before operations
- Handle multi-organization scenarios properly

---

## API Versioning

The API uses URL-based versioning (when applicable):

```http
https://api.chronos.example.com/v1/api/auth/login
```

**Current Version:** v1 (default, version prefix optional)

**Deprecation Policy:**

- 6 months notice for breaking changes
- Deprecated endpoints marked in documentation
- Migration guides provided

---

## SDKs and Client Libraries

Official SDKs are available for:

- JavaScript/TypeScript
- Python
- C#/.NET
- Java

See the [SDK Documentation](https://docs.chronos.example.com/sdks) for details.

---

## Webhooks

The Chronos API supports webhooks for event notifications:

**Event Types:**

- `schedule.assignment.created`
- `schedule.assignment.updated`
- `schedule.assignment.deleted`
- `user.created`
- `constraint.violation`

See the [Webhooks Guide](https://docs.chronos.example.com/webhooks) for configuration.

---

## Support and Resources

- **API Status**: [status.chronos.example.com](https://status.chronos.example.com)
- **Documentation**: [docs.chronos.example.com](https://docs.chronos.example.com)
- **Support**: support@chronos.example.com
- **Community Forum**: [community.chronos.example.com](https://community.chronos.example.com)
- **GitHub Issues**: [github.com/chronos/api](https://github.com/chronos/api)

---

## Changelog

### Version 1.0 (Current)

- Initial API release
- Authentication and user management
- Organization and department management
- Resource and subject management
- Scheduling and constraint management
- Health monitoring endpoints

---

## Legal

- [Terms of Service](https://chronos.example.com/terms)
- [Privacy Policy](https://chronos.example.com/privacy)
- [API License Agreement](https://chronos.example.com/api-license)

---

## Example Workflows

### Creating a Complete Schedule

1. **Create Scheduling Period**

    ```http
    POST /api/schedule/scheduling/periods
    ```

2. **Create Time Slots**

    ```http
    POST /api/schedule/scheduling/slots
    ```

3. **Create Subjects and Activities**

    ```http
    POST /api/department/{deptId}/resources/subjects/subject
    POST /api/department/{deptId}/resources/subjects/subject/{subjectId}/activities
    ```

4. **Add Constraints**

    ```http
    POST /api/schedule/constraints/activityConstraint
    POST /api/schedule/constraints/userConstraint
    ```

5. **Create Assignments**
    ```http
    POST /api/schedule/scheduling/assignments
    ```

### Setting Up an Organization

1. **Register Organization Owner**

    ```http
    POST /api/auth/register
    ```

2. **Create Departments**

    ```http
    POST /api/management/department
    ```

3. **Invite Users**

    ```http
    POST /api/user
    ```

4. **Assign Roles**

    ```http
    POST /api/management/role
    ```

5. **Create Resources**
    ```http
    POST /api/resources/resource/types
    POST /api/resources/resource
    ```

---

_Last Updated: January 25, 2026_
