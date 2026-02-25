# Schedule API Reference

## Overview

The Schedule API provides endpoints for managing scheduling periods, time slots, resource assignments, and various constraints (activity, user, and organizational policies) within the Chronos service.

**Base Path:** `/api/schedule`

**Required:** Organization context (via authentication token)

---

## Schedule Controller

Manages scheduling periods, time slots, and assignments.

**Route Prefix:** `/api/schedule/scheduling`

### Scheduling Period Endpoints

#### Create Scheduling Period

Creates a new scheduling period within the organization.

**Endpoint:** `POST /api/schedule/scheduling/periods`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Request Body:**

```json
{
    "name": "string",
    "fromDate": "datetime",
    "toDate": "datetime"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "name": "string",
    "fromDate": "datetime",
    "toDate": "datetime",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/schedule/scheduling/periods/{schedulingPeriodId}`

**Error Responses:**

- `400 Bad Request` - Invalid period data (e.g., fromDate after toDate)
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `409 Conflict` - Overlapping scheduling period exists

---

#### Get Scheduling Period

Retrieves a specific scheduling period by ID.

**Endpoint:** `GET /api/schedule/scheduling/periods/{schedulingPeriodId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `schedulingPeriodId` (guid) - The unique identifier of the scheduling period

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "name": "string",
    "fromDate": "datetime",
    "toDate": "datetime",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Scheduling period not found

---

#### Get All Scheduling Periods

Retrieves all scheduling periods within the organization.

**Endpoint:** `GET /api/schedule/scheduling/periods`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "name": "string",
        "fromDate": "datetime",
        "toDate": "datetime",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update Scheduling Period

Updates an existing scheduling period.

**Endpoint:** `PATCH /api/schedule/scheduling/periods/{schedulingPeriodId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `schedulingPeriodId` (guid) - The unique identifier of the scheduling period

**Request Body:**

```json
{
    "name": "string (optional)",
    "fromDate": "datetime (optional)",
    "toDate": "datetime (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid period data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Scheduling period not found

---

#### Delete Scheduling Period

Deletes a scheduling period from the organization.

**Endpoint:** `DELETE /api/schedule/scheduling/periods/{schedulingPeriodId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `schedulingPeriodId` (guid) - The unique identifier of the scheduling period

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Scheduling period not found
- `409 Conflict` - Period has associated slots or constraints

---

### Slot Endpoints

#### Create Slot

Creates a new time slot within a scheduling period.

**Endpoint:** `POST /api/schedule/scheduling/slots`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Request Body:**

```json
{
    "schedulingPeriodId": "guid",
    "weekday": "number (0-6, 0=Sunday)",
    "fromTime": "timespan",
    "toTime": "timespan"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "schedulingPeriodId": "guid",
    "weekday": "number",
    "fromTime": "timespan",
    "toTime": "timespan",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/schedule/scheduling/slots/{slotId}`

**Error Responses:**

- `400 Bad Request` - Invalid slot data (e.g., fromTime after toTime)
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Slot

Retrieves a specific slot by ID.

**Endpoint:** `GET /api/schedule/scheduling/slots/{slotId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `slotId` (guid) - The unique identifier of the slot

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "schedulingPeriodId": "guid",
    "weekday": "number",
    "fromTime": "timespan",
    "toTime": "timespan",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Slot not found

---

#### Get All Slots

Retrieves all slots within the organization.

**Endpoint:** `GET /api/schedule/scheduling/slots`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "schedulingPeriodId": "guid",
        "weekday": "number",
        "fromTime": "timespan",
        "toTime": "timespan",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Slots by Scheduling Period

Retrieves all slots for a specific scheduling period.

**Endpoint:** `GET /api/schedule/scheduling/periods/{schedulingPeriodId}/slots`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `schedulingPeriodId` (guid) - The unique identifier of the scheduling period

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "schedulingPeriodId": "guid",
        "weekday": "number",
        "fromTime": "timespan",
        "toTime": "timespan",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update Slot

Updates an existing slot.

**Endpoint:** `PATCH /api/schedule/scheduling/slots/{slotId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `slotId` (guid) - The unique identifier of the slot

**Request Body:**

```json
{
    "weekday": "number (optional)",
    "fromTime": "timespan (optional)",
    "toTime": "timespan (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid slot data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Slot not found

---

#### Delete Slot

Deletes a slot from the scheduling period.

**Endpoint:** `DELETE /api/schedule/scheduling/slots/{slotId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `slotId` (guid) - The unique identifier of the slot

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Slot not found
- `409 Conflict` - Slot has assignments

---

### Assignment Endpoints

#### Create Assignment

Creates a new assignment linking a slot, resource, and activity.

**Endpoint:** `POST /api/schedule/scheduling/assignments`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Request Body:**

```json
{
    "slotId": "guid",
    "resourceId": "guid",
    "activityId": "guid"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "slotId": "guid",
    "resourceId": "guid",
    "activityId": "guid",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/schedule/scheduling/assignments/{assignmentId}`

**Error Responses:**

- `400 Bad Request` - Invalid assignment data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `409 Conflict` - Conflicting assignment exists

---

#### Get Assignment

Retrieves a specific assignment by ID.

**Endpoint:** `GET /api/schedule/scheduling/assignments/{assignmentId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `assignmentId` (guid) - The unique identifier of the assignment

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "slotId": "guid",
    "resourceId": "guid",
    "activityId": "guid",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Assignment not found

---

#### Get All Assignments

Retrieves all assignments within the organization.

**Endpoint:** `GET /api/schedule/scheduling/assignments`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "slotId": "guid",
        "resourceId": "guid",
        "activityId": "guid",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Assignments by Slot

Retrieves all assignments for a specific slot.

**Endpoint:** `GET /api/schedule/scheduling/slots/{slotId}/assignments`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `slotId` (guid) - The unique identifier of the slot

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "slotId": "guid",
        "resourceId": "guid",
        "activityId": "guid",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Assignments by Activity

Retrieves all assignments for a specific activity.

**Endpoint:** `GET /api/schedule/scheduling/activities/{activityId}/assignments`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `activityId` (guid) - The unique identifier of the activity

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "slotId": "guid",
        "resourceId": "guid",
        "activityId": "guid",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Assignment by Slot and Resource

Retrieves an assignment for a specific slot and resource combination.

**Endpoint:** `GET /api/schedule/scheduling/slots/{slotId}/resources/{resourceId}/assignment`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `slotId` (guid) - The unique identifier of the slot
- `resourceId` (guid) - The unique identifier of the resource

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "slotId": "guid",
    "resourceId": "guid",
    "activityId": "guid",
    "createdAt": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Assignment not found

---

#### Update Assignment

Updates an existing assignment.

**Endpoint:** `PATCH /api/schedule/scheduling/assignments/{assignmentId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `assignmentId` (guid) - The unique identifier of the assignment

**Request Body:**

```json
{
    "slotId": "guid (optional)",
    "resourceId": "guid (optional)",
    "activityId": "guid (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid assignment data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Assignment not found
- `409 Conflict` - Conflicting assignment exists

---

#### Delete Assignment

Deletes an assignment.

**Endpoint:** `DELETE /api/schedule/scheduling/assignments/{assignmentId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `assignmentId` (guid) - The unique identifier of the assignment

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Assignment not found

---

## Constraint Controller

Manages various types of constraints including activity constraints, user constraints, user preferences, and organization policies.

**Route Prefix:** `/api/schedule/constraints`

### Activity Constraint Endpoints

#### Create Activity Constraint

Creates a new constraint for an activity.

**Endpoint:** `POST /api/schedule/constraints/activityConstraint`

**Authorization:** Required - Role: `OrgRole:Operator`

**Request Body:**

```json
{
    "activityId": "guid",
    "key": "string",
    "value": "string"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "activityId": "guid",
    "key": "string",
    "value": "string",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/schedule/constraints/activityConstraint/{activityConstraintId}`

**Error Responses:**

- `400 Bad Request` - Invalid constraint data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Activity Constraint

Retrieves a specific activity constraint by ID.

**Endpoint:** `GET /api/schedule/constraints/activityConstraint/{activityConstraintId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `activityConstraintId` (guid) - The unique identifier of the activity constraint

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "activityId": "guid",
    "key": "string",
    "value": "string",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Activity constraint not found

---

#### Get All Activity Constraints

Retrieves all activity constraints within the organization.

**Endpoint:** `GET /api/schedule/constraints/activityConstraint`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "activityId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Activity Constraints by Activity

Retrieves all constraints for a specific activity.

**Endpoint:** `GET /api/schedule/constraints/activityConstraint/by-activity/{activityId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `activityId` (guid) - The unique identifier of the activity

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "activityId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update Activity Constraint

Updates an existing activity constraint.

**Endpoint:** `PATCH /api/schedule/constraints/activityConstraint/{activityConstraintId}`

**Authorization:** Required - Role: `OrgRole:Operator`

**Path Parameters:**

- `activityConstraintId` (guid) - The unique identifier of the activity constraint

**Request Body:**

```json
{
    "key": "string (optional)",
    "value": "string (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid constraint data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Activity constraint not found

---

#### Delete Activity Constraint

Deletes an activity constraint.

**Endpoint:** `DELETE /api/schedule/constraints/activityConstraint/{activityConstraintId}`

**Authorization:** Required - Role: `OrgRole:Operator`

**Path Parameters:**

- `activityConstraintId` (guid) - The unique identifier of the activity constraint

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Activity constraint not found

---

### User Constraint Endpoints

#### Create User Constraint

Creates a new constraint for a user within a scheduling period.

**Endpoint:** `POST /api/schedule/constraints/userConstraint`

**Authorization:** Required - Role: `OrgRole:Operator`

**Request Body:**

```json
{
    "userId": "guid",
    "schedulingPeriodId": "guid",
    "key": "string",
    "value": "string"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "userId": "guid",
    "schedulingPeriodId": "guid",
    "key": "string",
    "value": "string",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/schedule/constraints/userConstraint/{userConstraintId}`

**Error Responses:**

- `400 Bad Request` - Invalid constraint data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get User Constraint

Retrieves a specific user constraint by ID.

**Endpoint:** `GET /api/schedule/constraints/userConstraint/{userConstraintId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `userConstraintId` (guid) - The unique identifier of the user constraint

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "userId": "guid",
    "schedulingPeriodId": "guid",
    "key": "string",
    "value": "string",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - User constraint not found

---

#### Get All User Constraints

Retrieves all user constraints within the organization.

**Endpoint:** `GET /api/schedule/constraints/userConstraint`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "userId": "guid",
        "schedulingPeriodId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get User Constraints by User

Retrieves all constraints for a specific user.

**Endpoint:** `GET /api/schedule/constraints/userConstraint/by-user/{userId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `userId` (guid) - The unique identifier of the user

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "userId": "guid",
        "schedulingPeriodId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get User Constraints by Period

Retrieves all user constraints for a specific scheduling period.

**Endpoint:** `GET /api/schedule/constraints/userConstraint/by-period/{schedulingPeriodId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `schedulingPeriodId` (guid) - The unique identifier of the scheduling period

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "userId": "guid",
        "schedulingPeriodId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get User Constraints by Period and User

Retrieves all constraints for a specific user and scheduling period.

**Endpoint:** `GET /api/schedule/constraints/userConstraint/by-period-and-user/{schedulingPeriodId}/{userId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `schedulingPeriodId` (guid) - The unique identifier of the scheduling period
- `userId` (guid) - The unique identifier of the user

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "userId": "guid",
        "schedulingPeriodId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update User Constraint

Updates an existing user constraint.

**Endpoint:** `PATCH /api/schedule/constraints/userConstraint/{userConstraintId}`

**Authorization:** Required - Role: `OrgRole:Operator`

**Path Parameters:**

- `userConstraintId` (guid) - The unique identifier of the user constraint

**Request Body:**

```json
{
    "key": "string (optional)",
    "value": "string (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid constraint data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - User constraint not found

---

#### Delete User Constraint

Deletes a user constraint.

**Endpoint:** `DELETE /api/schedule/constraints/userConstraint/{userConstraintId}`

**Authorization:** Required - Role: `OrgRole:Operator`

**Path Parameters:**

- `userConstraintId` (guid) - The unique identifier of the user constraint

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - User constraint not found

---

### User Preference Endpoints

#### Create User Preference

Creates a new preference for a user within a scheduling period.

**Endpoint:** `POST /api/schedule/constraints/preferenceConstraint`

**Authorization:** Required - Role: `OrgRole:Operator`

**Request Body:**

```json
{
    "userId": "guid",
    "schedulingPeriodId": "guid",
    "key": "string",
    "value": "string"
}
```

**Response:** `201 Created`

```json
{
    "userId": "guid",
    "schedulingPeriodId": "guid",
    "organizationId": "guid",
    "key": "string",
    "value": "string",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/schedule/constraints/preferenceConstraint/{userId}/{schedulingPeriodId}/{key}`

**Error Responses:**

- `400 Bad Request` - Invalid preference data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get User Preference

Retrieves a specific user preference.

**Endpoint:** `GET /api/schedule/constraints/preferenceConstraint/{userId}/{schedulingPeriodId}/{key}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `userId` (guid) - The unique identifier of the user
- `schedulingPeriodId` (guid) - The unique identifier of the scheduling period
- `key` (string) - The preference key

**Response:** `200 OK`

```json
{
    "userId": "guid",
    "schedulingPeriodId": "guid",
    "organizationId": "guid",
    "key": "string",
    "value": "string",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - User preference not found

---

#### Get All User Preferences

Retrieves all user preferences within the organization.

**Endpoint:** `GET /api/schedule/constraints/preferenceConstraint`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "userId": "guid",
        "schedulingPeriodId": "guid",
        "organizationId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get User Preferences by User

Retrieves all preferences for a specific user.

**Endpoint:** `GET /api/schedule/constraints/preferenceConstraint/by-user/{userId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `userId` (guid) - The unique identifier of the user

**Response:** `200 OK`

```json
[
    {
        "userId": "guid",
        "schedulingPeriodId": "guid",
        "organizationId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get User Preferences by Period

Retrieves all user preferences for a specific scheduling period.

**Endpoint:** `GET /api/schedule/constraints/preferenceConstraint/by-period/{schedulingPeriodId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `schedulingPeriodId` (guid) - The unique identifier of the scheduling period

**Response:** `200 OK`

```json
[
    {
        "userId": "guid",
        "schedulingPeriodId": "guid",
        "organizationId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get User Preferences by Period and User

Retrieves all preferences for a specific user and scheduling period.

**Endpoint:** `GET /api/schedule/constraints/preferenceConstraint/by-period-and-user/{schedulingPeriodId}/{userId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `schedulingPeriodId` (guid) - The unique identifier of the scheduling period
- `userId` (guid) - The unique identifier of the user

**Response:** `200 OK`

```json
[
    {
        "userId": "guid",
        "schedulingPeriodId": "guid",
        "organizationId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update User Preference

Updates an existing user preference.

**Endpoint:** `PATCH /api/schedule/constraints/preferenceConstraint/{userId}/{schedulingPeriodId}/{key}`

**Authorization:** Required - Role: `OrgRole:Operator`

**Path Parameters:**

- `userId` (guid) - The unique identifier of the user
- `schedulingPeriodId` (guid) - The unique identifier of the scheduling period
- `key` (string) - The preference key

**Request Body:**

```json
{
    "value": "string"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid preference data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - User preference not found

---

#### Delete User Preference

Deletes a user preference.

**Endpoint:** `DELETE /api/schedule/constraints/preferenceConstraint/{userPreferenceId}`

**Authorization:** Required - Role: `OrgRole:Operator`

**Path Parameters:**

- `userPreferenceId` (guid) - The unique identifier of the user preference

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - User preference not found

---

### Organization Policy Endpoints

#### Create Organization Policy

Creates a new organization-wide policy for a scheduling period.

**Endpoint:** `POST /api/schedule/constraints/policy`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Request Body:**

```json
{
    "schedulingPeriodId": "guid",
    "key": "string",
    "value": "string"
}
```

**Response:** `201 Created`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "schedulingPeriodId": "guid",
    "key": "string",
    "value": "string",
    "createdAt": "datetime"
}
```

**Location Header:** `/api/schedule/constraints/policy/{policyId}`

**Error Responses:**

- `400 Bad Request` - Invalid policy data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Organization Policy

Retrieves a specific organization policy by ID.

**Endpoint:** `GET /api/schedule/constraints/policy/{policyId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `policyId` (guid) - The unique identifier of the organization policy

**Response:** `200 OK`

```json
{
    "id": "guid",
    "organizationId": "guid",
    "schedulingPeriodId": "guid",
    "key": "string",
    "value": "string",
    "createdAt": "datetime",
    "lastModified": "datetime"
}
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Organization policy not found

---

#### Get All Organization Policies

Retrieves all organization policies.

**Endpoint:** `GET /api/schedule/constraints/policy`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "schedulingPeriodId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Get Organization Policies by Period

Retrieves all policies for a specific scheduling period.

**Endpoint:** `GET /api/schedule/constraints/policy/by-period/{schedulingPeriodId}`

**Authorization:** Required - Role: `OrgRole:Viewer`

**Path Parameters:**

- `schedulingPeriodId` (guid) - The unique identifier of the scheduling period

**Response:** `200 OK`

```json
[
    {
        "id": "guid",
        "organizationId": "guid",
        "schedulingPeriodId": "guid",
        "key": "string",
        "value": "string",
        "createdAt": "datetime"
    }
]
```

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions

---

#### Update Organization Policy

Updates an existing organization policy.

**Endpoint:** `PATCH /api/schedule/constraints/policy/{policyId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `policyId` (guid) - The unique identifier of the organization policy

**Request Body:**

```json
{
    "key": "string (optional)",
    "value": "string (optional)"
}
```

**Response:** `204 No Content`

**Error Responses:**

- `400 Bad Request` - Invalid policy data
- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Organization policy not found

---

#### Delete Organization Policy

Deletes an organization policy.

**Endpoint:** `DELETE /api/schedule/constraints/policy/{policyId}`

**Authorization:** Required - Role: `OrgRole:ResourceManager`

**Path Parameters:**

- `policyId` (guid) - The unique identifier of the organization policy

**Response:** `204 No Content`

**Error Responses:**

- `401 Unauthorized` - Not authenticated
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Organization policy not found

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

## Weekday Values

Weekday enumeration values:

- `0` - Sunday
- `1` - Monday
- `2` - Tuesday
- `3` - Wednesday
- `4` - Thursday
- `5` - Friday
- `6` - Saturday

---

## Best Practices

1. **Scheduling Periods:**
    - Create non-overlapping periods for clarity
    - Use meaningful names (e.g., "Fall 2024", "Q1 2025")
    - Plan periods well in advance

2. **Time Slots:**
    - Define consistent slot durations
    - Avoid overlapping time slots within the same period
    - Consider break times between slots

3. **Assignments:**
    - Verify resource availability before assignment
    - Check activity requirements match resource capabilities
    - Monitor for scheduling conflicts

4. **Constraints:**
    - **Activity Constraints**: Define requirements specific to activities (e.g., required equipment)
    - **User Constraints**: Define hard restrictions for users (e.g., unavailability)
    - **User Preferences**: Define soft preferences (e.g., preferred time slots)
    - **Organization Policies**: Define organization-wide rules (e.g., maximum daily hours)

5. **Constraint Management:**
    - Use consistent key naming conventions
    - Document constraint meanings
    - Regularly review and update constraints
    - Distinguish between hard constraints and preferences
