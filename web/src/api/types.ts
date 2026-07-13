export type UserRole = 'super_admin' | 'coordinador'

export type AuthUser = {
  id: string
  email: string
  name: string
  role: UserRole
  universityId: string | null
  campusId: string | null
  mustChangePassword?: boolean | null
  status?: string | null
}

export type AuthResponse = {
  accessToken: string
  refreshToken: string
  expiresIn: number
  mustChangePassword: boolean
  user: AuthUser
}

export type AdminNotification = {
  id: string
  type: string
  title: string
  body: string
  read: boolean
  createdAt: string
}

export type SuperStats = {
  universitiesCount: number
  totalUsers: number
  tripsToday: number
  activeSosCount: number
}

export type UniversitySummary = {
  id: string
  name: string
  slug: string
  status: string
  campusesCount: number
  usersCount: number
}

export type UniversityDetail = {
  id: string
  name: string
  slug: string
  status: string
  allowedEmailDomain?: string | null
}

export type CreateUniversityPayload = {
  name: string
  slug: string
  allowedEmailDomain?: string
}

export type UpdateUniversityPayload = {
  name: string
  slug: string
}

export type Campus = {
  id: string
  universityId: string
  name: string
  address: string
  lat: number
  lng: number
}

export type CampusPayload = {
  name: string
  address: string
  lat: number
  lng: number
}

export type Coordinador = {
  id: string
  name: string
  email: string
  status: string
  mustChangePassword: boolean
  universityId: string | null
}

export type CoordinadorCreado = Coordinador & {
  temporaryPassword: string
}

export type CreateCoordinadorPayload = {
  name: string
  email: string
}

export type ResetPasswordResponse = {
  id: string
  email: string
  temporaryPassword: string
  mustChangePassword: boolean
}

export type SosParticipante = {
  id: string
  name: string
  email: string
  role: string
}

export type SosViaje = {
  id: string
  status: string
  originText: string
  departureAt: string
}

export type AlertaSosAdmin = {
  id: string
  status: string
  lat: number
  lng: number
  firedAt: string
  resolvedBy?: string | null
  resolvedAt?: string | null
  student?: SosParticipante | null
  trip?: SosViaje | null
  driver?: SosParticipante | null
}

export type XpPorCarrera = {
  career: string
  xp: number
}

export type AdminDashboard = {
  tripsToday: number
  blockedUsers: number
  co2Saved: number
  adoptionRate: number
  adoptionRateBasis?: string
  activeSos: AlertaSosAdmin[]
  xpByCareer: XpPorCarrera[]
  gamificationEnabled: boolean
  xpByCareerNote?: string | null
}

export type RegistrationRequest = {
  id: string
  name: string
  email: string
  role: string
  career?: string | null
  idNumber?: string | null
  campusId: string
  campusName?: string | null
  vehicleJson?: string | null
  status: string
  createdAt: string
}

export type UsuarioAdmin = {
  id: string
  name: string
  email: string
  role: string
  status: string
  campusId?: string | null
  career?: string | null
  idNumber?: string | null
  ratingAvg: number
  createdAt: string
}

export type AdminUsersFilter = {
  status?: string
  role?: string
  campus?: string
  search?: string
  page?: number
  pageSize?: number
}

export type AdminUsersPage = {
  items: UsuarioAdmin[]
  total: number
}

export type PublicCampus = {
  id: string
  name: string
}

export type PublicUniversity = {
  id: string
  name: string
  campuses: PublicCampus[]
}

export type VehicleJson = {
  makeModel?: string
  plate?: string
  color?: string
  seatsTotal?: number
}

export type ReportPeriod = 'diario' | 'semanal' | 'mensual' | 'trimestral' | 'anual'

export type ReportExportFormat = 'csv' | 'xlsx' | 'pdf'

export type ReportKpis = {
  trips: number
  km: number
  co2Saved: number
  blockedUsers: number
  adoptionRate: number
}

export type ReportTrip = {
  id: string
  status: string
  originText: string
  departureAt: string
  completedAt?: string | null
  distanceKm: number
  co2SavedKg: number
  driverName: string
}

export type WeeklyChartPoint = {
  label: string
  trips: number
  km: number
}

export type AdminReport = {
  period: ReportPeriod | string
  from: string
  to: string
  kpis: ReportKpis
  trips: ReportTrip[]
  weeklyChart: WeeklyChartPoint[]
}

export type ReportExportResult = {
  blob: Blob
  filename: string | null
}

export type AuditEventType = 'sos' | 'auth' | 'admin' | 'system'

export type AuditSeverity = 'high' | 'medium' | 'low'

export type AuditEvent = {
  id: string
  action: string
  type: AuditEventType | string
  severity: AuditSeverity | string
  userId?: string | null
  userName?: string | null
  ip?: string | null
  device?: string | null
  createdAt: string
}

export type AuditLogFilter = {
  type?: string
  severity?: string
  page?: number
  pageSize?: number
}

export type AuditLogPage = {
  items: AuditEvent[]
  totalCount: number
  page: number
  pageSize: number
}
