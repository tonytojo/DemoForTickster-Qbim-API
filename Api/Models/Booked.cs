namespace QbimApi.Models
{
    public class Booked
    {
        public string Date { get; set; }
        public string GuestNight { get; set; }
        public string CarCounter { get; set; }
        public string TimeRange { get; set; }
        public string BelaggningLogi { get; set; }
        public Booked(string date, string guestNight)
        {
            this.Date = date;
            this.GuestNight = guestNight;
            this.CarCounter = "0";
            this.TimeRange = "";
        }

        public Booked()
        {
            
        }
    }
}
