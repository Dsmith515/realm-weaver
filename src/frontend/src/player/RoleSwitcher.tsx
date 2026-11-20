import { PlayerSwitcher } from './PlayerSwitcher'
import { useRole, type Role } from './roleContext'

const options: { role: Role; label: string }[] = [
  { role: 'gm', label: 'GM' },
  { role: 'player', label: 'Player' },
]

/** "Viewing as GM | Player". In player view it also shows which player, since the player pages need one. */
export function RoleSwitcher() {
  const { role, setRole } = useRole()

  return (
    <div className="role-switcher">
      <div className="role-switcher__toggle" role="group" aria-label="Viewing as">
        {options.map((option) => (
          <button key={option.role} type="button" aria-pressed={role === option.role} onClick={() => setRole(option.role)}>
            {option.label}
          </button>
        ))}
      </div>
      {role === 'player' && <PlayerSwitcher />}
    </div>
  )
}
