# CivicTrace

**Public procurement, followed from plan to published evidence.**

CivicTrace makes selected public construction and repair procurements in Poltava easier to understand. It brings together the procurement stage, buyer, published amounts, supplier, dates, location when safely available, and links to the original sources.

The project reports what its sources support. A missing field is shown as unknown; it is not treated as proof of misconduct or completion.

## Current scope

The prototype will start with one verified category and a small sample of real procurements. It is not a complete register of all city works and does not determine whether a procurement is corrupt.

## Team start

1. Read the [project brief](docs/PROJECT_BRIEF_UA.md).
2. Pick an open Issue and assign yourself.
3. Create a branch for the Issue; do not push feature work directly to main.
4. Open a Pull Request and link the Issue.
5. Keep every displayed fact linked to its source.

## Stack direction

- Backend: C#, ASP.NET Core Web API controllers, PostgreSQL, EF Core, Redis cache.
- Frontend: HTML, CSS, JavaScript; Leaflet for the map.
- Data: Prozorro public API and verified city open-data files. No browser-based scraping.
- Local orchestration: Docker Compose.

## Repository map

- backend/ — API and persistence.
- frontend/ — website.
- data/samples/ — small, safe, cited demo data.
- docs/ — project brief, data validation notes, decisions, and demo plan.

## Source links

- [Prozorro API documentation](https://prozorro-api-docs.readthedocs.io/en/master/tendering/basic-actions/tenders.html)
- [Poltava open-data portal](https://odata.rada-poltava.gov.ua/datasetsall)

## Frontend prototype

Run `node src/frontend/serve.mjs` from the repository root and open http://127.0.0.1:4173/. The frontend uses the backend API at http://localhost:5000. The local server provides the canonical `data/procurements.seed.json` when the API is unavailable. For cross-origin requests, allow http://127.0.0.1:4173 in `CORS_ALLOWED_ORIGINS` on the backend.

## Status

Planning and data validation. API field availability, procurement selection rules, exact locations, and links to city budget records must be confirmed from real examples before being promised in the product.
