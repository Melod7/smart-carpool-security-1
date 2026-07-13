import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { RequireAuth } from './auth/RequireAuth'
import { AppShell } from './layouts/AppShell'
import { LoginPage } from './pages/LoginPage'
import { ChangePasswordPage } from './pages/ChangePasswordPage'
import { RoleHomeRedirect } from './pages/RoleHomeRedirect'
import { ComingSoonPage } from './pages/ComingSoonPage'
import { AuditoriaPage } from './pages/admin/AuditoriaPage'
import { DashboardPage } from './pages/admin/DashboardPage'
import { ReportesPage } from './pages/admin/ReportesPage'
import { UsuariosPage } from './pages/admin/UsuariosPage'
import { StatsPage } from './pages/super/StatsPage'
import { UniversidadesPage } from './pages/super/UniversidadesPage'
import { UniversidadDetallePage } from './pages/super/UniversidadDetallePage'

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />

          <Route element={<RequireAuth allowPasswordChange />}>
            <Route path="/change-password" element={<ChangePasswordPage />} />
          </Route>

          <Route element={<RequireAuth roles={['coordinador']} />}>
            <Route path="/admin" element={<AppShell />}>
              <Route index element={<DashboardPage />} />
              <Route path="usuarios" element={<UsuariosPage />} />
              <Route path="reportes" element={<ReportesPage />} />
              <Route path="auditoria" element={<AuditoriaPage />} />
              <Route
                path="configuracion"
                element={<ComingSoonPage title="Configuración" description="Próximamente en KBX-20." />}
              />
              <Route
                path="tracking"
                element={<ComingSoonPage title="Tracking en vivo" description="Próximamente en KBX-21." />}
              />
            </Route>
          </Route>

          <Route element={<RequireAuth roles={['super_admin']} />}>
            <Route path="/super" element={<AppShell />}>
              <Route index element={<StatsPage />} />
              <Route path="universidades" element={<UniversidadesPage />} />
              <Route path="universidades/:id" element={<UniversidadDetallePage />} />
            </Route>
          </Route>

          <Route path="/" element={<RoleHomeRedirect />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
