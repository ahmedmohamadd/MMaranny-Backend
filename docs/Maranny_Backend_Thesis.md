# Maranny Backend Thesis Documentation

## Project Metadata
- **Project Name:** Maranny — Explainable AI-Powered Platform for Trusted Private Coaching and Sports Services
- **Supervisor:** Dr. Ola Khedr
- **Team Leader:** Radwa Ali
- **Prepared For:** Graduation Project Backend Documentation
- **Repository Scope:** ASP.NET Core API, application/domain layers, and SQL Server data layer

---

## 1. Introduction

The sports coaching market is rapidly expanding, yet private coaching discovery remains fragmented and trust-deficient in many local ecosystems. Prospective trainees often rely on social media pages or informal community recommendations that do not provide strong evidence of coach identity, credentials, or service quality. This causes high search friction, uncertain pricing, and weak accountability.

Maranny addresses this challenge by providing a backend platform that supports:
- secure user onboarding and role-based access,
- coach profile and verification workflows,
- training session scheduling and booking,
- chat and notifications,
- payment lifecycle management,
- product marketplace listing (display and management APIs), and
- extensible support for future AI/XAI recommendation services.

The backend is implemented using ASP.NET Core Web API with Entity Framework Core and SQL Server, while ASP.NET Identity and JWT tokens are used for authentication and authorization. The architecture is split into API, Core, Application, and Infrastructure layers to improve maintainability, testability, and future scalability.

### 1.1 Thesis Objective
This document provides a structured technical thesis for the Maranny backend subsystem, covering analysis, architecture, implementation details, and current outcomes in relation to the graduation project proposal.

### 1.2 Scope of This Thesis
This thesis focuses on the backend system currently available in the repository:
- Domain model and persistence design.
- API contracts and controller modules.
- Security and role-based access control.
- Operational concerns: notifications, chat, and payments.
- Constraints and roadmap items related to AI/XAI integration.

---

## 2. Related Work

Existing coaching and fitness platforms (e.g., Trainerize, TrueCoach, CoachUp, My PT Hub) provide partial solutions for coach-client interaction. However, many systems either:
1. prioritize fitness plan management over local coach discovery,
2. do not include strict coach verification workflows,
3. provide limited transparency around recommendation logic, or
4. are not optimized for a localized multi-role ecosystem (trainees, coaches, admins, and local service discovery).

In academic and industrial research, recommendation engines in sports and wellness applications often emphasize personalization accuracy. However, explainability and trust-centric moderation (e.g., credential verification + recommendation transparency) are less commonly integrated into a unified architecture.

Maranny differentiates itself through a combined backend model where:
- coach verification is a first-class administrative process,
- identity, booking, and communication modules are integrated in one API,
- domain entities already include recommendation and user-interaction records to support AI/XAI evolution.

---

## 3. System Analysis

### 3.1 Problem Analysis
The repository and proposal jointly frame the core problem as a trust + discovery + transaction challenge:
- users need verified coach profiles and reliable booking flows,
- coaches need manageable onboarding and profile lifecycle,
- administrators need moderation capabilities (verification, block/unblock, reporting),
- the platform requires secure identity and data integrity.

### 3.2 Stakeholders and Roles
- **Client (Trainee):** registers, browses coaches, books sessions, pays, reviews, chats.
- **Coach:** completes onboarding, manages profile and sessions, communicates with clients.
- **Admin:** verifies coaches, blocks abusive accounts, and monitors platform safety.

### 3.3 Functional Requirements (Implemented/Partially Implemented)
1. Authentication and account lifecycle: registration, login, refresh tokens, email confirmation, password reset.
2. Coach onboarding and profile setup, including optional certifications.
3. Admin verification and moderation APIs.
4. Coach search with filters (city, sport, rating, experience, gender, verification).
5. Session creation and booking workflow with overlap and capacity validation.
6. Booking status and cancellation/refund-aware payment flow.
7. Real-time and stored notifications.
8. In-app user-to-user chat with read/unread tracking.
9. Product and sports management endpoints supporting marketplace scenarios.

### 3.4 Non-Functional Requirements
- **Security:** JWT auth, role-based authorization, Identity password policies, blocked-user middleware.
- **Performance:** paginated listing endpoints, query filtering at database level.
- **Scalability:** layered architecture and clear service abstractions.
- **Maintainability:** modular projects (`Core`, `Application`, `Infrastructure`, `Api`) and EF migrations.

### 3.5 Risk and Challenge Analysis
- Real AI recommendation + explainability modules are not yet exposed in API endpoints.
- Payment gateway integration currently includes placeholders for production-grade external verification.
- Notification mapping logic assumes direct mapping between user and client IDs; this should be normalized in future refactoring.

---

## 4. System Design

### 4.1 Architectural Style
The backend follows a **layered clean-ish architecture**:

1. **Maranny.Api** — HTTP controllers, middleware pipeline, dependency injection, Swagger.
2. **Maranny.Application** — DTOs and use-case data contracts.
3. **Maranny.Core** — entities, enums, interfaces (domain contracts).
4. **Maranny.Infrastructure** — EF Core DbContext, migrations, service implementations, SignalR hubs.

This design isolates domain contracts from implementation concerns and supports future replacement of infrastructure details with minimal API-level impact.

### 4.2 Main Components
- **Identity & Auth Module:** ASP.NET Identity + JWT + Google OAuth integration.
- **Coach Discovery Module:** Search/filter endpoints with pagination and sorting.
- **Session & Booking Module:** booking validations, overlap checks, participant limits.
- **Payment Module:** initiation, status updates, refund workflow (currently mock gateway flow).
- **Notification Module:** persistent notification records + SignalR real-time dispatch.
- **Chat Module:** conversations, unread counts, read receipts.
- **Administration Module:** user blocking/unblocking and coach verification/rejection.

### 4.3 Data Design
The data model is relational and highly normalized. Key patterns include:
- one-to-one role profiles linked to `ApplicationUser` (`Admin`, `Coach`, `Client`),
- many-to-many link tables with composite keys (`ClientSession`, `CoachSport`, etc.),
- controlled deletion behavior (`Restrict`/`Cascade`) to avoid accidental integrity loss.

Entities such as `UserInteraction`, `Recommendation`, `ClientRecommendation`, and `RecommendedSport` provide a structural base for AI/XAI extensions.

### 4.4 Security Design
- JWT bearer token authentication for API protection.
- Role-based authorization on sensitive endpoints.
- password complexity and lockout policies.
- middleware-based blocked account enforcement.
- HTTPS pipeline and API documentation with bearer auth in Swagger.

---

## 5. Implementation

### 5.1 Technology Stack
- **Backend:** ASP.NET Core Web API (.NET)
- **ORM:** Entity Framework Core
- **Database:** SQL Server
- **Identity:** ASP.NET Identity (custom `ApplicationUser`)
- **Auth Tokens:** JWT + refresh tokens
- **Realtime:** SignalR (chat and notifications)
- **External Integrations:** Google OAuth, SMTP email flow, Paymob (staged/mock wiring)

### 5.2 Key Implemented Flows

#### 5.2.1 Registration and Account Confirmation
- Email validation service runs before account creation.
- Coaches are created in pending verification state.
- Email confirmation tokens are generated and sent via email service.

#### 5.2.2 Coach Onboarding and Verification
- Coach completes setup with sports, city/location, session price, and profile data.
- Admin verifies or rejects coach profiles.
- Coach role assignment is applied on successful admin approval.

#### 5.2.3 Search and Discovery
- Public endpoint supports coach search by name, sport, city, rating, experience, gender, and verification state.
- Sorting and pagination are available for scalable client-side browsing.

#### 5.2.4 Session Booking
- Client identity is validated from JWT claim.
- Session existence, status, future timing, capacity, duplicate booking, and overlap checks are enforced.
- Booking and client-session link are created atomically in the same transaction scope (`SaveChangesAsync` unit).

#### 5.2.5 Payment Lifecycle
- Payment records are initiated and stored with fee metadata.
- Completion updates booking status to confirmed.
- Refund functionality marks payment state and metadata.
- Current gateway verification is mocked pending full external integration.

#### 5.2.6 Communication and Notifications
- Chat endpoints support message send, conversation retrieval, read tracking, and unread counters.
- Notification service persists notification data and pushes real-time messages through SignalR hubs.

### 5.3 Deployment Readiness Notes
The project supports local deployment via SQL Server LocalDB and appsettings configuration. Production hardening should include:
- secure secret management,
- strict CORS policy per environment,
- robust payment callback verification,
- structured logging and observability (APM + centralized logs).

---

## 6. Results and Discussion

### 6.1 Implemented Outcome Versus Proposal
The backend currently achieves the core MVP foundation defined in the proposal:
- user authentication and profile management,
- coach onboarding and verification workflow,
- booking and payment lifecycle,
- notifications and chat communication,
- API-first architecture for Flutter integration.

### 6.2 Strengths
1. **Strong domain coverage:** entities and controllers span most required business modules.
2. **Security-aware baseline:** Identity, JWT, role-based access, and blocked-user middleware are in place.
3. **Scalable modularization:** project layering supports team collaboration and extension.
4. **Future AI readiness:** schema includes user interaction and recommendation entities.

### 6.3 Current Gaps
1. Recommendation/XAI execution pipeline is not yet exposed as a complete backend module.
2. Payment provider integration remains partially simulated.
3. Some service assumptions (e.g., client/user ID mapping in notifications) require refinement.
4. KPI instrumentation (response-time dashboards, recommendation accuracy metrics) is not yet automated.

### 6.4 Discussion on KPIs
Given current implementation status, the backend is structurally capable of tracking the proposal KPIs, but formal measurement requires:
- analytics logging around booking funnels and failures,
- SLA monitoring for response-time and uptime,
- recommendation evaluation datasets and model monitoring once AI endpoints are live.

---

## 7. Conclusion

Maranny’s backend demonstrates a robust graduation-project-grade foundation for a trusted sports coaching ecosystem. The implemented system successfully handles identity, role separation, moderation, booking, communication, and payment state management in a coherent API platform.

From an academic perspective, the project provides a strong example of translating socio-technical requirements (trust, verification, transparency) into a practical software architecture. The current codebase is sufficiently mature for MVP demonstration and integration with mobile clients.

### 7.1 Future Work
1. Implement end-to-end AI recommendation service and XAI outputs (e.g., SHAP/LIME explanations) through dedicated endpoints.
2. Complete production-grade payment integration with webhook verification and reconciliation.
3. Add observability dashboards and KPI automation.
4. Expand admin analytics and abuse detection workflows.
5. Introduce automated test suites (unit, integration, and API contract tests).

---

## Appendix A — Proposed Thesis Figure List (for printing edition)
To finalize the print-ready thesis, include the following diagrams from team assets:
- Use Case Diagram
- Context Diagram
- DFD Level 0 and Level 1
- ERD
- Activity Diagram
- Sequence Diagram
- Deployment Diagram
- Gantt Chart

## Appendix B — Suggested Chapter-to-Team Contribution Mapping
- **Backend chapters (Analysis/Design/Implementation):** Backend team members.
- **AI/XAI subsection:** Backend+AI members.
- **UI integration evidence in Results:** Flutter team + testing members.
- **UX and user-flow evidence:** UI/UX members.

