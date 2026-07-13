import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { AdminDashboard } from '../../api/types'
import { DashboardPage } from './DashboardPage'

const getDashboard = vi.hoisted(() => vi.fn())

vi.mock('../../api/admin', () => ({
  adminApi: {
    getDashboard,
    listSos: vi.fn(),
    resolveSos: vi.fn(),
  },
}))

function renderPage() {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, refetchOnWindowFocus: false },
    },
  })

  return render(
    <QueryClientProvider client={client}>
      <DashboardPage />
    </QueryClientProvider>,
  )
}

const baseDashboard: AdminDashboard = {
  tripsToday: 12,
  blockedUsers: 3,
  co2Saved: 45.5,
  adoptionRate: 67.25,
  adoptionRateBasis: 'active_users_over_total',
  activeSos: [],
  xpByCareer: [
    { career: 'Sistemas', xp: 120 },
    { career: 'Industrial', xp: 80 },
  ],
  gamificationEnabled: true,
  xpByCareerNote: null,
}

describe('DashboardPage', () => {
  beforeEach(() => {
    getDashboard.mockReset()
  })

  it('renderiza los KPI desde los datos del dashboard', async () => {
    getDashboard.mockResolvedValue(baseDashboard)

    renderPage()

    expect(await screen.findByText('Viajes hoy')).toBeInTheDocument()
    expect(screen.getByText('12')).toBeInTheDocument()
    expect(screen.getByText('Usuarios bloqueados')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText('CO₂ ahorrado')).toBeInTheDocument()
    expect(screen.getByText('45,5 kg')).toBeInTheDocument()
    expect(screen.getByText('Tasa de adopción')).toBeInTheDocument()
    expect(screen.getByText('67,25%')).toBeInTheDocument()
  })

  it('muestra el estado deshabilitado cuando la gamificación está off', async () => {
    getDashboard.mockResolvedValue({
      ...baseDashboard,
      gamificationEnabled: false,
      xpByCareer: [],
      xpByCareerNote: 'Gamification is disabled for this university; xpByCareer is empty.',
    })

    renderPage()

    expect(await screen.findByText('Gamificación deshabilitada')).toBeInTheDocument()
    expect(
      screen.getByText('Gamification is disabled for this university; xpByCareer is empty.'),
    ).toBeInTheDocument()
    expect(screen.queryByText('Sistemas')).not.toBeInTheDocument()
  })
})
