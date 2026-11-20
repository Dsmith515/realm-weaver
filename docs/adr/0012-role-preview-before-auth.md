# ADR 0012: Role preview before authentication

- **Status:** Accepted
- **Date:** 2026-11-20

## Context
Realm Weaver is a GM tool first (ADR 0010), and its player pages are a preview of what a later session client will show. Without logins, though, every visitor sees everything at once: the GM's Quick Weave and Components pages next to the player's Ritual and Player pages. A GM preparing a session wants to check "what will my players see?", and the coming navigation redesign (Milestone 8i) needs to know whose menu to draw.

There is no authentication yet, and adding it now would pull in player-session features (accounts, parties) that belong to the separate Campaign Manager client.

The app called its management side "admin" in a few places, and "GM" everywhere else. Only one person ever plays that part.

## Decision
- **A "Viewing as GM | Player" switch** in the header, backed by a `RoleProvider` and a `useRole()` hook (`src/frontend/src/player/roleContext.ts`). The choice is saved in localStorage, and the app starts as GM.
- **In player view:**
  - The player picker appears next to the switch, because the player pages need to know which player.
  - GM-only links (Quick Weave, Components) are hidden.
  - Opening a GM-only page directly shows a short "This page is for the GM" notice with a *Switch to GM view* button (`<GmOnly>`). It doesn't redirect, so a shared link still explains itself.
- **It's a preview, not security.** The API doesn't check roles, and anyone can flip the switch. This is stated wherever the switch is used, so nobody mistakes it for access control.
- **"Admin" is now "GM"** in the UI, the API description and messages, code comments and test names. A single word for the role makes the code and the screen read the same.

## Consequences
- **Authentication can replace this cleanly.** When logins arrive, `RoleProvider` takes the role from the signed-in user instead of localStorage, and the switch disappears (or stays only for a GM previewing the player view). Nothing that calls `useRole()` changes. The API then enforces the same split server-side.
- **Role-aware pages are cheap to add.** Step 4 of Milestone 8i uses `useRole()` to show players only their assigned and completed realms.
- **The player picker no longer shows in GM view.** The player pages still work there, using the last chosen player. A GM switches to player view to choose another.
