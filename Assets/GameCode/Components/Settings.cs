namespace Legacy.Observer
{
	public static class ObserverSettings
    {
        [ConfigVar(
			Name = "TileSize", 
			DefaultValue = "1.65", 
			Description = "Grid Tile Size"
		)] public static ConfigVar TileSize;
		[ConfigVar(
			Name = "GridWidth", 
			DefaultValue = "24", 
			Description = "Grid width"
		)] public static ConfigVar GridWidth;
		[ConfigVar(
			Name = "GridHeight", 
			DefaultValue = "14", 
			Description = "Grid height"
		)] public static ConfigVar GridHeight;
	}
}
