import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { RequireAuth } from './auth/RequireAuth'
import { AppShell } from './layouts/AppShell'
import { LoginPage } from './pages/LoginPage'
import { ChangePasswordPage } from './pages/ChangePasswordPage'
import { RoleHomeRedirect } from './pages/RoleHomeRedirect'
import { ComingSoonPage } from './pages/ComingSoonPage'
import { DashboardPlaceholder } from './pages/admin/DashboardPlaceholder'
import { StatsPlaceholder } from './pages/super/StatsPlaceholder'

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
              <Route index element={<DashboardPlaceholder />} />
              <Route
                path="usuarios"
                element={<ComingSoonPage title="Gestión de Usuarios" description="Próximamente en KBX-17." />}
              />
              <Route
                path="reportes"
                element={<ComingSoonPage title="Reportes de Viajes" description="Próximamente en KBX-18." />}
              />
              <Route
                path="auditoria"
                element={<ComingSoonPage title="Auditoría de Seguridad" description="Próximamente en KBX-19." />}
              />
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
              <Route index element={<StatsPlaceholder />} />
              <Route
                path="universidades"
                element={<ComingSoonPage title="Universidades" description="Próximamente en KBX-15." />}
              />
            </Route>
          </Route>

          <Route path="/" element={<RoleHomeRedirect />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
