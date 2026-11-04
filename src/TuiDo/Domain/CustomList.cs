namespace TuiDo.Domain;

internal sealed class CustomList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Icon { get; set; } = IconParser.DefaultIcon;
}
