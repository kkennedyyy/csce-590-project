# Sprint 1 Demo Script (7-8 minutes)

## Demo URLs
- Frontend dashboard: `http://localhost:5173/dashboard`
- Example class detail route: `http://localhost:5173/classes/1`
- API Swagger: `http://localhost:8080/swagger`

## 7-Point Demo Script
1. **Start app and context (30s)**
   - Show repo structure (`frontend`, `backend`, `infra`).
   - State Sprint 1 scope: Student Dashboard (Use Case 1.1 + 1.2).

2. **Dashboard list view (1 min)**
   - Open `/dashboard`.
   - Show each class item contains: class name, course code, instructor, days/times, location, credits.
   - Highlight waitlisted class badge.

3. **Calendar view toggle (1 min)**
   - Toggle from list to calendar.
   - Confirm classes appear in day columns and correct time ranges.

4. **Class detail navigation (1 min)**
   - Click a class card.
   - Show detail page with professor, capacity, enrolled count, location, schedule, credits, waitlist data.

5. **API-backed proof (1 min)**
   - Open browser DevTools Network tab.
   - Refresh dashboard and show:
     - `GET /api/students/1/classes`
     - `GET /api/students/1/schedule`
   - Click class and show:
     - `GET /api/classes/{id}`

6. **Tests + CI (1.5 min)**
   - Run backend tests: `dotnet test backend/Tests/Backend.Tests.csproj`.
   - Run frontend tests: `cd frontend && npm test`.
   - Show CI workflow config: `.github/workflows/ci.yml`.

7. **Accessibility + responsive QA (1 min)**
   - Keyboard tab through list controls and open class detail with Enter.
   - Show ARIA labels on dashboard/list/calendar landmarks.
   - Resize to mobile width and confirm class cards remain readable.

## Manual QA Checklist
- Verify class list has ARIA labels and keyboard navigation opens class detail.
- Verify calendar times are consistent for the same data across refreshes/time zones.
- Verify mobile layout keeps class info readable without horizontal clipping.
- Verify waitlist badge appears for waitlisted class entries.
- Verify detail page shows waitlist list only when data exists.

## Optional Notes for Next Sprints
- Event ingestion can use Azure Functions Blob Trigger for feed processing.
- Notification workflows can use Azure Service Bus + Logic Apps for enrollment events.
