import { Provider } from 'react-redux'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { store } from './app.store'
import { useAuthBootstrap } from '~/modules/auth/hooks/auth.hooks'
import { HomePage } from '~/pages/home/home.page'

// Inner component runs inside the Redux Provider so the bootstrap hook's
// `useDispatch` / `useSelector` resolve to the right store. Routing
// components (`<RequireAuth>`, `/login`, `/register`) land in T03.2.
const AppShell = () => {
  useAuthBootstrap()
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
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
