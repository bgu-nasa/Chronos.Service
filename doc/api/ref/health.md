# Health & Monitoring API Reference

## Overview

The Health API provides endpoints for checking the service health status and availability. These endpoints are useful for monitoring, load balancers, and container orchestration platforms.

**Base Path:** `/api/health`

---

## Health Controller

Provides health check endpoints for service monitoring.

**Route Prefix:** `/api/health`

### Endpoints

#### Get Health Status

Retrieves the current health status of the service (public endpoint).

**Endpoint:** `GET /api/health`

**Authorization:** None (Public)

**Response:** `200 OK`

```json
{
    "responseTime": "datetime",
    "message": "string",
    "serviceInstance": "string"
}
```

**Response Fields:**

- `responseTime` - The UTC timestamp when the health check was performed
- `message` - Health status message (e.g., "Service is healthy")
- `serviceInstance` - The machine name or instance identifier running the service

**Example Response:**

```json
{
    "responseTime": "2026-01-25T13:00:00.000Z",
    "message": "Service is healthy",
    "serviceInstance": "chronos-api-01"
}
```

**Error Responses:**

- `500 Internal Server Error` - Service is experiencing issues

**Use Cases:**

- Load balancer health checks
- Container orchestration (Docker, Kubernetes) liveness probes
- Monitoring systems
- Service discovery mechanisms
- Uptime monitoring

---

#### Get Authorized Health Status

Retrieves the health status with authentication verification (tests both service health and authentication system).

**Endpoint:** `GET /api/health/test`

**Authorization:** Required (Bearer Token)

**Response:** `200 OK`

```json
{
    "responseTime": "datetime",
    "message": "string",
    "serviceInstance": "string"
}
```

**Response Fields:**

- `responseTime` - The UTC timestamp when the health check was performed
- `message` - Health status message (e.g., "Authorized service is healthy")
- `serviceInstance` - The machine name or instance identifier running the service

**Example Response:**

```json
{
    "responseTime": "2026-01-25T13:00:00.000Z",
    "message": "Authorized service is healthy",
    "serviceInstance": "chronos-api-01"
}
```

**Error Responses:**

- `401 Unauthorized` - Invalid or missing authentication token
- `500 Internal Server Error` - Service is experiencing issues

**Use Cases:**

- Testing authentication system functionality
- End-to-end health verification
- Authenticated monitoring endpoints
- Service readiness checks with auth verification

---

## Common Headers

The public health endpoint does not require headers. The authorized endpoint requires:

```
Authorization: Bearer {token}
```

---

## Health Check Best Practices

### For Monitoring Systems

1. **Polling Frequency:**
    - Use the public endpoint (`/api/health`) for frequent health checks
    - Recommended interval: 10-30 seconds for load balancers
    - Recommended interval: 1-5 minutes for monitoring dashboards

2. **Timeout Configuration:**
    - Set appropriate timeout values (recommended: 5-10 seconds)
    - Consider network latency in distributed environments

3. **Alerting:**
    - Alert on consecutive failures (e.g., 3+ failures in a row)
    - Monitor response time trends for performance degradation
    - Track service instance changes for deployment verification

### For Load Balancers

1. **Health Check Configuration:**

    ```yaml
    path: /api/health
    interval: 10s
    timeout: 5s
    healthy_threshold: 2
    unhealthy_threshold: 3
    ```

2. **Expected Response:**
    - HTTP Status: 200
    - Response time: < 1 second (typical)
    - Valid JSON response body

### For Container Orchestration

#### Docker Health Check

```dockerfile
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:80/api/health || exit 1
```

#### Kubernetes Liveness Probe

```yaml
livenessProbe:
    httpGet:
        path: /api/health
        port: 80
    initialDelaySeconds: 30
    periodSeconds: 10
    timeoutSeconds: 5
    failureThreshold: 3
```

#### Kubernetes Readiness Probe

```yaml
readinessProbe:
    httpGet:
        path: /api/health
        port: 80
    initialDelaySeconds: 10
    periodSeconds: 5
    timeoutSeconds: 3
    failureThreshold: 3
```

---

## Health Check Response Interpretation

### Healthy Service

- **HTTP Status:** 200 OK
- **Response Time:** < 1 second
- **Message:** "Service is healthy" or "Authorized service is healthy"
- **Action:** No action required

### Degraded Service

- **HTTP Status:** 200 OK
- **Response Time:** 1-5 seconds
- **Action:** Monitor for continued degradation, check system resources

### Unhealthy Service

- **HTTP Status:** 500 or no response
- **Response Time:** > 5 seconds or timeout
- **Action:** Remove from load balancer pool, trigger alerts, investigate issues

---

## Monitoring Metrics

Track these metrics from health check endpoints:

1. **Availability:**
    - Uptime percentage
    - Success rate of health checks
    - Time to recovery after failures

2. **Performance:**
    - Average response time
    - P95/P99 response time percentiles
    - Response time trends over time

3. **Service Information:**
    - Active service instances
    - Instance distribution
    - Deployment tracking via instance names

---

## Integration Examples

### cURL

```bash
# Public health check
curl http://api.chronos.example.com/api/health

# Authenticated health check
curl -H "Authorization: Bearer YOUR_TOKEN" \
     http://api.chronos.example.com/api/health/test
```

### Python

```python
import requests

# Public health check
response = requests.get('http://api.chronos.example.com/api/health')
if response.status_code == 200:
    health_data = response.json()
    print(f"Service is healthy on {health_data['serviceInstance']}")

# Authenticated health check
headers = {'Authorization': 'Bearer YOUR_TOKEN'}
response = requests.get(
    'http://api.chronos.example.com/api/health/test',
    headers=headers
)
```

### JavaScript/Node.js

```javascript
// Public health check
fetch("http://api.chronos.example.com/api/health")
    .then((response) => response.json())
    .then((data) => {
        console.log(`Service healthy: ${data.message}`);
        console.log(`Instance: ${data.serviceInstance}`);
    });

// Authenticated health check
fetch("http://api.chronos.example.com/api/health/test", {
    headers: {
        Authorization: "Bearer YOUR_TOKEN",
    },
})
    .then((response) => response.json())
    .then((data) => console.log(data));
```

---

## Troubleshooting

### Health Check Failures

**Symptom:** Health endpoint returns 500 or times out

**Possible Causes:**

1. Database connection issues
2. High server load or resource exhaustion
3. Application errors or crashes
4. Network connectivity problems
5. Configuration issues

**Resolution Steps:**

1. Check application logs for errors
2. Verify database connectivity
3. Review server resources (CPU, memory, disk)
4. Check network connectivity
5. Restart the service if necessary

### Authentication Health Check Failures

**Symptom:** `/api/health/test` returns 401 but `/api/health` works

**Possible Causes:**

1. Authentication service issues
2. Invalid or expired token
3. JWT signing key problems
4. Database authentication issues

**Resolution Steps:**

1. Verify token is valid and not expired
2. Check authentication service logs
3. Validate JWT configuration
4. Test with a freshly generated token

---

## Security Considerations

1. **Public Endpoint:**
    - The `/api/health` endpoint is intentionally public
    - Does not expose sensitive information
    - Safe for external monitoring systems

2. **Rate Limiting:**
    - Health endpoints may have separate rate limiting rules
    - Designed to handle frequent polling
    - Contact administrators for specific limits

3. **Information Disclosure:**
    - Responses include minimal information
    - Service instance names help with deployment tracking
    - No sensitive configuration or internal details exposed
