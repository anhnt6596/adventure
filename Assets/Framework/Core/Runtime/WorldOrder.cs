namespace Core
{
    // Where everything in the world sits in the draw order.
    //
    // The project has ONE sorting layer (Default), so order-in-layer is the entire ordering budget and it is
    // worth spending on purpose. The bands, back to front:
    //
    //     -300            water reflections    under the surface they are mirrored in
    //     -200            the water surface
    //     -100 and down   terrain tiles        topmost terrain layer at -100, each layer under it one lower
    //     -99 .. -1       ON the ground        bridge decks, boats, jetties, roads, decals, ground shadows
    //        0            standing in it       characters, trees, props - everything billboarded
    //
    // THE GAP BETWEEN THE TILES AND ZERO IS THE POINT. Things that lie flat on the ground but are not terrain -
    // walked over rather than around - had nowhere to go before: the old scheme packed the whole world into
    // -5..0 and there was no room to insert anything between the ground and the things standing on it. A
    // hundred slots per band is not generosity, it is what stops the next thing from forcing a renumber.
    //
    // Render queue is NOT the knob here. Order in layer outranks it (which is why the terrain layers, whose
    // queues run 3000..3003, still draw under sprites at queue 3000), and queue only breaks ties - the three
    // camera veils all sit at order 0 and are separated by queue alone.
    public static class WorldOrder
    {
        public const int Reflection = -300;
        public const int Water = -200;

        // The topmost terrain layer. Lower layers step down from here, so tiles never rise above -100.
        public const int TerrainTop = -100;

        // A flat thing lying on the ground that is not terrain: a bridge deck, a jetty, a road, a decal. Mid-band
        // on purpose - there is room either side to put something under or over it without touching this.
        public const int OnGround = -50;

        // Anything standing in the world. Sprites are authored at 0 and there is no reason for that to change.
        public const int Standing = 0;
    }
}
