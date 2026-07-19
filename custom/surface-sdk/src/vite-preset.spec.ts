import { describe, it, expect } from 'vitest'
import { calloraSurfacePlugin } from './vite-preset'

describe('calloraSurfacePlugin', () => {
  it('builds one IIFE bundle with fixed names and Vue external → CalloraVue', () => {
    const config = calloraSurfacePlugin({ entry: 'src/main.ts', name: 'CalloraVoip' })

    const lib = config.build?.lib
    expect(lib).toMatchObject({ entry: 'src/main.ts', formats: ['iife'], name: 'CalloraVoip' })
    expect(typeof lib === 'object' && lib.fileName).toBeTypeOf('function')
    expect(typeof lib === 'object' && typeof lib.fileName === 'function' && lib.fileName('iife', 'main')).toBe(
      'main.js',
    )

    const output = config.build?.rollupOptions?.output
    const single = Array.isArray(output) ? output[0] : output
    expect(config.build?.rollupOptions?.external).toContain('vue')
    expect(single?.globals).toEqual({ vue: 'CalloraVue' })
  })

  it('names the emitted stylesheet main.css and leaves other assets by name', () => {
    const config = calloraSurfacePlugin({ entry: 'src/main.ts', name: 'X' })
    const output = config.build?.rollupOptions?.output
    const single = Array.isArray(output) ? output[0] : output
    const assetFileNames = single?.assetFileNames as (asset: { names: string[] }) => string

    expect(assetFileNames({ names: ['style.css'] })).toBe('main.css')
    expect(assetFileNames({ names: ['logo.svg'] })).toBe('logo.svg')
  })

  it('defaults the output dir to Resources/public/<surface> and honours overrides', () => {
    expect(calloraSurfacePlugin({ entry: 'e', name: 'X' }).build?.outDir).toBe(
      'src/Resources/public/workspace',
    )
    expect(calloraSurfacePlugin({ entry: 'e', name: 'X', surface: 'admin' }).build?.outDir).toBe(
      'src/Resources/public/admin',
    )
    expect(calloraSurfacePlugin({ entry: 'e', name: 'X', outDir: 'dist/custom' }).build?.outDir).toBe(
      'dist/custom',
    )
  })
})
