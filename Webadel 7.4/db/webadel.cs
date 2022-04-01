using StackExchange.Profiling;
using StackExchange.Profiling.Data;

namespace Webadel7.DB {
	partial class WebadelDataContext {
		public static WebadelDataContext GetProfiledDC() {
			WebadelDataContext dc = new WebadelDataContext();
			ProfiledDbConnection conn = new ProfiledDbConnection(dc.Connection, MiniProfiler.Current);
			return new WebadelDataContext(conn);
		}
	}

	public partial class Message {
		public User Author { get { return User1; } }
		public User Recipient { get { return User; } }
	}
}
