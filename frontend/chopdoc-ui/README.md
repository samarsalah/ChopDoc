# ChopDoc UI

Angular 17 frontend for ChopDoc: submit a PDF conversion job, choose the output format and size limit, and browse job history with each job's status timeline and downloadable parts.

Generated with Angular CLI 17.0.9.

## Run

```bash
npm install
npm start
```

UI: http://localhost:4200 — expects the API on http://localhost:5105 (see `src/environments/environment.ts`). Start the backend first: `cd ../../backend/ChopDoc.Api && dotnet run --launch-profile http`.

## Build

```bash
npm run build      # artifacts in dist/
```

The production environment (`environment.prod.ts`) points at `/api`, assuming the UI is served behind the same origin as the API.

## Tests

```bash
npm test           # ng test (Karma + Jasmine)
```

The core logic under test lives in the backend — see [`docs/TESTING.md`](../../docs/TESTING.md) for the full test plan and the manual demo scenarios.

## Where things are

| Path | Purpose |
|------|---------|
| `src/app/pages/jobs-page` | Submit form, job list, job detail with status timeline |
| `src/app/services/job.service.ts` | API calls and part download URLs |
| `src/environments` | API base URL per configuration |

See the [root README](../../README.md) for the full solution layout and API reference.
