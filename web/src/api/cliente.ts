import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { AuthResponse, AuthUser, AdminNotification } from './types'

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:8080'

const STORAGE_ACCESS = 'kubix.accessToken'
const STORAGE_REFRESH = 'kubix.refreshToken'
const STORAGE_USER = 'kubix.user'

function parseAuthUser(data: unknown): AuthUser {
  if (
    typeof data === 'object' &&
    data !== null &&
    typeof (data as AuthUser).id === 'string' &&
    typeof (data as AuthUser).email === 'string' &&
    typeof (data as AuthUser).role === 'string'
  ) {
    return data as AuthUser
  }

  throw new Error('La API devolvió una respuesta inválida para /me.')
}

export const tokenStorage = {
  getAccessToken: () => localStorage.getItem(STORAGE_ACCESS),
  getRefreshToken: () => localStorage.getItem(STORAGE_REFRESH),
  getUser: (): AuthUser | null => {
    const raw = localStorage.getItem(STORAGE_USER)
    if (!raw) return null
    try {
      return JSON.parse(raw) as AuthUser
    } catch {
      return null
    }
  },
  setSession: (accessToken: string, refreshToken: string, user: AuthUser) => {
    localStorage.setItem(STORAGE_ACCESS, accessToken)
    localStorage.setItem(STORAGE_REFRESH, refreshToken)
    localStorage.setItem(STORAGE_USER, JSON.stringify(user))
  },
  updateUser: (user: AuthUser) => {
    localStorage.setItem(STORAGE_USER, JSON.stringify(user))
  },
  clear: () => {
    localStorage.removeItem(STORAGE_ACCESS)
    localStorage.removeItem(STORAGE_REFRESH)
    localStorage.removeItem(STORAGE_USER)
  },
}

export const api = axios.create({
  baseURL: API_URL,
  headers: { 'Content-Type': 'application/json' },
})

type RetryConfig = InternalAxiosRequestConfig & { _retry?: boolean }

let refreshPromise: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = tokenStorage.getRefreshToken()
  if (!refreshToken) return null

  try {
    const { data } = await axios.post<AuthResponse>(`${API_URL}/auth/refresh`, {
      refreshToken,
    })
    tokenStorage.setSession(data.accessToken, data.refreshToken, data.user)
    return data.accessToken
  } catch {
    tokenStorage.clear()
    return null
  }
}

api.interceptors.request.use((config) => {
  const token = tokenStorage.getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as RetryConfig | undefined
    if (!original || error.response?.status !== 401 || original._retry) {
      return Promise.reject(error)
    }

    const url = original.url ?? ''
    if (url.includes('/auth/login') || url.includes('/auth/refresh')) {
      return Promise.reject(error)
    }

    original._retry = true

    refreshPromise ??= refreshAccessToken().finally(() => {
      refreshPromise = null
    })

    const newToken = await refreshPromise
    if (!newToken) {
      if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/login')) {
        window.location.assign('/login')
      }
      return Promise.reject(error)
    }

    original.headers.Authorization = `Bearer ${newToken}`
    return api(original)
  },
)

export const authApi = {
  login: async (email: string, password: string) => {
    const { data } = await api.post<AuthResponse>('/auth/login', { email, password })
    return data
  },
  logout: async (refreshToken?: string | null) => {
    await api.post('/auth/logout', { refreshToken: refreshToken ?? null })
  },
  changePassword: async (currentPassword: string, newPassword: string) => {
    await api.post('/auth/change-password', { currentPassword, newPassword })
  },
  me: async () => {
    const { data } = await api.get<unknown>('/me')
    return parseAuthUser(data)
  },
}

export const notificationsApi = {
  list: async () => {
    const { data } = await api.get<AdminNotification[]>('/admin/notifications')
    return data
  },
}

export { API_URL }
