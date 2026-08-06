import { describe, expect, it } from 'vitest'
import { calloraAdminPlugin } from './vite-preset'

type LibOptions = { entry: string; formats: string[]; name: string; fileName: () => string }
type Output = { globals: Record<string, string>; assetFileNames: (a: { names: string[] }) => string }

describe('calloraAdminPlugin', () => {
  it('builds one IIFE bundle with fixed names, so the asset manifest can point at it', () => {
    const config = calloraAdminPlugin({ entry: 'src/main.ts', name: 'CalloraDemoAdminUi' })
    const lib = config.build?.lib as LibOptions

    expect(lib).toMatchObject({ entry: 'src/main.ts', formats: ['iife'], name: 'CalloraDemoAdminUi' })
    expect(lib.fileName()).toBe('main.js')
  })

  it('keeps Vue external against the shell global, so a plugin never ships its own Vue', () => {
    const config = calloraAdminPlugin({ entry: 'src/main.ts', name: 'X' })
    const output = config.build?.rollupOptions?.output as Output

    expect(config.build?.rollupOptions?.external).toEqual(['vue'])
    expect(output.globals.vue).toBe('CalloraAdmin.vue')
  })

  it('outputs to the directory the host publishes for the admin surface', () => {
    expect(calloraAdminPlugin({ entry: 'src/main.ts', name: 'X' }).build?.outDir).toBe(
      'src/Resources/public/admin',
    )
  })

  it('honours an explicit output directory', () => {
    const config = calloraAdminPlugin({ entry: 'src/main.ts', name: 'X', outDir: '../../public/admin' })

    expect(config.build?.outDir).toBe('../../public/admin')
  })

  it('emits a single stylesheet named main.css', () => {
    const config = calloraAdminPlugin({ entry: 'src/main.ts', name: 'X' })
    const output = config.build?.rollupOptions?.output as Output

    expect(config.build?.cssCodeSplit).toBe(false)
    expect(output.assetFileNames({ names: ['style.css'] })).toBe('main.css')
  })

  it('leaves non-stylesheet assets under their own name', () => {
    const output = calloraAdminPlugin({ entry: 'src/main.ts', name: 'X' }).build?.rollupOptions
      ?.output as Output

    expect(output.assetFileNames({ names: ['logo.svg'] })).toBe('logo.svg')
  })

  it('empties the output directory, so a removed file cannot linger in the bundle', () => {
    expect(calloraAdminPlugin({ entry: 'src/main.ts', name: 'X' }).build?.emptyOutDir).toBe(true)
  })

  it('pins NODE_ENV to production, because a plugin bundle has no build-time environment', () => {
    expect(calloraAdminPlugin({ entry: 'src/main.ts', name: 'X' }).define).toMatchObject({
      'process.env.NODE_ENV': '"production"',
    })
  })
})
