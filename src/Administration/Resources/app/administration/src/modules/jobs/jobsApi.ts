import { apiFetch, unwrap } from '@/core/http'

// Mirrors the anonymous projection of JobEndpoints (GET /api/jobs). Read-only
// monitoring of the background job queue; workspace-bound sessions only see their
// own workspace's jobs (server-enforced).
export interface Job {
  id: string
  jobType: string
  status: string
  workspaceKey: string | null
  attemptCount: number
  maxAttempts: number
  scheduledAtUtc: string | null
  createdAtUtc: string
  startedAtUtc: string | null
  completedAtUtc: string | null
  lastError: string | null
}

const jobsPath = '/api/jobs'

export const jobsApi = {
  // The backend clamps the limit to its configured RecentListLimit.
  async list(limit?: number): Promise<Job[]> {
    const query = limit ? `?limit=${encodeURIComponent(limit)}` : ''
    return (await unwrap(await apiFetch(`${jobsPath}${query}`))).json()
  },
}
