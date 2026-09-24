import { spawnSync } from 'node:child_process';

process.loadEnvFile(new URL('.env', import.meta.url));
const result = spawnSync('ikon', [
  'app', 'run', '--host', process.env.TAILSCALE_HOST ?? 'localhost',
  '--no-auto-frontend-login', '--',
  '--https-port', '8444', '--frontend-port', '9443',
  ...process.argv.slice(2),
], {
  cwd: new URL('IncidentCommander', import.meta.url),
  stdio: 'inherit',
  env: {
    ...process.env,
    ELKS_API_USERNAME: process.env.ELKS_API_USERNAME ?? process.env['46USER'] ?? '',
    ELKS_API_PASSWORD: process.env.ELKS_API_PASSWORD ?? process.env['46PASS'] ?? '',
  },
});
if (result.error) console.error(result.error.message);
process.exitCode = result.status ?? 1;
