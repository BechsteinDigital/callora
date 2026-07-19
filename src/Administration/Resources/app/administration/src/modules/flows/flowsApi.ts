import { apiFetch, jsonInit, unwrap } from '@/core/http'

// Mirrors FlowSnapshot. Conditions/actions are stored as raw JSON strings on the
// backend; the create/update request instead carries them as parsed JSON values.
export interface Flow {
  id: string
  workspaceKey: string
  name: string
  triggerEvent: string
  conditionsJson: string | null
  actionsJson: string
  isActive: boolean
  priority: number
  createdAtUtc: string
}

// One page (PagedApiResponse<FlowSnapshot>). nextCursor is null on the last page.
export interface FlowsPage {
  items: Flow[]
  total: number
  nextCursor: string | null
}

// Mirrors UpsertFlowApiRequest — conditions/actions are parsed JSON (object/array
// or null), not strings.
export interface UpsertFlowInput {
  name: string
  triggerEvent: string
  conditions: unknown | null
  actions: unknown
  isActive: boolean
  priority: number
}

export const FLOWS_PAGE_SIZE = 50

const basePath = '/api/flows'

export const flowsApi = {
  async list(workspaceKey: string, cursor?: string): Promise<FlowsPage> {
    const params = new URLSearchParams({ workspaceKey, limit: String(FLOWS_PAGE_SIZE) })
    if (cursor) {
      params.set('cursor', cursor)
    }
    return (await unwrap(await apiFetch(`${basePath}?${params.toString()}`))).json()
  },

  async create(workspaceKey: string, input: UpsertFlowInput): Promise<Flow> {
    const params = new URLSearchParams({ workspaceKey })
    return (await unwrap(await apiFetch(`${basePath}?${params.toString()}`, jsonInit('POST', input)))).json()
  },

  async update(workspaceKey: string, id: string, input: UpsertFlowInput): Promise<Flow> {
    const params = new URLSearchParams({ workspaceKey })
    return (
      await unwrap(
        await apiFetch(`${basePath}/${encodeURIComponent(id)}?${params.toString()}`, jsonInit('PUT', input)),
      )
    ).json()
  },

  async remove(workspaceKey: string, id: string): Promise<void> {
    const params = new URLSearchParams({ workspaceKey })
    await unwrap(await apiFetch(`${basePath}/${encodeURIComponent(id)}?${params.toString()}`, { method: 'DELETE' }))
  },
}
