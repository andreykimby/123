using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Features.InstaViewer.Entities
{
    public class AppUsage
    {
        public int CallCount { get; set; }
        public int TotalCputime { get; set; }
        public int TotalTime { get; set; }

        public static AppUsage Empty => new AppUsage()
        {
            CallCount = 0,
            TotalCputime = 0,
            TotalTime = 0
        };
    }
}
