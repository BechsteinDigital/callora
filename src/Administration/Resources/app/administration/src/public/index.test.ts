import { describe, expect, it } from 'vitest'
import { ADMIN_PACKAGE_VERSION } from './index'

describe('@callora/admin', () => {
  it('exposes its contract version so a plugin can refuse an incompatible host', () => {
    expect(ADMIN_PACKAGE_VERSION).toMatch(/^\d+\.\d+\.\d+$/)
  })

  it('matches the version in package.json, so the published contract cannot claim another', async () => {
    const pkg = JSON.parse(
      await import('node:fs').then((fs) => fs.readFileSync(`${process.cwd()}/package.json`, 'utf8')),
    ) as { version: string }

    expect(ADMIN_PACKAGE_VERSION).toBe(pkg.version)
  })
})
