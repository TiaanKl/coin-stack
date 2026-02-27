# Psychology Features: Setup and Trigger Guide

Last updated: 2026-02-27

## What is already active

- Reflection prompts (CBT-style) are active.
- Waitlist cool-off and readiness scoring are active.
- Emotion tagging and mood-before/after reflection capture are active.
- Thought Reframe step is now active inside the reflection modal.

## How reflection is triggered

A reflection becomes pending when a transaction is processed and settings allow reflections.

Primary trigger conditions in the game loop:

- Over budget spend
- Savings dip
- Impulse buy
- Large expense threshold reached

Technical path:

- Transaction processing: `GameLoopService.ProcessTransactionAsync`
- Reflection creation: `ReflectionService.CreateReflectionAsync`
- Pending reflection fetch: `ReflectionService.GetPendingAsync`
- UI display: header icon + modal in `MainLayout.razor`

## Setup checklist for triggers

1. In Settings, ensure reflections are enabled (`EnableReflections = true`).
2. Set a practical large expense threshold (for example 50%).
3. Ensure transactions are categorized and bucket-linked where possible.
4. Log qualifying transactions to trigger reflection conditions.

## How the new Thought Reframe works

When a reflection modal appears:

1. User writes initial thought in "Your Thoughts".
2. User optionally selects cognitive distortion tags.
3. User writes a balanced replacement thought in "Thought Reframe (CBT)".
4. On submit, both original reflection and reframe are stored together in the reflection response payload.

## About auth/multi-user

Authentication and per-user data partitioning are not implemented yet.
They remain in the product-readiness backlog and were not started in this implementation pass.
