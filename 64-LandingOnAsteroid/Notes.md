# Some notes about the map generation process.

RimWorld assumes each tile can only have one MapParent.
Settlement and BasicAsteroidMapParent are both MapParents.
When registering, both will be registered as MapParents.
When fetch the mapParent at specific cell, it will return the first one.

Because BasicAsteroidMapParent is added earlier to the map than Settlement.
So when fetching the mapParent?.Map, it will always return null because the map is generated for Settlement.
