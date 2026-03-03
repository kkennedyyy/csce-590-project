# ClassFinder Scheduler Frontend

Production-oriented React + TypeScript frontend for student class registration with drag-and-drop schedule editing, class browsing, conflict visualization, validation, persistence, and tests.

## Features

- Single-page app routing:
  - `/schedule` (default): drag-and-drop and keyboard-assisted schedule editor
  - `/browse`: searchable catalog with infinite-scroll style loading and quick-add
- Schedule grid:
  - Monday-Friday columns
  - Time window 08:00-20:00 in 30-minute slots (configurable)
- Validation:
  - Capacity lock (`enrolledCount >= capacity`)
  - Credit cap (19)
  - Time window enforcement with actionable hints
- Overlap handling:
  - Exact overlap detection and red-striped conflict overlays
  - Finalize disabled while conflicts exist
- Accessibility:
  - Keyboard add flows (`A` and `Enter` on class cards)
  - Focusable schedule cells and ARIA-rich labels
  - Screen-reader friendly toasts and conflict labels
- Persistence and API abstraction:
  - Mock API layer (`src/api/api.ts`)
  - localStorage-backed classes and student schedule
- Tests:
  - Unit and integration: Jest + React Testing Library
  - E2E: Playwright

## Tech Stack

- React 19 + TypeScript
- React Router
- Zustand (schedule state)
- `@dnd-kit/core` (drag and drop)
- Fuse.js (fuzzy search)
- CSS Modules + PostCSS
- Jest + RTL
- Playwright
- ESLint + Prettier

## Install

```bash
npm install
```

## Run

```bash
npm run dev
```

## Build

```bash
npm run build
```

## Test

```bash
npm test
npm run e2e
```

## Project Structure

```text
src/
  api/
    api.ts
  components/
    Header.tsx
    SearchBar.tsx
    ScheduleGrid.tsx
    DayColumn.tsx
    ClassCard.tsx
    ConflictOverlay.tsx
    BrowseList.tsx
    ClassDetailModal.tsx
    Toast.tsx
  hooks/
    useSchedule.ts
    useClasses.ts
  pages/
    SchedulePage.tsx
    BrowsePage.tsx
  store/
    scheduleStore.ts
  utils/
    time.ts
    validators.ts
    search.ts
  tests/
    unit/
    integration/
    e2e/
```

## Configuration Notes

- Max credits:
  - `src/utils/validators.ts` -> `MAX_CREDITS`
- Schedule window + slot size:
  - `src/utils/time.ts` -> `DEFAULT_TIME_WINDOW`
- Mock API behavior toggles for tests:
  - `src/api/api.ts` -> `setApiBehavior`, `resetApiBehavior`, `resetMockData`

## Mock API Surface

- `fetchClasses({ page, pageSize, search })`
- `fetchClassById(classId)`
- `fetchSchedule(studentId)`
- `registerClass({ studentId, classId, meetingTime })`
- `deregisterClass({ studentId, classId })`

These map cleanly to future backend endpoints:

- `GET /api/classes?page=1&search=...`
- `GET /api/classes/:id`
- `GET /api/schedule`
- `POST /api/schedule`
- `DELETE /api/schedule/:classId`

## Notes for Real Backend Integration

- Replace `src/api/api.ts` internals with real HTTP calls.
- Keep the function signatures unchanged to avoid UI rewrites.
- Preserve status-code semantics for capacity/credit/conflict errors.
