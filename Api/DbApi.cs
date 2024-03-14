using QbimApi.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Reflection;
using QbimApi;
using System.Data.Common;
using System.Drawing;


//[{
//    date: "2023-04-26",
//    reservationPercent: 75,
//    reservationCount: 3000
//}, 
//{
//    date: "2023-04-20",
//    reservationPercent: 75,
//    reservationCount: 3000
//}]

namespace QbimApi
{
    /// <summary>
    /// This class represent the engine to be able to get data from the database
    /// </summary>
    /// 
    public class DbApi : IDbApi
    {
        private const float MULTIPLIKATOR = 2.7f;
        private string connectionR360;
        private string connectionCarCounter;

        private SqlConnection connR360;
        private SqlConnection connCarCounter;

        private SqlCommand cmd;
        private SqlDataReader reader;
        private List<Booked> bookedlist = new List<Booked>();
        private List<TempForLogi> logiList = new List<TempForLogi>();
        private int availableUnits;
        private DateTime now;
        private string startTime;
        private string endTime;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="configuration"></param>
        public DbApi(IConfiguration configuration, string startTime, string endTime)
        {
            this.startTime = startTime;
            this.endTime = endTime;

            connectionR360 = configuration.GetConnectionString("R360Connection");
            connR360 = new SqlConnection(connectionR360);
            if (connR360 != null && connR360.State == ConnectionState.Closed)
                connR360.Open();

            connectionCarCounter = configuration.GetConnectionString("CarCounterConnection");
            connCarCounter = new SqlConnection(connectionCarCounter);
            if (connCarCounter != null && connCarCounter.State == ConnectionState.Closed)
                connCarCounter.Open();

            now = DateTime.Now.Date;
        }
        
        public UserCredential GetUser(string UserName, string Password, string ClientSecret)
        {
            cmd = new SqlCommand("SELECT UserName, Password, ClientSecret from [User] WHERE UserName = @UserName and Password = @Password and ClientSecret = @ClientSecret;", connR360);

            cmd.Parameters.AddWithValue("@UserName", UserName);
            cmd.Parameters.AddWithValue("@Password", Password);
            cmd.Parameters.AddWithValue("@ClientSecret", ClientSecret);

            reader = cmd.ExecuteReader();

            var UserCredential = new UserCredential();

            while (reader.Read())
            {
                UserCredential.UserName = reader["UserName"].ToString();
                UserCredential.Password = reader["Password"].ToString();
                UserCredential.ClientSecret = reader["ClientSecret"].ToString();
            }

            return UserCredential;
        }

        /// <summary>
        /// This method will get the number of booked guestnight for each date 
        /// ranging from start to end
        /// </summary>
        /// <param name="start">The startdate to start from to calculate the number of booked guestnights</param>
        /// <param name="end">The enddate to end the calculation of the number of booked guestnights</param>
        public void GetGuestNights(string start, string end)
        {
            string sql = "SELECT dc.Date as Mydate, sum(hfb.GuestsAdult * hfb.quantity) as Guestnight " +
                   "FROM[Datamart].[HistoryFact_Booking] hfb " +
                   "INNER JOIN[Datamart].[Dim_Pool] as dp ON hfb.Dim_Pool_SK = dp.Dim_Pool_SK " +
                   "INNER JOIN[Datamart].[Dim_Calendar] as dc ON hfb.Dim_CalendarArrivalDate_SK = dc.Dim_Calendar_SK " +
                   "INNER JOIN[Datamart].[Dim_Resort] as dr ON hfb.Dim_Resort_SK = dr.Dim_Resort_SK " +
                   "INNER JOIN[Datamart].[Dim_Unit] as du ON hfb.Dim_Unit_SK = du.Dim_Unit_SK " +
                   "INNER JOIN[Datamart].[Dim_PoolGrpConns] as coons ON dp.Dim_Pool_SK = coons.Dim_Pool_SK " +
                   "INNER JOIN[Datamart].[Dim_PoolGrps] as grp ON coons.PoolGrpsKey = grp.PoolGrpsKey " +
                   "where hfb.ValidFrom <= @ValidFrom and hfb.ValidTo >= @ValidTo and " +
                   "hfb.Gästnatt = 1 and " +
                   "hfb.Is_Canceled = 0 and " +
                   "dp.StatGroupLid in (1, 5) and " +
                   "grp.Desc1 = 'Alla boende exkl. camping' and " +
                   "dc.date >= @start and dc.date <= @end " +
                   "group by dc.Date";

            cmd = new SqlCommand(sql, connR360);
            cmd.CommandTimeout = 0;

            cmd.Parameters.AddWithValue("@ValidFrom", now);
            cmd.Parameters.AddWithValue("@ValidTo", now);

            cmd.Parameters.AddWithValue("@start", start);
            cmd.Parameters.AddWithValue("@end", end);

            reader = cmd.ExecuteReader();

            //Here we loop throught the dataset and add each result to List<T>
            while (reader.Read())
            {
                string nowInString = Convert.ToDateTime(reader["MyDate"]).ToString("yyyy-MM-dd");
                bookedlist.Add(new Booked
                {
                     Date = nowInString,
                     GuestNight = reader["Guestnight"].ToString(),
                     TimeRange = startTime + '-' + endTime,
                     CarCounter = "0"
                 });
            }

            reader.Close();
        }

        /// <summary>
        /// This method will get the LodgingOccupancy for a range of date. There are several steps
        /// that has to be done.
        /// </summary>
        /// <param name="start">The startdate to start from to calculate LodgingOccupancy in percent</param>
        /// <param name="end">The enddate to end the calculation of LodgingOccupancy in percent</param>
        public void GetLodgingOccupancy(string start, string end)
        {
            //Beläggningsgrad beräknas enligt =
            //Antal bokade enheter logi/Antal beläggningsbara enheter logi

            /*************************************************************************
            *              Calculate number of available units                       *
            **************************************************************************/

            string sql = "SELECT sum(dpl.nofunits) as Available " +
                         "FROM[Datamart].[Dim_Pool] dp " +
                         "INNER JOIN[Datamart].[Dim_PoolsLogi] as dpl ON r2lidPool = concat(dp.ResortLId, '-', dp.PoolLid) " +
                         "INNER JOIN[Datamart].[Dim_PoolGrpConns] as coons ON dp.Dim_Pool_SK = coons.Dim_Pool_SK " +
                         "INNER JOIN[Datamart].[Dim_PoolGrps] as grp ON coons.PoolGrpsKey = grp.PoolGrpsKey " +
                         "where dp.ResortLId = 12193247 and " +
                         "dp.StatGroupLid in (1, 5) and " +
                         "dpl.ValidFrom <= @ValidFrom and " +
                         "dpl.ValidTo >= @ValidTo and grp.Desc1 = 'Alla boende exkl. camping'";

            cmd = new SqlCommand(sql, connR360);
            cmd.CommandTimeout = 0;

            cmd.Parameters.AddWithValue("@ValidFrom", now);
            cmd.Parameters.AddWithValue("@ValidTo", now);

            reader = cmd.ExecuteReader();
            if (reader.Read())
                availableUnits = Int32.Parse(reader["Available"].ToString());

            reader.Close();

            /*******************************************************************************
            *    Calculate the number of blocked units for LodgingOccupancy(Logiberäkning)                       *
            *******************************************************************************/
            sql = "SELECT sum(AntalUnitsBlockade) as Blocked, bu.date as Mydate " +
                  "FROM Datamart.Blocked_Units bu " +
                  "INNER JOIN[Datamart].[Dim_Pool] as dp ON bu.R2lidPool = concat(dp.ResortLId, '-', dp.PoolLid) " +
                  "INNER JOIN[Datamart].[Dim_PoolGrpConns] as coons ON dp.Dim_Pool_SK = coons.Dim_Pool_SK " +
                  "INNER JOIN[Datamart].[Dim_PoolGrps] as grp ON coons.PoolGrpsKey = grp.PoolGrpsKey " +
                  "where dp.ResortLId = 12193247 and " +
                  "grp.Desc1 = 'Alla boende exkl. camping' and " +
                  "bu.date >= @start and bu.date <= @end " +
                  "and dp.StatGroupLid in (1, 5) " +
                  "group by date " +
                  "order by date asc";

            cmd = new SqlCommand(sql, connR360);
            cmd.CommandTimeout = 0;

            cmd.Parameters.AddWithValue("@start", start);
            cmd.Parameters.AddWithValue("@end", end);

            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                int blockedUnits = int.Parse(reader["Blocked"].ToString());
                string date = Convert.ToDateTime(reader["MyDate"]).ToString("yyyy-MM-dd");

                //This will be used in denominator when calculating the Lodging occupancy(Logibeläggning)
                int nrOfBelaggningsbaraEnheter = availableUnits - blockedUnits;
                logiList.Add(new TempForLogi(date, nrOfBelaggningsbaraEnheter));
            }

            reader.Close();

            /**************************************************************
            *        Calculate the number of booked Accommodation(Logi)   *
            **************************************************************/

            sql = "SELECT sum(hfb.Quantity) as Booked,dc.date as Date " +
                  "FROM[Datamart].[HistoryFact_Booking] hfb " +
                  "INNER JOIN[Datamart].[Dim_Calendar] as dc ON hfb.Dim_CalendarArrivalDate_SK = dc.Dim_Calendar_SK " +
                  "INNER JOIN[Datamart].[Dim_Pool] as dp ON hfb.Dim_Pool_SK = dp.Dim_Pool_SK and dp.StatGroupLid in (1, 5) " +
                  "INNER JOIN[Datamart].[Dim_Unit] as du ON hfb.Dim_Unit_SK = du.Dim_Unit_SK " +
                  "INNER JOIN[Datamart].[Dim_PoolGrpConns] as coons ON dp.Dim_Pool_SK = coons.Dim_Pool_SK " +
                  "INNER JOIN[Datamart].[Dim_PoolGrps] as grp ON coons.PoolGrpsKey = grp.PoolGrpsKey " +
                  "INNER JOIN[Datamart].Dim_PoolStatCodes as dpsc ON hfb.Dim_PoolStatCodes_SK = dpsc.Dim_PoolStatCodes_SK " +
                  "where hfb.ValidFrom <= @ValidFrom and hfb.ValidTo >= @ValidTo " +
                  "and hfb.Gästnatt = 1 " +
                  "and hfb.Dettype = 'D' " +
                  "and hfb.Is_Canceled = 0 " +
                  "and dc.Date >= @start and dc.date <= @end " +
                  "and dpsc.StatGroup = 'Logi' " +
                  "and grp.Desc1 = 'Alla boende exkl. camping' " +
                  "and du.ResortLid = 12193247 " +
                  "group by dc.date " +
                  "order by dc.date";

            cmd = new SqlCommand(sql, connR360);
            cmd.CommandTimeout = 0;

            cmd.Parameters.AddWithValue("@ValidFrom", now);
            cmd.Parameters.AddWithValue("@ValidTo", now);

            cmd.Parameters.AddWithValue("@start", start);
            cmd.Parameters.AddWithValue("@end", end);

            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                int booked = (int)reader["Booked"];
                string date = Convert.ToDateTime(reader["Date"]).ToString("yyyy-MM-dd");

                var findLogi = logiList.Find(d => d.Date == date);
                var bookedItem = bookedlist.Find(d => d.Date == date);
                bookedItem.BelaggningLogi = string.Format("{0:F1}", (Convert.ToSingle(booked) / findLogi.NrOfBelaggningsbaraEnheter) * 100);
            }
        }

        /// <summary>
        /// This method will sum the number of cars that pass in between startTime and endTime 
        /// for each specified date ranging from start to end. If no startTime and endTime is being passed when
        /// consuming the API we use default 08:30 resp 13:30.
        /// </summary>
        /// <param name="start">The startdate</param>
        /// <param name="end">The enddate</param>
        /// <param name="startTime">The time to start count from</param>
        /// <param name="endTime">The time to end counting</param>
        public IEnumerable<Booked> GetCarCounter(string start, string end)
        {
            string tempStart = start + "T00:00:01";
            string tempEnd = end + "T23:59:59";

            string sql = "SELECT SUM(CAST(count as INT)) as Count, CAST(date as DATE) as Date " +
                         "FROM CarCounter " +
                         "WHERE CAST(date as datetime) >=  CAST(@tempStart as datetime) and " +
                               "CAST(date as datetime) <= CAST(@tempEnd as datetime) and " +
                               "direction = '1' and " +
                               "CAST(date AS TIME) BETWEEN @startTime and @endTime " +
                               "group by CAST(date as DATE) " +
                               "ORDER BY DATE";
            

            cmd = new SqlCommand(sql, connCarCounter);
            cmd.CommandTimeout = 0;

            cmd.Parameters.AddWithValue("@tempStart", tempStart);
            cmd.Parameters.AddWithValue("@tempEnd", tempEnd);

            cmd.Parameters.AddWithValue("@startTime", startTime);
            cmd.Parameters.AddWithValue("@endTime", endTime);

            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string date = Convert.ToDateTime(reader["Date"]).ToString("yyyy-MM-dd");
                var bookedItem = bookedlist.Find(d => d.Date == date);
                bookedItem = bookedItem == null ? new Booked() : bookedItem;
                bookedItem.Date = date;
                bookedItem.CarCounter = Math.Ceiling(((int)reader["Count"]) * MULTIPLIKATOR).ToString();
                bookedItem.TimeRange = startTime + '-' + endTime;
            }

            reader.Close();

            connR360.Close();
            connCarCounter.Close();

            return bookedlist;
        }
    }
}
