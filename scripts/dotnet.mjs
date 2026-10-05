import { existsSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

export const root = dirname(dirname(fileURLToPath(import.meta.url)))
const portable = join(root, '.tools', 'dotnet', process.platform === 'win32' ? 'dotnet.exe' : 'dotnet')
export const dotnet = existsSync(portable) ? portable : 'dotnet'
export const dotnetEnv = { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', ...(existsSync(portable) ? { DOTNET_ROOT: dirname(portable) } : {}) }
