# UI Design Specification — Cosmetic Mod Inspector

## Purpose

This document defines the UI layout, styling, and component structure for a **static, client-side web app** hosted on GitHub Pages.

The app allows users to visually inspect **cosmetic mods** from Thunderstore by:
- Fetching mod metadata on page load
- Allowing the user to explicitly analyze a mod
- Downloading and extracting mod archives client-side
- Converting cosmetic assets into preview images
- Displaying those images in a fast, scannable gallery

The primary goal is **visual investigation**, not mod management.

---

## Technical Constraints

- React (functional components only)
- Static site (GitHub Pages)
- No backend / no server-side rendering
- All processing is client-side
- Styling must use one of:
  - Tailwind CSS, or
  - Plain CSS using Grid and Flexbox
- No heavy UI frameworks (e.g., MUI, Ant Design)
- Desktop-first layout with clean mobile fallback
- Avoid heavy animations; prioritize clarity and performance

---

## Page Layout

### Desktop Layout (Primary)

Use a **master–detail layout**:

┌──────────────────────────────────────────────┐
│ Header │
├───────────────┬──────────────────────────────┤
│ Mod List │ Mod Detail / Preview Pane │
│ (scrollable) │ (scrollable) │
└───────────────┴──────────────────────────────┘


- Header remains fixed or sticky
- Left pane contains the mod browser
- Right pane shows details for the selected mod

### Mobile Layout

- Single-column layout
- Mod list view → mod detail view
- Back button returns to list
- No split panes on mobile

---

## Header

### Contents

- App name (left)
- Search input (center or right)
- Optional filter dropdown (future expansion)

### Style

- Height: 56–64px
- Dark background
- High-contrast text
- Subtle bottom border
- Minimal branding; tool-like appearance

---

## Mod List (Left Pane)

### Layout

- Vertical, scrollable list
- Each mod is a compact card row
- Hover highlight and clear selected state

### Each Mod Item Displays

- Mod name (bold)
- Author
- Short description (single line, ellipsis overflow)
- Small metadata badges (e.g., downloads, categories)

### Behavior

- Clicking selects a mod
- Selection updates the detail pane
- No zip download or analysis occurs here

---

## Mod Detail Pane (Right Pane)

### A. Mod Header

Displays:
- Mod name
- Author
- Version
- Download count

Actions:
- Primary button: **Analyze Mod**
- Secondary button: **Open on Thunderstore**

### B. Analysis Status

- Visible progress messages for long-running operations:
  - Fetching archive
  - Extracting files
  - Converting assets
- Errors must be clearly visible and human-readable
- No silent failures

### C. Asset Preview Gallery (Primary Content)

- Grid-based gallery
- Square or slightly rectangular tiles
- Each tile includes:
  - Generated preview image
  - Asset name or inferred label
- Clicking a tile opens a modal/lightbox view

### D. Optional Asset Details (Collapsible)

- Original file path inside archive
- File type
- Conversion notes

Collapsed by default.

---

## Color & Style Guidelines

### Theme

- Dark theme by default
- Neutral, tool-like aesthetic
- Avoid overly stylized or neon “gamer” visuals

### Suggested Palette

- Background: very dark gray / near-black
- Panels: slightly lighter gray
- Text: off-white / light gray
- Accent color: muted blue or purple
- Error state: soft red
- Success state: soft green

### Design Principles

- Clear visual hierarchy
- Use spacing instead of decoration
- Subtle borders over heavy shadows
- Designed for scanning many items quickly

---

## Suggested Component Structure

Break the UI into small, focused components:

- `AppLayout`
- `Header`
- `ModList`
- `ModListItem`
- `ModDetail`
- `AnalyzeButton`
- `AnalysisStatus`
- `AssetGallery`
- `AssetTile`
- `AssetModal`

Components should:
- Be functional components
- Be driven by props
- Be stateless where possible
- Be easy to rearrange and extend

---

## UX Rules

- Never auto-download or analyze a mod without explicit user action
- Always show progress for long-running tasks
- If no cosmetic assets are found:
  - State this clearly
  - Do not fail silently
- The UI should feel like an **inspection / debugging tool**, not a storefront

---

## Implementation Guidance

- Start by building layout and styling first
- Use placeholder or mock data initially
- Stub analysis logic behind the Analyze button
- Prioritize clean structure over feature completeness

---

## End of Specification
