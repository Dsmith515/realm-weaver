interface PlaceholderProps {
  title: string
  description: string
}

function Placeholder({ title, description }: PlaceholderProps) {
  return (
    <section>
      <h1>{title}</h1>
      <p>{description}</p>
    </section>
  )
}

export const RitualPage = () => (
  <Placeholder title="Ritual" description="Combine a Needle, Thread, Coating and optional Map to open a portal. Arrives in Milestone 6." />
)
export const PlayerPage = () => (
  <Placeholder title="Player" description="Your inventory of ritual components. Arrives in Milestone 6." />
)
export const RealmsPage = () => (
  <Placeholder title="Realms" description="Generated fields and dungeons with placed encounters. Arrives in Milestone 6." />
)
export const ComponentsPage = () => (
  <Placeholder title="Components" description="Admin catalog of ritual components. Arrives in Milestone 6." />
)
