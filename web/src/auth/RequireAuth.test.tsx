import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { RequireAuth } from '../auth/RequireAuth'
import type { AuthUser } from '../api/types'

const authState = vi.hoisted(() => ({
  value: {
    user: null as AuthUser | null,
    accessToken: null as string | null,
    isAuthenticated: false,
    mustChangePassword: false,
    isBootstrapping: false,
    login: vi.fn(),
    logout: vi.fn(),
    changePassword: vi.fn(),
    refreshUser: vi.fn(),
    hasRole: vi.fn(),
  },
}))

vi.mock('../auth/AuthContext', () => ({
  useAuth: () => authState.value,
}))

function renderGuard(initialPath: string, roles?: Array<'super_admin' | 'coordinador'>) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/login" element={<div>Página de login</div>} />
        <Route path="/admin" element={<div>Área admin</div>} />
        <Route path="/super" element={<div>Área super</div>} />
        <Route element={<RequireAuth roles={roles} />}>
          <Route path="/admin/panel" element={<div>Panel protegido</div>} />
          <Route path="/super/stats" element={<div>Stats protegidas</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('RequireAuth', () => {
  beforeEach(() => {
    authState.value = {
      user: null,
      accessToken: null,
      isAuthenticated: false,
      mustChangePassword: false,
      isBootstrapping: false,
      login: vi.fn(),
      logout: vi.fn(),
      changePassword: vi.fn(),
      refreshUser: vi.fn(),
      hasRole: vi.fn(),
    }
  })

  it('redirige a /login cuando no hay token', () => {
    renderGuard('/admin/panel', ['coordinador'])
    expect(screen.getByText('Página de login')).toBeInTheDocument()
    expect(screen.queryByText('Panel protegido')).not.toBeInTheDocument()
  })

  it('bloquea el acceso cuando el rol no coincide', () => {
    authState.value = {
      ...authState.value,
      isAuthenticated: true,
      accessToken: 'token-fake',
      user: {
        id: 'u1',
        email: 'coord@utn.edu',
        name: 'Coordinador',
        role: 'coordinador',
        universityId: 'uni-1',
        campusId: null,
      },
    }

    renderGuard('/super/stats', ['super_admin'])
    expect(screen.getByText('Área admin')).toBeInTheDocument()
    expect(screen.queryByText('Stats protegidas')).not.toBeInTheDocument()
  })
})
