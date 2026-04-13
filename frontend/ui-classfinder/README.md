# ClassFinder Scheduler Frontend

React + TypeScript frontend for class registration, catalog browsing, teacher catalog browsing, and immediate schedule updates against the shared backend API.

## Routes

- `/schedule`: weekly schedule view and quick actions
- `/browse`: class catalog with search, department filtering, and enroll/disenroll
- `/teachers`: teacher catalog with search, department filtering, and class-level enroll/disenroll

## Sprint 2 Behaviors

- enroll and drop actions persist immediately
- seat counts update after enroll/drop
- active filters are shown and can be cleared
- prerequisite, overlap, capacity, and policy errors surface in the UI
- teacher catalog supports direct student enroll/disenroll actions
- mock API mirrors the production backend rules closely enough for local UI testing

## Development

```bash
npm install
npm run dev
npm run build
npm test -- --runInBand
npm run e2e
```

## Runtime Config

- `VITE_API_BASE_URL`: backend base URL. Leave empty to use the local mock service.

## Key Files

- API client and mock behavior: [`src/api/api.ts`](/home/mcs46/csce-590-project/frontend/ui-classfinder/src/api/api.ts)
- schedule state hook: [`src/hooks/useSchedule.ts`](/home/mcs46/csce-590-project/frontend/ui-classfinder/src/hooks/useSchedule.ts)
- class catalog page: [`src/pages/BrowsePage.tsx`](/home/mcs46/csce-590-project/frontend/ui-classfinder/src/pages/BrowsePage.tsx)
- teacher catalog page: [`src/pages/TeachersPage.tsx`](/home/mcs46/csce-590-project/frontend/ui-classfinder/src/pages/TeachersPage.tsx)
- schedule page: [`src/pages/SchedulePage.tsx`](/home/mcs46/csce-590-project/frontend/ui-classfinder/src/pages/SchedulePage.tsx)
