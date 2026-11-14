# ADR 0001: Start as a modular monolith, then split into microservices

- **Status:** Accepted
- **Date:** 2026-11-11

## Context
One goal of this project is to practise microservice architecture and distributed transactions. The domain has natural boundaries:
- **Catalog**: component definitions
- **Inventory**: players and the items they own
- **Rituals**: orchestrating a ritual
- **Realms**: generated maps

A ritual touches all four. It spends consumables, creates a Map component, grants it to the player and stores the generated realm.

Building microservices from day one means paying for a broker, gateway, sagas and distributed tracing before the domain itself is understood.

## Decision
Build Phase 1 as a **modular monolith**:
- One ASP.NET Core host, with one project per module.
- Modules talk only through public contracts.
- A ritual is a single **local ACID transaction** on PostgreSQL, using an idempotency key and optimistic concurrency.

In Phase 2 (Milestone 9), extract each module into its own service with its own database:
- RabbitMQ as the message broker.
- Wolverine for the transactional outbox and inbox.
- An orchestrated **saga** for rituals: reserve consumables, then generate the realm, then create the map, then grant it, then commit. Compensating steps undo earlier steps when a later one fails.

## Consequences
- Something working ships early, and the generator can be built test-first on its own.
- The Phase 1 transaction becomes the **baseline** to compare the saga against. It shows clearly what ACID gave us for free: atomicity, isolation and immediate consistency. It also shows what the saga has to rebuild: compensation, idempotent consumers and eventual consistency.
- The boundaries between modules have to be enforced with discipline and tests, otherwise the later split stops being mechanical.
