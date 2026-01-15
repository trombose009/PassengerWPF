namespace PassengerWPF
{
    public class BoardingConfig
    {
        public BoardingPaths Paths { get; set; }
        public BoardingCsv Csv { get; set; }
    }

    public class BoardingPaths
    {
        public string BGImage { get; set; }
        public string BoardingSound { get; set; }
        public string Avatars { get; set; }
    }

    public class BoardingCsv
    {
        public string PassengerData { get; set; }
    }
}
