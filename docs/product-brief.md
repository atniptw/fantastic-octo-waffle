# Product Brief

## Purpose
Build a browser-only Dress-Up app for R.E.P.O. mods so cosmetics can be reviewed before loading anything into the game.

## Legal/Distribution Constraint
- The app never uploads, proxies, or redistributes mod files through any server controlled by this project.
- All parsing, processing, and preview generation happen locally in the user's browser.
- The project assumes users only load mods they are legally permitted to use.

## Problem
Testing cosmetics directly in-game is slow and disruptive. The app should let a user upload mod packages, inspect available cosmetics, combine selections with a base avatar, and preview the result in-browser.

## Target User
- Solo user (project owner)
- Internal use first (no multi-tenant requirements)

## Hosting Target
- Deployment target is GitHub Pages (static hosting only).

## MVP Scope
- Upload mod packages (zip)
- Discover and index `.hhh` cosmetic bundles
- Persist bundles and metadata in IndexedDB
- Parse Unity assets needed for avatar + cosmetics composition
- Generate GLB in-browser for preview
- Render preview via three.js

## Non-Goals (MVP)
- Cloud sync or server-side storage
- User accounts/roles
- Marketplace or sharing features
- Full Unity format coverage beyond needed sample corpus

## Permanent Non-Goals
- Any backend API or server runtime dependency for mod ingestion, parsing, or preview generation
- Any feature that requires hosting third-party mod content on project-controlled infrastructure

## Success Criteria
The app is considered "deployed and working" when:
1. It runs in browser from a deployed GitHub Pages URL.
2. A user can upload representative mod zips and see discovered cosmetics.
3. Selected cosmetics can be combined with base avatar and previewed in three.js.
4. Data persists across refresh via IndexedDB.
5. Verification commands pass (`npm run verify`).
