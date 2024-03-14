using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using QbimApi.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text.Json;

//An example how we can consume REST API GET https://localhost:44380/api/data/reservations?start=2022-04-20&end=2022-04-26
//För att authentisera dig använder du https://localhost:44380/api/Token/Authenticate med method POST
//Content-type application-json
//Body
//{
//  "username": "IdreFjall",
//  "password": "IdreFjall",
//  "clientSecret": "kqpSaGCERiYZYQ9doRqJN1QZgFpJrtCrxSNAjUdS"
//}
//Denna returnerar en Token som du sedan använder vid Get
//När man sätter Authorization Bearer pASDPJsaidj9usadijIJD måste man använda Header.
namespace QbimApi.Controllers
{
    [Authorize] //The user must must be authenticated before using this action
    [Route("api/[controller]")]
    [ApiController]
    public class DataController : Controller // ControllerBase
    {
        private IConfiguration Configuration;

        public DataController(IConfiguration _configuration)
        {
            Configuration = _configuration;
        }

        /// <summary>
        /// Accessed by https://domän/api/Data/Reservations
        /// If you call this action without being Authorized you will be Unauthorized and give no access.
        /// If the consumer doesn't specify startTime and endTime we use default 08:30 resp 13:30
        /// </summary>
        /// <param name="start">The date to use as startdate</param>
        /// <param name="end">The date to use as enddate</param>
        /// <param name="startTime">The time to use as the starttime</param>
        /// <param name="endTime">The time to use as the endtime</param>
        /// <returns></returns>
        [HttpGet("Reservations")]
        public JsonResult Reservations(string start, string end, string startTime = "08:30", string endTime = "13:30")
        {
            DateTime dateValue;
            bool validTime = false;

            //Validate the time part
            if (IsValidTimeFormat(startTime) && IsValidTimeFormat(endTime)) 
            {
                validTime = Convert.ToDateTime(endTime) >= Convert.ToDateTime(startTime);
            }

            //Make a complete validation for the passed argument
            if (DateTime.TryParse(start, out dateValue) && DateTime.TryParse(end, out dateValue) && validTime)
            {
                DbApi dbApi = new DbApi(Configuration, startTime,endTime);
                dbApi.GetGuestNights(start, end);
                dbApi.GetLodgingOccupancy(start, end);
                IEnumerable<Booked> bookedList = dbApi.GetCarCounter(start, end);
                return new JsonResult(bookedList);
            }
            else
            {
                Error error = new Error
                {
                    StatusCode = "400",
                    ErrorMessage = "Invalid passed argument",
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                return new JsonResult(error);
            }
        }

        public bool IsValidTimeFormat(string input)
        {
            TimeSpan dummyOutput;
            return TimeSpan.TryParse(input, out dummyOutput);
        }
    }
}
