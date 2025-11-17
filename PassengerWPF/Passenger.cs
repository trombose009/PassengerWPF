using CsvHelper.Configuration.Attributes;

public class Passenger
{
    public string Name { get; set; } = "";
    public string Sitzplatz { get; set; } = "";

    [Name("Avatar")]  // <- CsvHelper weiß jetzt, dass diese Spalte "Avatar" heißt
    public string AvatarFile { get; set; } = "";

    public string Order1 { get; set; } = "";
    public string Order2 { get; set; } = "";
    public string Order3 { get; set; } = "";
    public string Order4 { get; set; } = "";
}
