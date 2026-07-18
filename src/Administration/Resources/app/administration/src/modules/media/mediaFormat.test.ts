import { describe, it, expect } from 'vitest'
import { formatBytes, isImageType, isAudioType } from './mediaFormat'

describe('formatBytes', () => {
  it('formats bytes, kilobytes and megabytes', () => {
    expect(formatBytes(512)).toBe('512 B')
    expect(formatBytes(1536)).toBe('1.5 KB')
    expect(formatBytes(2 * 1024 * 1024)).toBe('2.0 MB')
  })
})

describe('content type predicates', () => {
  it('detects image types', () => {
    expect(isImageType('image/png')).toBe(true)
    expect(isImageType('audio/mpeg')).toBe(false)
  })

  it('detects audio types', () => {
    expect(isAudioType('audio/wav')).toBe(true)
    expect(isAudioType('image/jpeg')).toBe(false)
  })
})
