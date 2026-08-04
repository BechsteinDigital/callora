/** One step of the topbar trail. The last step has no target — it is the page. */
export interface Breadcrumb {
  readonly label: string
  readonly to?: string
}
