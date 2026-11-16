interface PagerProps {
  page: number
  pageSize: number
  totalCount: number
  /** What's being counted, plural: "components", "realms". */
  noun: string
  onPage: (page: number) => void
}

/** Previous / Next with "Page 2 of 5 · 112 components". Sits under a scrolling table, so it's always in view. */
export function Pager({ page, pageSize, totalCount, noun, onPage }: PagerProps) {
  const pages = Math.max(1, Math.ceil(totalCount / pageSize))
  return (
    <div className="pager">
      <button type="button" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        Previous
      </button>
      <span>
        Page {page} of {pages} · {totalCount} {noun}
      </span>
      <button type="button" disabled={page >= pages} onClick={() => onPage(page + 1)}>
        Next
      </button>
    </div>
  )
}
