import { apiFetch, jsonInit, unwrap } from '@/core/http'

// Mirrors WebhookSubscriptionApiResponse. The secret is write-only and never
// echoed by the API, so it is absent here by design.
export interface WebhookSubscription {
  id: string
  workspaceKey: string | null
  eventName: string
  targetUrl: string
  isActive: boolean
  includeSensitiveData: boolean
  createdAtUtc: string
}

// One page (PagedApiResponse<WebhookSubscriptionApiResponse>). nextCursor is null
// on the last page.
export interface WebhookSubscriptionsPage {
  items: WebhookSubscription[]
  total: number
  nextCursor: string | null
}

// Mirrors CreateWebhookSubscriptionApiRequest. workspaceKey null = platform-level.
export interface CreateWebhookInput {
  eventName: string
  targetUrl: string
  secret: string
  workspaceKey: string | null
  includeSensitiveData: boolean
}

export const WEBHOOKS_PAGE_SIZE = 50

const basePath = '/api/webhooks'

export const webhooksApi = {
  async list(cursor?: string): Promise<WebhookSubscriptionsPage> {
    const params = new URLSearchParams({ limit: String(WEBHOOKS_PAGE_SIZE) })
    if (cursor) {
      params.set('cursor', cursor)
    }
    return (await unwrap(await apiFetch(`${basePath}?${params.toString()}`))).json()
  },

  async create(input: CreateWebhookInput): Promise<WebhookSubscription> {
    return (await unwrap(await apiFetch(basePath, jsonInit('POST', input)))).json()
  },

  // Activation toggles via a dedicated route; isActive travels as a query flag.
  async setActive(id: string, isActive: boolean): Promise<void> {
    await unwrap(
      await apiFetch(`${basePath}/${encodeURIComponent(id)}/activation?isActive=${isActive}`, { method: 'PUT' }),
    )
  },

  async remove(id: string): Promise<void> {
    await unwrap(await apiFetch(`${basePath}/${encodeURIComponent(id)}`, { method: 'DELETE' }))
  },
}
