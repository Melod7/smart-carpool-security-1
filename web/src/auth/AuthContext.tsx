import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { authApi, tokenStorage } from '../api/cliente'
import type { AuthUser, UserRole } from '../api/types'

type AuthContextValue = {
  user: AuthUser | null
  accessToken: string | null
  isAuthenticated: boolean
  mustChangePassword: boolean
  isBootstrapping: boolean
  login: (email: string, password: string) => Promise<AuthUser>
  logout: () => Promise<void>
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>
  refreshUser: () => Promise<AuthUser | null>
  hasRole: (...roles: UserRole[]) => boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

function readMustChange(user: AuthUser | null, forced = false): boolean {
  if (forced) return true
  return Boolean(user?.mustChangePassword)
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => tokenStorage.getUser())
  const [accessToken, setAccessToken] = useState<string | null>(() =>
    tokenStorage.getAccessToken(),
  )
  const [forcedPasswordChange, setForcedPasswordChange] = useState(false)
  const [isBootstrapping, setIsBootstrapping] = useState(true)

  useEffect(() => {
    let cancelled = false

    async function bootstrap() {
      const token = tokenStorage.getAccessToken()
      if (!token) {
        if (!cancelled) setIsBootstrapping(false)
        return
      }

      try {
        const me = await authApi.me()
        if (cancelled) return
        tokenStorage.updateUser(me)
        setUser(me)
        setAccessToken(token)
        setForcedPasswordChange(Boolean(me.mustChangePassword))
      } catch {
        if (cancelled) return
        tokenStorage.clear()
        setUser(null)
        setAccessToken(null)
        setForcedPasswordChange(false)
      } finally {
        if (!cancelled) setIsBootstrapping(false)
      }
    }

    void bootstrap()
    return () => {
      cancelled = true
    }
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const data = await authApi.login(email, password)
    const nextUser = {
      ...data.user,
      mustChangePassword: data.mustChangePassword || data.user.mustChangePassword,
    }
    tokenStorage.setSession(data.accessToken, data.refreshToken, nextUser)
    setAccessToken(data.accessToken)
    setUser(nextUser)
    setForcedPasswordChange(data.mustChangePassword)
    return nextUser
  }, [])

  const logout = useCallback(async () => {
    const refreshToken = tokenStorage.getRefreshToken()
    try {
      await authApi.logout(refreshToken)
    } catch {
      // Clear local session even if the API call fails.
    } finally {
      tokenStorage.clear()
      setUser(null)
      setAccessToken(null)
      setForcedPasswordChange(false)
    }
  }, [])

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    await authApi.changePassword(currentPassword, newPassword)
    const me = await authApi.me()
    const nextUser = { ...me, mustChangePassword: false }
    tokenStorage.updateUser(nextUser)
    setUser(nextUser)
    setForcedPasswordChange(false)
  }, [])

  const refreshUser = useCallback(async () => {
    const me = await authApi.me()
    tokenStorage.updateUser(me)
    setUser(me)
    setForcedPasswordChange(Boolean(me.mustChangePassword))
    return me
  }, [])

  const hasRole = useCallback(
    (...roles: UserRole[]) => (user ? roles.includes(user.role) : false),
    [user],
  )

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      accessToken,
      isAuthenticated: Boolean(accessToken && user),
      mustChangePassword: readMustChange(user, forcedPasswordChange),
      isBootstrapping,
      login,
      logout,
      changePassword,
      refreshUser,
      hasRole,
    }),
    [
      user,
      accessToken,
      forcedPasswordChange,
      isBootstrapping,
      login,
      logout,
      changePassword,
      refreshUser,
      hasRole,
    ],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth debe usarse dentro de AuthProvider')
  }
  return ctx
}
