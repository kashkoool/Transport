import { execFileSync } from 'node:child_process';
import { setTimeout as sleep } from 'node:timers/promises';
import * as path from 'node:path';

// Makes `npx playwright test` self-contained and repeatable: it resets the compose stack to a
// pristine DB (so the seeded trip has all seats free and no stale holds from a prior run) and waits
// for the API to report healthy before the specs run.
//
// Skip it when you already have a fresh stack, or when pointing BASE_URL at a remote/staging target:
//   E2E_SKIP_RESET=1 npx playwright test
export default async function globalSetup(): Promise<void> {
  if (process.env.E2E_SKIP_RESET === '1') return;

  const cwd = path.resolve(__dirname, '..', '..'); // repo root (this file is at tests/e2e/)
  const env = { ...process.env, POSTGRES_PASSWORD: process.env.POSTGRES_PASSWORD ?? 'change_me_local_only' };
  // Fixed argument arrays, no shell — nothing here is interpolated from external input.
  const docker = (...args: string[]) => execFileSync('docker', args, { cwd, env, stdio: 'inherit' });

  docker('compose', 'down', '-v');
  docker('compose', 'up', '-d');

  // Wait for the API container's healthcheck to go green (migrator + seed complete).
  for (let i = 0; i < 40; i++) {
    const status = execFileSync(
      'docker',
      ['inspect', '--format', '{{.State.Health.Status}}', 'tpx-api-1'],
      { cwd, env },
    ).toString().trim();
    if (status === 'healthy') return;
    await sleep(3000);
  }
  throw new Error('tpx-api-1 did not become healthy within ~120s');
}
