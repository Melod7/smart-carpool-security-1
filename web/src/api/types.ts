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
