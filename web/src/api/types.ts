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
