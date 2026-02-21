# EazeCad Server — Improvement Roadmap

## Deployment & DevOps
- [ ] 1. **Dockerize the app** — Docker + docker-compose for API + PostgreSQL + Redis
- [ ] 2. **CI/CD pipeline** — GitHub Actions for build, test, and deploy on push
- [ ] 3. **Health check enhancements** — Add DB + Redis connectivity checks to `/health`

## Security Hardening
- [ ] 4. **Rate limit email endpoints** — Prevent abuse of resend-verification and forgot-password
- [ ] 5. **Refresh token rotation** — Invalidate old refresh token when a new one is issued
- [ ] 6. **CORS configuration** — Proper origin whitelisting for frontend integration

## API Quality
- [ ] 7. **Swagger/OpenAPI documentation** — Descriptions, examples, grouped endpoints
- [ ] 8. **API versioning middleware** — Formal versioning strategy
- [ ] 9. **Request validation improvements** — Consistent error response format

## Observability
- [ ] 10. **Structured logging with Serilog** — File/seq/ELK sinks, correlation IDs
- [ ] 11. **Metrics endpoint** — Prometheus-compatible `/metrics`

## Features
- [ ] 12. **Dashboard stats endpoint** — `GET /api/v1/stats` for admin overview
- [ ] 13. **Notification preferences** — User opt-in/out for email notifications
- [ ] 14. **License usage tracking** — Machine fingerprint, last seen, activation history
