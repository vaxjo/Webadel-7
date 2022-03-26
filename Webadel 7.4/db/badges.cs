using System;
using System.Linq;
using StackExchange.Profiling;
using StackExchange.Profiling.Data;


namespace Webadel7.DB_Badges {
    partial class DataContext {
        public static DataContext GetProfiledDC() {
            DataContext dc = new DataContext();
            ProfiledDbConnection conn = new ProfiledDbConnection(dc.Connection, MiniProfiler.Current);
            return new DataContext(conn);
        }
    }

    public partial class Badge {
       
    }
}
