import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { AuditLogPage } from '../../api/types'
import { AuditoriaPage } from './AuditoriaPage'

const getAuditLog = vi.hoisted(() => vi.fn())

vi.mock('../../api/admin', () => ({
  adminApi: {
    getAuditLog,
  },
}))

const allPage: AuditLogPage = {
  items: [
    {
      id: 'evt-1',
      action: 'sos.fired',
      type: 'sos',
      severity: 'high',
      userId: 'user-1',
      userName: 'Sofía Pasajera',
      ip: '10.0.0.1',
      device: 'iPhone',
      createdAt: '2026-07-12T15:00:00Z',
    },
    {
      id: 'evt-2',
      action: 'user.blocked',
      type: 'admin',
      severity: 'medium',
      userId: 'user-2',
      userName: 'Coord UTN',
      ip: '10.0.0.2',
      device: 'Chrome',
      createdAt: '2026-07-12T14:00:00Z',
    },
  ],
  totalCount: 2,
  page: 1,
  pageSize: 20,
}

const highOnlyPage: AuditLogPage = {
  items: [allPage.items[0]],
  totalCount: 1,
  page: 1,
  pageSize: 20,
}

function renderPage() {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, refetchOnWindowFocus: false },
    },
  })

  return render(
    <QueryClientProvider client={client}>
      <AuditoriaPage />
    </QueryClientProvider>,
  )
}

describe('AuditoriaPage', () => {
  beforeEach(() => {
    getAuditLog.mockReset()
    getAuditLog.mockImplementation(async (filters: { severity?: string } = {}) => {
      if (filters.severity === 'high') return highOnlyPage
      return allPage
    })
  })

  it('renderiza la tabla y filtra por severidad alta', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('sos.fired')).toBeInTheDocument()
    expect(screen.getByText('user.blocked')).toBeInTheDocument()
    expect(screen.getByText('Sofía Pasajera')).toBeInTheDocument()
    expect(getAuditLog).toHaveBeenCalledWith(
      expect.objectContaining({ page: 1, pageSize: 20 }),
    )

    await user.selectOptions(screen.getByLabelText('Filtrar por severidad'), 'high')

    expect(await screen.findByText('sos.fired')).toBeInTheDocument()
    expect(screen.queryByText('user.blocked')).not.toBeInTheDocument()
    expect(getAuditLog).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'high', page: 1, pageSize: 20 }),
    )
  })
})
