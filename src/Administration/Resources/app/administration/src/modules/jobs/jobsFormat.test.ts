import { describe, it, expect } from 'vitest'
import { formatTimestamp, statusTone } from './jobsFormat'

describe('formatTimestamp', () => {
  it('renders an em dash for null or blank', () => {
    expect(formatTimestamp(null)).toBe('—')
    expect(formatTimestamp('')).toBe('—')
  })

  it('returns the raw value when it is not a valid date', () => {
    expect(formatTimestamp('not-a-date')).toBe('not-a-date')
  })

  it('formats a valid ISO timestamp (keeps the year)', () => {
    expect(formatTimestamp('2026-07-19T10:00:00Z')).toContain('2026')
  })
})

// Anchored to the real BackgroundJobStatus enum: Pending / Running / Succeeded / Failed.
describe('statusTone', () => {
  it('maps the real Failed status (and related wordings) to danger', () => {
    expect(statusTone('Failed')).toBe('danger')
    expect(statusTone('Errored')).toBe('danger')
  })

  it('maps the real Succeeded status (and related wordings) to success', () => {
    expect(statusTone('Succeeded')).toBe('success') // the actual terminal success value
    expect(statusTone('Completed')).toBe('success')
    expect(statusTone('done')).toBe('success')
  })

  it('maps the real in-flight statuses to neutral', () => {
    expect(statusTone('Pending')).toBe('neutral')
    expect(statusTone('Running')).toBe('neutral')
  })
})
