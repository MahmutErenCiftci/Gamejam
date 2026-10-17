namespace AfetUyanisi.Shared
{
    /// <summary>
    /// Oyundaki bölümler. Jam kapsamında sadece Flood ve Earthquake açık;
    /// Wildfire ve Landslide menüde "Yakında" olarak kilitli (GameSceneRouter.IsUnlocked).
    /// </summary>
    public enum ChapterId
    {
        Flood,
        Earthquake,
        Wildfire,
        Landslide
    }
}
