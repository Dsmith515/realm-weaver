import { Navigate, Route, Routes } from 'react-router'
import { Layout } from './components/Layout'
import { ComponentsPage, PlayerPage, RealmsPage, RitualPage } from './pages/Placeholders'

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Navigate to="/ritual" replace />} />
        <Route path="ritual" element={<RitualPage />} />
        <Route path="player" element={<PlayerPage />} />
        <Route path="realms" element={<RealmsPage />} />
        <Route path="components" element={<ComponentsPage />} />
      </Route>
    </Routes>
  )
}
