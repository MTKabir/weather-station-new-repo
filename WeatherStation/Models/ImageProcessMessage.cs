using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace WeatherStation.Models
{
    internal class ImageProcessMessage
    {
        public string JobId { get; set; } = default!;
        public string StationName { get; set; } = default!;
        public string Temperature { get; set; } = default!;
    }
}
