import { spawn } from 'node:child_process'
import { join } from 'node:path'
import { dotnet, dotnetEnv, root } from './dotnet.mjs'

const children = [
  spawn(dotnet, ['run', '--project', join(root, 'backend', 'EventKit.Api'), '--no-launch-profile', '--urls', 'http://127.0.0.1:5357'], {
    cwd: root, stdio: 'inherit', env: { ...dotnetEnv, ASPNETCORE_ENVIRONMENT: 'Development' },
  }),
  spawn(process.execPath, [join(root, 'frontend', 'node_modules', 'vite', 'bin', 'vite.js')], { cwd: join(root, 'frontend'), stdio: 'inherit' }),
]
console.log('\nEventKit: http://127.0.0.1:5173\nAPI: http://127.0.0.1:5357/api/health\n')
let stopping = false
function stop(code = 0) {
  if (stopping) return
  stopping = true
  for (const child of children) child.kill('SIGTERM')
  process.exitCode = code
}
for (const child of children) {
  child.on('error', error => { console.error(error.message); stop(1) })
  child.on('exit', code => { if (!stopping) stop(code ?? 1) })
}
process.on('SIGINT', () => stop())
process.on('SIGTERM', () => stop())
