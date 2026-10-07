import { spawn } from 'node:child_process'
import { dotnet, dotnetEnv, root } from './dotnet.mjs'
const args = ['run', '--project', 'backend/EventKit.Api', '--no-launch-profile', '--', '--migrate']
if (process.argv.includes('--seed')) args.push('--seed')
const child = spawn(dotnet, args, { cwd: root, stdio: 'inherit', env: { ...dotnetEnv, ASPNETCORE_ENVIRONMENT: process.env.ASPNETCORE_ENVIRONMENT ?? 'Development' } })
child.on('error', error => { console.error(error.message); process.exitCode = 1 })
child.on('exit', code => { process.exitCode = code ?? 1 })
