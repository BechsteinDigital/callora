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

describe('statusTone', () => {
  it('maps failure-like statuses to danger', () => {
    expect(statusTone('Failed')).toBe('danger')
    expect(statusTone('Dead')).toBe('danger')
    expect(statusTone('Errored')).toBe('danger')
  })

  it('maps completion-like statuses to success', () => {
    expect(statusTone('Completed')).toBe('success')
    expect(statusTone('done')).toBe('success')
  })

  it('maps everything else to neutral', () => {
    expect(statusTone('Pending')).toBe('neutral')
    expect(statusTone('Running')).toBe('neutral')
    expect(statusTone('Scheduled')).toBe('neutral')
  })
})
