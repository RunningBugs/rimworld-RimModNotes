# RimWorld Terrain Types Overview

Based on investigation of RimWorld source code, here are the different types of terrain organized by their major categories:

## 1. **Natural Terrain** (`natural=true`)

### Soil Types
- **Soil** - Standard fertile ground
- **Rich Soil** - Higher fertility (1.4x)
- **Stony Soil (Gravel)** - Lower fertility (0.7x)
- **Marshy Soil** - Wet, fertile but slow movement
- **Riverbank** - Rich soil near water sources

### Sand
- **Sand** - Low fertility (0.1x), generates sand filth
- **Soft Sand** - Very slow movement, no fertility

### Water
- **Deep Water** - Impassable, can freeze
- **Ocean Deep Water** - Saltwater, impassable
- **Chest-Deep Water** - Passable but slow
- **Shallow Water** - Supports waterproof construction
- **Moving Water** - Can power hydroelectric generators
- **Marsh** - Shallow water with mud, extinguishes fire

### Other Natural
- **Ice** - Frozen water, slippery
- **Mud** - Wet, slow movement, dries to soil

## 2. **Constructed Floors** (`layerable=true`)

### Basic Floors
- **Wood Floor** - Quick to build, flammable
- **Concrete** - Cheap, ugly but functional
- **Paved Tile** - Better concrete tiles
- **Straw Matting** - Animal barn flooring, low filth acceptance

### Metal Tiles
- **Steel Tile** - Medical bonus, quick to clean
- **Silver Tile** - Beautiful and clean
- **Gold Tile** - Extremely beautiful and expensive

### Stone Tiles
Made from different stone block types:
- **Sandstone Tile**
- **Granite Tile** 
- **Limestone Tile**
- **Slate Tile**
- **Marble Tile**

### Specialized Floors
- **Flagstone** - Rough stone for roads and walkways
- **Sterile Tile** - Medical/research flooring with high cleanliness bonus
- **Carpets** - Textile-based flooring (via TerrainTemplateDef system)

## 3. **Foundation/Bridge Terrain** (`bridge=true`, `isFoundation=true`)

### Bridges
- **Bridge** - Wooden structures built over water/marsh
- Allow light construction on top
- Fragile - can collapse and destroy buildings built on them
- Change terrain support type from `Bridgeable` to `Light`

## 4. **Special/Ancient Terrain**

### Ancient Floors
- **Ancient Tile** - Pre-existing metal tiles
- **Ancient Concrete** - Pre-existing concrete
- **Ancient Wood Floor** - Pre-existing wood flooring

### Roads
- **Broken Asphalt** - Fast movement, supports construction
- **Packed Dirt** - Compressed dirt roads

### Special
- **Underwall** - Hidden terrain under walls

## Key Terrain Properties

### Affordances (Construction Support)
Terrain affordances determine what can be built:

- **Light** - Supports light structures
- **Medium** - Supports medium structures (mainly walls)
- **Heavy** - Supports heavy structures (most buildings)
- **Bridgeable** - Can have bridges built over it
- **GrowSoil** - Plants can grow here
- **Diggable** - Graves can be dug
- **ShallowWater** - Waterproof items only
- **MovingFluid** - Hydroelectric power generation
- **WaterproofConduitable** - Waterproof conduits
- **SmoothableStone** - Can be smoothed into better stone
- **Walkable** - Basic movement allowed

### Layering System
RimWorld uses a terrain layering system:

1. **Natural Terrain** forms the base layer
2. **Floors** (`layerable=true`) can be built on top with sufficient terrain affordance
3. **Bridges** are foundations that change terrain support type
4. **Terrain Requirements**: Floors typically require `Heavy` affordance from underlying terrain

### Terrain Support Types
- **Natural terrain** provides various affordances based on type
- **Bridges** convert `Bridgeable` terrain to support `Light` construction  
- **Constructed floors** require adequate support from underlying terrain

### Other Properties
- **Fertility** - Affects plant growth (0.0 to 1.4+)
- **Path Cost** - Movement speed modifier
- **Beauty** - Aesthetic impact on colonists
- **Cleanliness** - Hygiene effects for medical/research
- **Flammability** - Fire spread risk
- **Pollution** - Some terrains can become polluted (Biotech DLC)

This system allows for complex terrain interactions like:
- Building floors on natural terrain
- Constructing bridges over water to enable building
- Layering different terrain types for specific purposes
- Managing terrain support requirements for different construction types