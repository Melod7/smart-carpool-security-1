import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { AdminReport } from '../../api/types'
import { ReportesPage } from './ReportesPage'

const getReports = vi.hoisted(() => vi.fn())
const exportReports = vi.hoisted(() => vi.fn())

vi.mock('../../api/admin', () => ({
  adminApi: {
    getReports,
    exportReports,
  },
}))

const semanalReport: AdminReport = {
  period: 'semanal',
  from: '2026-07-06T05:00:00Z',
  to: '2026-07-13T04:59:59Z',
  kpis: {
    trips: 8,
    km: 120.5,
    co2Saved: 18.2,
    blockedUsers: 2,
    adoptionRate: 71.5,
  },
  trips: [
    {
      id: 'trip-1',
      status: 'completed',
      originText: 'Av. 10 de Agosto',
      departureAt: '2026-07-10T12:00:00Z',
      completedAt: '2026-07-10T12:40:00Z',
      distanceKm: 12.5,
      co2SavedKg: 1.8,
      driverName: 'Carla Ruiz',
    },
  ],
  weeklyChart: [
    { label: 'Lun', trips: 2, km: 30 },
    { label: 'Mar', trips: 3, km: 45 },
    { label: 'Mié', trips: 3, km: 45.5 },
  ],
}

const mensualReport: AdminReport = {
  ...semanalReport,
  period: 'mensual',
  from: '2026-07-01T05:00:00Z',
  to: '2026-08-01T04:59:59Z',
  kpis: {
    trips: 42,
    km: 610,
    co2Saved: 88,
    blockedUsers: 2,
    adoptionRate: 74,
  },
  trips: [
    {
      id: 'trip-2',
      status: 'completed',
      originText: 'Calle América',
      departureAt: '2026-07-02T13:00:00Z',
      completedAt: '2026-07-02T13:30:00Z',
      distanceKm: 8,
      co2SavedKg: 1.1,
      driverName: 'Diego León',
    },
  ],
  weeklyChart: [
    { label: 'Sem 1', trips: 10, km: 140 },
    { label: 'Sem 2', trips: 12, km: 160 },
  ],
}

function renderPage() {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, refetchOnWindowFocus: false },
    },
  })

  return render(
    <QueryClientProvider client={client}>
      <ReportesPage />
    </QueryClientProvider>,
  )
}

describe('ReportesPage', () => {
  beforeEach(() => {
    getReports.mockReset()
    exportReports.mockReset()
    getReports.mockImplementation(async (period: string) => {
      if (period === 'mensual') return mensualReport
      return semanalReport
    })
  })

  it('muestra KPIs desde el mock y refetch al cambiar el periodo', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Viajes')).toBeInTheDocument()
    expect(screen.getByText('8')).toBeInTheDocument()
    expect(screen.getByText('120,5 km')).toBeInTheDocument()
    expect(screen.getByText('18,2 kg')).toBeInTheDocument()
    expect(screen.getByText('Usuarios bloqueados')).toBeInTheDocument()
    expect(screen.getByText('71,5%')).toBeInTheDocument()
    expect(screen.getByText('Av. 10 de Agosto')).toBeInTheDocument()
    expect(getReports).toHaveBeenCalledWith('semanal')

    await user.selectOptions(screen.getByLabelText('Seleccionar periodo'), 'mensual')

    expect(await screen.findByText('42')).toBeInTheDocument()
    expect(screen.getByText('610 km')).toBeInTheDocument()
    expect(screen.getByText('74%')).toBeInTheDocument()
    expect(screen.getByText('Calle América')).toBeInTheDocument()
    expect(getReports).toHaveBeenCalledWith('mensual')
  })
})
