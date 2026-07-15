import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { TrackingPage } from './TrackingPage'

const getActiveTracking = vi.hoisted(() => vi.fn())

vi.mock('../../api/admin', () => ({
  adminApi: {
    getActiveTracking,
  },
}))

vi.mock('./TrackingMap', () => ({
  TrackingMap: () => <div data-testid="tracking-map">Mapa mock</div>,
}))

function renderPage(initialPath = '/admin/tracking') {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, refetchOnWindowFocus: false },
    },
  })

  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[initialPath]}>
        <TrackingPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('TrackingPage', () => {
  beforeEach(() => {
    getActiveTracking.mockReset()
  })

  it('muestra estado vacío cuando no hay viajes activos', async () => {
    getActiveTracking.mockResolvedValue({ trips: [] })

    renderPage()

    expect(await screen.findByText('No hay viajes activos para rastrear.')).toBeInTheDocument()
    expect(screen.getByText('Viajes activos')).toBeInTheDocument()
  })

  it('muestra aviso cuando falta GOOGLE_MAPS_API_KEY', async () => {
    getActiveTracking.mockResolvedValue({ trips: [] })

    renderPage()

    expect(await screen.findByText('Falta la clave de Google Maps')).toBeInTheDocument()
    expect(screen.getByText(/GOOGLE_MAPS_API_KEY/)).toBeInTheDocument()
  })
})
