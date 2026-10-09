import { spawn } from 'node:child_process'
import { dotnet, dotnetEnv, root } from './dotnet.mjs'
const child = spawn(dotnet, ['test', 'backend/EventKit.Tests', '--verbosity', 'minimal'], { cwd: root, stdio: 'inherit', env: dotnetEnv })
child.on('error', error => { console.error(error.message); process.exitCode = 1 })
child.on('exit', code => { process.exitCode = code ?? 1 })
