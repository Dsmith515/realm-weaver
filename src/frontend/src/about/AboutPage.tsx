/** What Realm Weaver is for. A first draft, refined as the project grows. */
export function AboutPage() {
  return (
    <section className="about">
      <h1>About Realm Weaver</h1>
      <p>
        Realm Weaver generates realms for tabletop role-playing games: battle maps for fields and dungeons, the events
        and creatures placed on them, and the exports a game master needs to run them. The same inputs always weave the
        same realm, so a map can be shared, reopened and played again.
      </p>

      <h2>How it works</h2>
      <p>
        Realms are woven in a <strong>ritual</strong> from components a player owns:
      </p>
      <ul>
        <li>
          <strong>Needle</strong>: how dangerous the realm is.
        </li>
        <li>
          <strong>Thread</strong>: where it is, meaning the biome and element.
        </li>
        <li>
          <strong>Coating</strong>: who lives there, plus the realm's kind and number of dungeon floors.
        </li>
        <li>
          <strong>Map</strong>: the recorded recipe and seed, so the realm can be woven again.
        </li>
      </ul>
      <p>
        Game masters can skip the inventory with <strong>Quick Weave</strong>, which builds a realm from the whole
        catalog for a one-shot.
      </p>

      <h2>What you get</h2>
      <ul>
        <li>A map with terrain, rooms, events and placements you can highlight.</li>
        <li>An SVG map, a Markdown placement sheet and the realm as JSON.</li>
        <li>An image prompt describing the map, ready for an image generator of your choice.</li>
      </ul>

      <h2>Principles</h2>
      <ul>
        <li>
          <strong>Deterministic.</strong> Generation uses a seeded random number generator, so a seed and its components
          always give the same realm.
        </li>
        <li>
          <strong>Original content.</strong> Events, creatures and rules are original or written by you. Nothing is
          copied from published games.
        </li>
        <li>
          <strong>Free to run.</strong> Everything runs on your own machine, with no paid services.
        </li>
      </ul>

      <h2>Where it's heading</h2>
      <p>
        Next up are richer settlements (population, structures and wealth), objectives tied to events, Thimbles that
        steer what a realm contains, and easier ways to install and share the tool. The full plan is in{' '}
        <code>docs/roadmap.md</code> in the repository.
      </p>
      <p className="muted">
        Realm Weaver is also a learning project in software engineering: a modular monolith on .NET with a React
        frontend, built one milestone at a time.
      </p>
    </section>
  )
}
