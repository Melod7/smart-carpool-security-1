import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { AuditLogPage } from '../../api/types'
import { AuditoriaPage } from './AuditoriaPage'

const getAuditLog = vi.hoisted(() => vi.fn())
const exportAuditLog = vi.hoisted(() => vi.fn())

vi.mock('../../api/admin', () => ({
  adminApi: {
    getAuditLog,
    exportAuditLog,
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
    exportAuditLog.mockReset()
    getAuditLog.mockImplementation(async (filters: { severity?: string } = {}) => {
      if (filters.severity === 'high') return highOnlyPage
      return allPage
    })
    exportAuditLog.mockResolvedValue({
      blob: new Blob(['%PDF-1.7'], { type: 'application/pdf' }),
      filename: 'kubix_auditoria_2026-07-20.pdf',
    })
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:kubix-auditoria')
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined)
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renderiza la tabla y filtra por severidad alta', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Alerta SOS activada')).toBeInTheDocument()
    expect(screen.getByText('Usuario bloqueado')).toBeInTheDocument()
    expect(screen.getByText('Sofía Pasajera')).toBeInTheDocument()
    expect(getAuditLog).toHaveBeenCalledWith(
      expect.objectContaining({ page: 1, pageSize: 20 }),
    )

    await user.selectOptions(screen.getByLabelText('Filtrar por severidad'), 'high')

    expect(await screen.findByText('Alerta SOS activada')).toBeInTheDocument()
    expect(screen.queryByText('Usuario bloqueado')).not.toBeInTheDocument()
    expect(getAuditLog).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'high', page: 1, pageSize: 20 }),
    )
  })

  it('descarga la auditoría completa en PDF con los filtros activos', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Alerta SOS activada')).toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Filtrar por severidad'), 'high')
    await user.click(screen.getByRole('button', { name: 'Exportar PDF' }))

    await waitFor(() => {
      expect(exportAuditLog).toHaveBeenCalledWith({
        type: undefined,
        severity: 'high',
      })
    })
    expect(URL.createObjectURL).toHaveBeenCalledWith(expect.any(Blob))
    expect(HTMLAnchorElement.prototype.click).toHaveBeenCalled()
  })
})
