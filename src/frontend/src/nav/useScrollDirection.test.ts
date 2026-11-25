import { describe, it } from 'vitest'

// The maintainer's piece: turn these into real tests while writing useScrollDirection.
describe('useScrollDirection', () => {
  it.todo('starts as up')
  it.todo('turns down after scrolling down past the threshold')
  it.todo('turns up again after scrolling back up past the threshold')
  it.todo('ignores movements smaller than the threshold')
  it.todo('is always up near the top of the page')
  it.todo('stops listening when the component unmounts')
})
