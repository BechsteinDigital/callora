import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import CalDataTable from './CalDataTable.vue'
import type { DataTableColumn } from './dataTable'

interface Row extends Record<string, unknown> {
  id: string
  name: string
  email: string | null
}

const columns: readonly DataTableColumn[] = [
  { key: 'name', label: 'Name' },
  { key: 'email', label: 'E-Mail' },
  { key: 'id', label: 'Id', mono: true },
]

const rows: Row[] = [
  { id: 'u-1', name: 'Alice', email: 'alice@example.test' },
  { id: 'u-2', name: 'Bob', email: null },
]

function mountTable(props: Record<string, unknown> = {}) {
  return mount(CalDataTable, { props: { columns, rows, rowKey: 'id', ...props } })
}

describe('CalDataTable', () => {
  it('renders one row per entry with the column values', () => {
    const wrapper = mountTable()

    const bodyRows = wrapper.findAll('tbody tr')
    expect(bodyRows).toHaveLength(2)
    expect(bodyRows[0].text()).toContain('Alice')
    expect(bodyRows[0].text()).toContain('alice@example.test')
  })

  it('renders a dash for missing values instead of an empty cell', () => {
    const wrapper = mountTable()

    expect(wrapper.findAll('tbody tr')[1].text()).toContain('—')
  })

  it('drops hidden columns from header and body alike', () => {
    const wrapper = mountTable({
      columns: [{ key: 'name', label: 'Name' }, { key: 'email', label: 'E-Mail', hidden: true }],
    })

    expect(wrapper.findAll('thead th')).toHaveLength(1)
    expect(wrapper.findAll('tbody tr')[0].findAll('td')).toHaveLength(1)
    expect(wrapper.text()).not.toContain('alice@example.test')
  })

  it('shows placeholder rows instead of data while loading', () => {
    const wrapper = mountTable({ loading: true, skeletonRowCount: 3 })

    expect(wrapper.findAll('.cal-table__skeleton-row')).toHaveLength(3)
    expect(wrapper.text()).not.toContain('Alice')
  })

  it('shows the empty state only when there is nothing to show and no error', () => {
    const wrapper = mountTable({ rows: [], emptyTitle: 'Keine Benutzer vorhanden.' })

    expect(wrapper.text()).toContain('Keine Benutzer vorhanden.')
  })

  it('shows the error instead of the empty state when loading failed', () => {
    const wrapper = mountTable({ rows: [], error: 'Verbindung verweigert', emptyTitle: 'Keine Benutzer.' })

    expect(wrapper.text()).toContain('Verbindung verweigert')
    expect(wrapper.text()).not.toContain('Keine Benutzer.')
  })

  it('suppresses the empty state while still loading', () => {
    const wrapper = mountTable({ rows: [], loading: true, emptyTitle: 'Keine Benutzer.' })

    expect(wrapper.text()).not.toContain('Keine Benutzer.')
  })

  it('lets a caller override a cell through the per-column slot', () => {
    const wrapper = mount(CalDataTable, {
      props: { columns, rows, rowKey: 'id' },
      slots: { 'cell-name': '<template #cell-name="{ row }"><b>{{ row.name }}!</b></template>' },
    })

    expect(wrapper.find('tbody b').text()).toBe('Alice!')
  })

  it('derives row keys through a function when identity is composite', () => {
    const wrapper = mountTable({ rowKey: (row: Row) => `${row.id}-${row.name}` })

    expect(wrapper.findAll('tbody tr')).toHaveLength(2)
  })
})
