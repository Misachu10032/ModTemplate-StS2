namespace ModTemplate.ModTemplateCode.Snapshots;

// Lightweight descriptor — actual state lives in the copied save files on disk.
public class RunSnapshot
{
    public int      Floor    { get; set; }
    public DateTime SavedAt  { get; set; } = DateTime.UtcNow;
    public string   Dir      { get; set; } = ""; // absolute path to the snapshot folder
}
