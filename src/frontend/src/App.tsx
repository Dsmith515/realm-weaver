import { Navigate, Route, Routes } from 'react-router'
import { AboutPage } from './about/AboutPage'
import { ComponentsPage } from './catalog/ComponentsPage'
import { Layout } from './components/Layout'
import { QuickWeavePage } from './gm/QuickWeavePage'
import { LandingPage } from './landing/LandingPage'
import { GmOnly } from './player/GmOnly'
import { PlayerPage } from './player/PlayerPage'
import { PlayerProvider } from './player/PlayerProvider'
import { RoleProvider } from './player/RoleProvider'
import { RealmPage, RealmsPage } from './realms/RealmPages'
import { RitualPage } from './ritual/RitualPage'
import { ThemeProvider } from './theme/ThemeProvider'

export function App() {
  return (
    <ThemeProvider>
      <RoleProvider>
        <PlayerProvider>
          <Routes>
            <Route element={<Layout />}>
              <Route index element={<LandingPage />} />
              <Route path="weave">
                <Route index element={<Navigate to="/weave/quick" replace />} />
                <Route
                  path="quick"
                  element={
                    <GmOnly title="Quick Weave">
                      <QuickWeavePage />
                    </GmOnly>
                  }
                />
                <Route path="ritual" element={<RitualPage />} />
              </Route>
              {/* Old addresses, kept so bookmarks still work. */}
              <Route path="gm/weave" element={<Navigate to="/weave/quick" replace />} />
              <Route path="ritual" element={<Navigate to="/weave/ritual" replace />} />
              <Route path="player" element={<PlayerPage />} />
              <Route path="realms" element={<RealmsPage />} />
              <Route path="realms/:id" element={<RealmPage />} />
              <Route
                path="components"
                element={
                  <GmOnly title="Components">
                    <ComponentsPage />
                  </GmOnly>
                }
              />
              <Route path="about" element={<AboutPage />} />
            </Route>
          </Routes>
        </PlayerProvider>
      </RoleProvider>
    </ThemeProvider>
  )
}
