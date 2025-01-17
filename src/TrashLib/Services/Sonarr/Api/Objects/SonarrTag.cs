using JetBrains.Annotations;

namespace TrashLib.Services.Sonarr.Api.Objects;

public class SonarrTag
{
    public string Label { get; [UsedImplicitly] set; } = "";
    public int Id { get; [UsedImplicitly] set; }
}
