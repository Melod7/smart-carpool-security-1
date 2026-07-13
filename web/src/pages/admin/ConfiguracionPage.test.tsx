import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { AdminSettings } from '../../api/types'
import { ConfiguracionPage, validateSettingsForm } from './ConfiguracionPage'

const getSettings = vi.hoisted(() => vi.fn())
const updateSettings = vi.hoisted(() => vi.fn())

vi.mock('../../api/admin', () => ({
  adminApi: {
    getSettings,
    updateSettings,
  },
}))

const baseSettings: AdminSettings = {
  timezone: 'America/Guayaquil',
  supportEmail: 'soporte@utn.edu.ec',
  allowedEmailDomain: 'utn.edu.ec',
  maxDailyTrips: 6,
  minDriverRating: 3.5,
  co2FactorKgKm: 0.12,
  gamificationEnabled: true,
  co2TrackingEnabled: true,
  notifySos: true,
  notifyBlock: true,
  notifyWeeklyReport: false,
}

function renderPage() {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, refetchOnWindowFocus: false },
    },
  })

  return render(
    <QueryClientProvider client={client}>
      <ConfiguracionPage />
    </QueryClientProvider>,
  )
}

describe('validateSettingsForm', () => {
  it('bloquea valores negativos y fuera de rango', () => {
    const errors = validateSettingsForm({
      timezone: 'America/Guayaquil',
      supportEmail: 'soporte@utn.edu.ec',
      allowedEmailDomain: 'utn.edu.ec',
      maxDailyTrips: '-1',
      minDriverRating: '6',
      co2FactorKgKm: '-0.5',
      gamificationEnabled: true,
      co2TrackingEnabled: true,
      notifySos: true,
      notifyBlock: false,
      notifyWeeklyReport: false,
    })

    expect(errors.maxDailyTrips).toBe('Debe ser mayor que 0.')
    expect(errors.minDriverRating).toBe('Debe estar entre 0 y 5.')
    expect(errors.co2FactorKgKm).toBe('No puede ser negativo.')
  })
})

describe('ConfiguracionPage', () => {
  beforeEach(() => {
    getSettings.mockReset()
    updateSettings.mockReset()
    getSettings.mockResolvedValue(baseSettings)
    updateSettings.mockImplementation(async (payload: AdminSettings) => payload)
  })

  it('cancela y revierte al último valor cargado cuando hay cambios', async () => {
    const user = userEvent.setup()
    renderPage()

    const maxTrips = await screen.findByLabelText('Máximo de viajes diarios por conductor')
    expect(maxTrips).toHaveValue(6)

    await user.clear(maxTrips)
    await user.type(maxTrips, '10')
    expect(maxTrips).toHaveValue(10)
    expect(screen.getByText('Hay cambios sin guardar')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Cancelar' }))

    await waitFor(() => {
      expect(screen.getByLabelText('Máximo de viajes diarios por conductor')).toHaveValue(6)
    })
    expect(screen.queryByText('Hay cambios sin guardar')).not.toBeInTheDocument()
    expect(updateSettings).not.toHaveBeenCalled()
  })

  it('muestra error inline y no guarda cuando maxDailyTrips es negativo', async () => {
    const user = userEvent.setup()
    renderPage()

    const maxTrips = await screen.findByLabelText('Máximo de viajes diarios por conductor')
    await user.clear(maxTrips)
    await user.type(maxTrips, '-3')

    await user.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText('Debe ser mayor que 0.')).toBeInTheDocument()
    expect(updateSettings).not.toHaveBeenCalled()
  })
})
