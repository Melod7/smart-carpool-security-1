import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { RegistrationRequest, UsuarioAdmin } from '../../api/types'
import { UsuariosPage } from './UsuariosPage'

const listRegistrationRequests = vi.hoisted(() => vi.fn())
const acceptRegistrationRequest = vi.hoisted(() => vi.fn())
const denyRegistrationRequest = vi.hoisted(() => vi.fn())
const listUsers = vi.hoisted(() => vi.fn())
const exportUsers = vi.hoisted(() => vi.fn())
const blockUser = vi.hoisted(() => vi.fn())
const unblockUser = vi.hoisted(() => vi.fn())
const listPublicUniversities = vi.hoisted(() => vi.fn())

vi.mock('../../api/admin', () => ({
  adminApi: {
    listRegistrationRequests,
    acceptRegistrationRequest,
    denyRegistrationRequest,
    listUsers,
    exportUsers,
    blockUser,
    unblockUser,
    listPublicUniversities,
  },
}))

vi.mock('../../auth/AuthContext', () => ({
  useAuth: () => ({
    user: {
      id: 'coord-1',
      email: 'coord@utn.edu.ec',
      name: 'Coordinador',
      role: 'coordinador',
      universityId: 'uni-1',
      campusId: null,
    },
  }),
}))

const pendingRequest: RegistrationRequest = {
  id: 'req-1',
  name: 'Ana Pérez',
  email: 'ana@utn.edu.ec',
  role: 'driver',
  career: 'Sistemas',
  idNumber: '1712345678',
  campusId: 'campus-1',
  campusName: 'Campus Norte',
  vehicleJson: JSON.stringify({
    makeModel: 'Chevrolet Spark',
    plate: 'PBA-1234',
    color: 'Rojo',
    seatsTotal: 3,
  }),
  status: 'pending',
  createdAt: '2026-07-12T10:00:00Z',
}

const activeUser: UsuarioAdmin = {
  id: 'user-1',
  name: 'Luis Mora',
  email: 'luis@utn.edu.ec',
  role: 'passenger',
  status: 'active',
  campusId: 'campus-1',
  career: 'Industrial',
  idNumber: '1799999999',
  ratingAvg: 4.5,
  createdAt: '2026-06-01T12:00:00Z',
}

function renderPage() {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, refetchOnWindowFocus: false },
    },
  })

  return render(
    <QueryClientProvider client={client}>
      <UsuariosPage />
    </QueryClientProvider>,
  )
}

describe('UsuariosPage', () => {
  beforeEach(() => {
    listRegistrationRequests.mockReset()
    acceptRegistrationRequest.mockReset()
    denyRegistrationRequest.mockReset()
    listUsers.mockReset()
    exportUsers.mockReset()
    blockUser.mockReset()
    unblockUser.mockReset()
    listPublicUniversities.mockReset()

    listRegistrationRequests.mockResolvedValue([pendingRequest])
    listUsers.mockImplementation(async (filters: { status?: string; pageSize?: number } = {}) => {
      if (filters.status === 'blocked') {
        return { items: [], total: 1 }
      }
      if (filters.pageSize === 1 && !filters.status) {
        return { items: [activeUser], total: 5 }
      }
      return { items: [activeUser], total: 5 }
    })
    listPublicUniversities.mockResolvedValue([
      {
        id: 'uni-1',
        name: 'UTN',
        campuses: [{ id: 'campus-1', name: 'Campus Norte' }],
      },
    ])
    acceptRegistrationRequest.mockResolvedValue(undefined)
    blockUser.mockResolvedValue(undefined)
  })

  it('filtra el directorio por rol y confirma el bloqueo', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Ana Pérez')).toBeInTheDocument()
    expect(await screen.findByText('Luis Mora')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Filtrar por rol'), 'driver')

    expect(listUsers).toHaveBeenCalledWith(
      expect.objectContaining({ role: 'driver', page: 1, pageSize: 20 }),
    )

    await user.click(screen.getByRole('button', { name: 'Bloquear' }))

    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText(/¿Bloquear a Luis Mora/)).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Confirmar bloqueo' }))

    expect(blockUser).toHaveBeenCalledWith('user-1')
  })

  it('acepta una solicitud de registro tras confirmar', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Ana Pérez')).toBeInTheDocument()

    const requestRow = screen.getByText('Ana Pérez').closest('tr')
    expect(requestRow).toBeTruthy()

    await user.click(within(requestRow as HTMLElement).getByRole('button', { name: 'Aceptar' }))

    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText(/¿Aceptar el registro de Ana Pérez/)).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Confirmar aceptación' }))

    expect(acceptRegistrationRequest).toHaveBeenCalledWith('req-1')
  })
})
