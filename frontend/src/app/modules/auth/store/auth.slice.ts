import { createSlice, type PayloadAction } from '@reduxjs/toolkit'
import type { AuthState, AuthUser } from '~/modules/auth/models/auth.model'

const initialState: AuthState = {
  status: 'unknown',
  user: null,
}

export const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    sessionVerified: (state, action: PayloadAction<AuthUser>) => {
      state.status = 'authenticated'
      state.user = action.payload
    },
    loggedOut: (state) => {
      state.status = 'unauthenticated'
      state.user = null
    },
  },
})

export const { sessionVerified, loggedOut } = authSlice.actions
export const authReducer = authSlice.reducer
