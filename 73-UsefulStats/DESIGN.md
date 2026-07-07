# Useful Stats design notes

## Goal

A RimWorld data-table mod inspired by WeaponStats, but focused on craftable item efficiency instead of owned weapons/apparel.

First version:

1. Show all currently craftable items.
2. Show all future/unlocked-later craftable items (recipes/buildables blocked by research, missing workbench/user, ideology/faction gates, etc.).
3. Provide categories and filters.
4. Compute and display:
   - market value / work amount
   - material count / work amount when the recipe/buildable has one concrete material input

Example use cases:

- Training Crafting/Tailoring while material is limited: compare how much cloth per work a tribalwear/parka/etc. consumes.
- Turning material into colony wealth/trade value: compare market value per work.

## Current implementation

Main tab: `Useful Stats`.

Categories:

- Dynamic Kind picker generated from loaded defs.
  - Items/buildings are grouped by top-level storage/thing categories such as `Foods`, `Manufactured`, `ResourcesRaw`, `Weapons`, `Apparel`, and `Buildings`, rather than leaf categories. This keeps the menu coarse and avoids duplicate labels like multiple `misc` categories.
  - Mods that add new top-level storage/thing categories automatically add new picker options.
  - The picker is opened from a single `Kind:` button; the first row of the popup is a text field for filtering kinds.

Filters:

- Current: recipe/buildable available now.
- Future: recipe/buildable exists but is not currently available.
- Search: matches item, defName, recipe/user, ingredient, and unlock/status text.

Columns:

- category
- item
- recipe/user
- work
- market value
- market value per work
- single material
- material per work and value per material; material variants are grouped into one row instead of one row per stuff/material, but rows can be expanded to show each material's concrete values
- summary-row `Val/Mat` uses the default stuff/material value instead of a min-max range, because the relative material multipliers are usually close enough and the default value better supports at-a-glance comparison
- current/future unlock status
- rows that exist in defs but do not have a known future crafting/build path are `All only`, not `Future`

## Important semantic choices

- Recipes come from `DefDatabase<RecipeDef>.AllDefsListForReading` and are excluded if they are surgery or have no single produced thing.
- Current recipe availability is conservative: `recipe.AvailableNow` plus at least one usable non-pawn recipe user/workbench on the current map when a map exists.
- Future recipes are included when they are valid products but not currently available.
- For stuffable single-material recipes/buildables, the table emits one row per product and summarizes the material range, e.g. `25-250 x stuff (24)` and `0.01-0.15` material/work. This avoids thousands of duplicated rows when equipment/material mods add many stuff defs. Hover text shows best/worst material/work.
- Expand/collapse: material-summary rows can be expanded inline to show concrete per-material rows; top controls provide `Expand all` and `Collapse all` for the current filtered view.
- `Future` is reserved for rows with a known path to become available later (e.g. research/buildable prerequisites or at least one potential non-pawn recipe user). Def-only products with no known future craft path are labelled `All only`.
- Current column order: Item, Work, Material, Value, Mat/Work, Val/Work, Val/Mat. `Recipe/User` and `Status / unlock` are intentionally hidden for now to keep the first version focused on efficiency comparison.
- The scroll table is virtualized: only rows near the visible scroll window are drawn. Filtered/sorted rows are cached and rebuilt only when filters/sort/data change, not every GUI frame.
- Buildables are included separately from `ThingDef.BuildableByPlayer`; single-material buildings are summarized the same way as recipes.

## UI screenshot workflow

Run `python3 tools/generate_ui_mockup.py --out /tmp/usefulstats-ui-mockup.png` to generate a static PNG mockup from the current C# table layout constants. This is used for fast visual iteration before doing an in-game RimWorld smoke test.

## Known limitations / future ideas

- Add localization keys instead of hard-coded English labels.
- Add persistent mod settings for default filters/sort.
- Add CSV export.
- Add richer work-skill columns, e.g. crafting/tailoring/smithing and skill learn factor.
- Add quality/profit estimates for quality-producing recipes.
- Add better grouping for multi-output recipes and products with special products.
- Add a configurable material focus filter, e.g. show only Cloth, Leather, Steel.
- Add exact bill/workbench reachability checks for current craftability if we need to account for missing ingredients instead of recipe unlock state only.
