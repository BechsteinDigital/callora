import { describe, expect, it } from 'vitest'
import { Phone, Puzzle, Video } from 'lucide-vue-next'
import { resolvePluginIcon } from './pluginIcons'

describe('resolvePluginIcon', () => {
  it('maps a known name from the plugin vocabulary', () => {
    expect(resolvePluginIcon('phone')).toBe(Phone)
    expect(resolvePluginIcon('video')).toBe(Video)
  })

  it('matches case-insensitively — a manifest may capitalise', () => {
    expect(resolvePluginIcon('Phone')).toBe(Phone)
    expect(resolvePluginIcon('VIDEO')).toBe(Video)
  })

  it('falls back to the generic extension icon for an unknown name', () => {
    expect(resolvePluginIcon('rocket-ship')).toBe(Puzzle)
  })

  it('falls back when a plugin declares no icon at all', () => {
    expect(resolvePluginIcon(null)).toBe(Puzzle)
    expect(resolvePluginIcon('')).toBe(Puzzle)
  })
})
