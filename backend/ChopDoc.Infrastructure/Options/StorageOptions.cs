namespace ChopDoc.Infrastructure.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Root folder for uploaded sources and generated parts.</summary>
    public string RootPath { get; set; } = "App_Data/storage";
}
