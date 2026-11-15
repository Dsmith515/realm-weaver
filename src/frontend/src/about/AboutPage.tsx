/** What Realm Weaver is for. A first draft, refined as the project grows. */
export function AboutPage() {
  return (
    <section className="about">
      <h1>About Realm Weaver</h1>
      <p>
        Realm Weaver is a game master&apos;s tool for tabletop role-playing games. It weaves realms (battle maps for fields
        and dungeons, the events and creatures placed on them, and the exports needed to run them) and keeps a
        repository of the components you create and hand out to players. The same ingredients always weave the same
        realm, so a realm can be reopened and played again.
      </p>

      <h2>How it works</h2>
      <p>
        For a one-shot or a story beat, <strong>Quick Weave</strong> builds a realm straight from the catalog. In play,
        realms are woven in a <strong>ritual</strong> from components a player owns:
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
          <strong>Map</strong>: the recorded recipe and seed. Weaving the same Needle, Thread and Coating reopens the
          same realm without it; a map the GM marks <em>Map required</em> is sealed and only opens for its holders.
        </li>
      </ul>
      <p>
        When the party finishes, the GM completes the run on the realm page and hands its map to the players who should
        keep it.
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
        The player pages are a preview. Shared sessions, where players log in, watch a ritual together and trade
        components, belong to a later session client built on this API. Also ahead: Thimbles that steer what a realm
        contains, richer settlements, and easier ways to share the tool. The full plan is in{' '}
        <code>docs/roadmap.md</code> in the repository.
      </p>
      <p className="muted">
        Realm Weaver is also a learning project in software engineering: a modular monolith on .NET with a React
        frontend, built one milestone at a time.
      </p>
    </section>
  )
}
