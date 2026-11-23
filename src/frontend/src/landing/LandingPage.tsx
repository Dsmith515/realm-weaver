import { Link } from 'react-router'
import componentIcon from '../assets/icons/components.svg'
import realmsIcon from '../assets/icons/realms.svg'
import weaveIcon from '../assets/icons/weave.svg'
import { NavIcon } from '../nav/NavMenu'
import { useRole } from '../player/roleContext'

/** The front door: who Realm Weaver is for, and where to start. */
export function LandingPage() {
  const { role } = useRole()
  const gm = role === 'gm'

  return (
    <section className="landing">
      <div className="landing__hero">
        <img
          className="landing__logo"
          src="/brand/logo-960.webp"
          srcSet="/brand/logo-480.webp 480w, /brand/logo-960.webp 960w"
          sizes="(min-width: 768px) 440px, 60vw"
          width={960}
          height={960}
          alt="Realm Weaver"
        />
        <div className="landing__intro">
          <p className="landing__eyebrow">{gm ? 'For game masters' : 'For players'}</p>
          <h1>Run a one-shot tonight.</h1>
          <p className="landing__premise">
            Realm Weaver is a map-and-encounter layer for the game system you already play. Weave realms yourself, or hand
            your party components and let them weave their own.
          </p>
          <p className="landing__premise">
            Every realm is tied to a seed and a recipe: weaving with the same components gives the same realm.
            Weave with a map and different components, and you get a mirrored variant. GMs keep track of what everyone
            has cleared.
          </p>
          <div className="landing__actions">
            {gm ? (
              <Link className="button button--primary landing__cta" to="/weave/quick">
                Quick Weave
              </Link>
            ) : (
              <Link className="button button--primary landing__cta" to="/weave/ritual">
                Weave a ritual
              </Link>
            )}
            <Link className="button landing__cta landing__browse" to="/realms">
              Browse realms
            </Link>
            <a className="landing__how" href="#how-it-works">
              How it works
              <svg aria-hidden="true" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2.25" strokeLinecap="round" strokeLinejoin="round">
                <path d="M12 5v14M6 13l6 6 6-6" />
              </svg>
            </a>
          </div>
          <p className="landing__note">
            {gm ? 'Quick Weave draws from the whole catalog. Nothing is used up.' : 'A ritual spends one needle, thread and coating from your pack.'}
          </p>
        </div>
      </div>

      <section id="how-it-works" className="landing__steps" aria-labelledby="how-heading">
        <h2 id="how-heading">How weaving works</h2>
        <ol>
          <li>
            <span className="landing__step-head">
              <NavIcon icon={componentIcon} tone="soft" size={34} />
              <span className="landing__step-no">Step 1</span>
            </span>
            <h3>Gather Components</h3>
            <p>GMs create components and hand them out to players. Each ritual uses one needle, and consumes one thread and one coating.</p>
          </li>
          <li>
            <span className="landing__step-head">
              <NavIcon icon={weaveIcon} tone="accent" size={34} />
              <span className="landing__step-no">Step 2</span>
            </span>
            <h3>Weave the Realm</h3>
            <p>
              Weaving creates a realm with its own seed, map, objectives and encounters. Players weave with a <b>Ritual</b>;
              GMs can <b>Quick Weave</b> drawing from the whole component catalog.
            </p>
          </li>
          <li>
            <span className="landing__step-head">
              <NavIcon icon={realmsIcon} tone="info" size={34} />
              <span className="landing__step-no">Step 3</span>
            </span>
            <h3>Play and Reward</h3>
            <p>GMs give players a realm's map as a reward for completing its objectives. With a map, players can return to a favorite realm or weave a mirrored variant.</p>
          </li>
        </ol>
      </section>

      <p className="landing__footnote">Content is original or written by you. Realm Weaver works alongside the rules you already use.</p>
    </section>
  )
}
