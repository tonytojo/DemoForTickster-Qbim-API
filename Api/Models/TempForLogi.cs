namespace QbimApi.Models
{
    public class TempForLogi
    {
        public string Date { get; set; }
        public int NrOfBelaggningsbaraEnheter { get; set; }
        public TempForLogi(string date,int nrOfBelaggningsbaraEnheter)
        {
            this.Date = date;
            this.NrOfBelaggningsbaraEnheter = nrOfBelaggningsbaraEnheter;
        }
    }
}
