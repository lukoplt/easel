// Cross-platform Azure DevOps task runner for easel (Node handler — works on
// Windows, Linux and macOS agents). Installs pac + easel, then runs `easel lint`.
'use strict';
const { spawnSync } = require('child_process');
const path = require('path');

function input(name, def = '') {
  return process.env['INPUT_' + name.toUpperCase()] || def;
}

const appPath = input('path', '.');
const format = input('format', 'sarif');
const failOn = input('failOn', 'warning');
const output = input('output', '');
const version = input('version', '');

const home = process.env.HOME || process.env.USERPROFILE || '';
const toolsDir = path.join(home, '.dotnet', 'tools');
process.env.PATH = toolsDir + path.delimiter + (process.env.PATH || '');

function run(cmd, args) {
  return spawnSync(cmd, args, { stdio: 'inherit' }).status;
}

// Install, falling back to update; fail the task if the tool is not usable afterwards.
function ensureTool(id, ver) {
  const install = ['tool', 'install', '--global', id];
  const update = ['tool', 'update', '--global', id];
  if (ver) { install.push('--version', ver); update.push('--version', ver); }
  if (run('dotnet', install) === 0) return;
  if (run('dotnet', update) === 0) return;
  console.error(`##vso[task.logissue type=error]Failed to install or update ${id}`);
  process.exit(1);
}

ensureTool('Microsoft.PowerApps.CLI.Tool', '');
ensureTool('EaselCli', version);

const exe = process.platform === 'win32' ? 'easel.exe' : 'easel';
const easel = path.join(toolsDir, exe);

const args = ['lint', appPath, '--format', format, '--fail-on', failOn];
if (output) args.push('--output', output);

const code = run(easel, args);
if (code !== 0) {
  console.error(`##vso[task.complete result=Failed;]easel exit ${code}`);
}
process.exit(code === null ? 1 : code);
