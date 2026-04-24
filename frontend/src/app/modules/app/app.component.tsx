import { Provider } from 'react-redux'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { store } from './app.store'
import { RequireAuth } from '~/modules/auth/components/require-auth.component'
import { useAuthBootstrap, useAuthBroadcastListener } from '~/modules/auth/hooks/auth.hooks'
import { HomePage } from '~/pages/home/home.page'
import { LoginPage } from '~/pages/login/login.page'
import { RegisterPage } from '~/pages/register/register.page'

// Inner component runs inside the Redux Provider so auth hooks'
// `useDispatch` / `useSelector` resolve to the right store.
const AppShell = () => {
  useAuthBootstrap()
  useAuthBroadcastListener()

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route
        path="/"
        element={
          <RequireAuth>
            <HomePage />
          </RequireAuth>
        }
      />
    </Routes>
  )
}

export const App = () => {
  return (
    <Provider store={store}>
      <BrowserRouter>
        <AppShell />
      </BrowserRouter>
    </Provider>
  )
}
