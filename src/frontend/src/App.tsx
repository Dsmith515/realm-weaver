import { Navigate, Route, Routes } from 'react-router'
import { ComponentsPage } from './catalog/ComponentsPage'
import { Layout } from './components/Layout'
import { PlayerPage } from './player/PlayerPage'
import { PlayerProvider } from './player/PlayerProvider'
import { RealmPage, RealmsPage } from './realms/RealmPages'
import { RitualPage } from './ritual/RitualPage'

export function App() {
  return (
    <PlayerProvider>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Navigate to="/ritual" replace />} />
          <Route path="ritual" element={<RitualPage />} />
          <Route path="player" element={<PlayerPage />} />
          <Route path="realms" element={<RealmsPage />} />
          <Route path="realms/:id" element={<RealmPage />} />
          <Route path="components" element={<ComponentsPage />} />
        </Route>
      </Routes>
    </PlayerProvider>
  )
}
