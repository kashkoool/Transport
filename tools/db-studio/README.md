# DB Studio (Prisma Studio for the TPX database)

A browser-based database browser — the same **Prisma Studio** you may know — pointed at this
project's PostgreSQL database. The application uses **EF Core**, not Prisma; this is purely a
dev convenience for inspecting/editing rows in the browser. It installs from **npm** (not Docker
Hub), so it works even when Docker image pulls are failing.

## Use

Make sure the database is running (`docker compose up -d postgres` from the repo root), then:

```sh
cd tools/db-studio
cp .env.example .env          # first time only (already matches the dev Postgres)
npm install                   # installs the Prisma CLI
npm run pull                  # introspect the live DB into prisma/schema.prisma
npm run studio                # opens http://localhost:5555
```

Studio opens at **<http://localhost:5555>** with the familiar URL pattern, e.g.
`http://localhost:5555/#schema=public&table=trip&view=table`. Browse `company`, `trip`,
`booking`, `payment`, `AspNetUsers`, etc.

- Re-run `npm run pull` whenever the schema changes (after a new EF migration) to refresh the
  table list.
- To use a different port: `npx prisma studio --port 5556`.
- This connects to `localhost:5433` (the compose Postgres; host 5432 is left for any
  locally-installed PostgreSQL). For a remote DB, edit `DATABASE_URL` in `.env`.
