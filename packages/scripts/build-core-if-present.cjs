const fs = require('node:fs')
const path = require('node:path')
const { spawnSync } = require('node:child_process')

const coreDir = path.resolve(__dirname, '..', 'identity-client-core')
if (!fs.existsSync(coreDir)) {
  process.exit(0)
}

const npmCmd = process.platform === 'win32' ? 'npm.cmd' : 'npm'
const result = spawnSync(npmCmd, ['--prefix', coreDir, 'run', 'build'], { stdio: 'inherit' })
process.exit(result.status ?? 1)

