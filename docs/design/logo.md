# Logo and icon: chosen treatment

Decided while exploring the brand on the design canvas, before Milestone 8i step 1 (brand assets and tokens).

## Art
- **Style:** Medium glow. The medallion centre is solid, and a moderate haze around the ring keeps the stars and the gem tips.
- **Source files** (kept outside the repo; only resized copies get committed):
  `C:\Programming\References\RealmWeaverReferences\Rebrand\Transparent\Realm-Weaver-<Logo|Icon|Icon-No-Text|Icon-No-Text-Aura>-Transparent-MediumGlow.png` (1254 px square).
- Other styles stay in the same folder for comparison: `-SoftGlow`, `-Transparent` (wide glow), `-SeeThrough` and `-NoGlow`.
- **How it was cut** from the navy originals (key colour `#000c31`):
  - Haze: colour-unmixed against the key, with floor 0.05, ramp 0.40 and 60% smoothstep.
  - Centre: solid inside r 0.40.
  - Edge: fades out over 0.02, ending at r 0.499.
- **Format:** keep the art as raster (PNG or WebP). It is painted artwork, and vector tracing would lose the glow. The favicon is the exception: it should be a flat, simplified "RW" mark, which can be SVG.

## In the app
| File (`src/frontend/public/brand/`) | Use |
|---|---|
| `logo-480.webp`, `logo-960.webp` | Landing hero logo, with `srcset` so 2x screens get the larger one |
| `icon-64.webp`, `icon-128.webp` | Header and nav icon |

These live in `public/` because they need fixed URLs (`/brand/logo-480.webp`). Vite copies `public/` as is, without hashing the file names.

## Halo (CSS `drop-shadow` around the image)
- The same size and strength apply to the logo and the icon. Tokens in `index.css`:
  - `--logo-halo-size: 16px`
  - `--logo-halo: 15%`, overridden to 35% by the Parchment theme. A light background needs a stronger halo for the glow to read.
- **Hero logo:** `filter: drop-shadow(0 0 var(--logo-halo-size) color-mix(in srgb, var(--accent-2) var(--logo-halo), transparent))`
- **Header icon:** the same filter, tinted with `var(--accent)` instead of `--accent-2`.

## Hero backdrop
- A soft radial glow behind the landing hero: `radial-gradient(ellipse at 72% 0%, var(--glow) 0%, transparent 60%)`.
- On phones, centre it at `50% 0%`.
