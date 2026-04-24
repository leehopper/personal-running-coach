export type AuthStatus = 'authenticated' | 'unauthenticated' | 'unknown'

export interface AuthUser {
  userId: string
  email: string
}

export interface AuthState {
  status: AuthStatus
  user: AuthUser | null
}

export interface RegisterRequestDto {
  email: string
  password: string
}

export interface LoginRequestDto {
  email: string
  password: string
}

export interface AuthResponseDto {
  userId: string
  email: string
}
