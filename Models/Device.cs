namespace Aggregator.Models
{
    internal class Device
    {
        public string? ip { get; set; }
        public string? context { get; set; }

        public string? mac_address { get; set; }
        public string? serial_number { get; set; }
        public string? discovery_date { get; set; }

        public override string ToString()
        {
            return $"{ip}, {context}, {discovery_date}, {mac_address}, {serial_number}";
        }
    }
}
