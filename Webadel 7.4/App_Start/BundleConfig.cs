using System.Collections.Generic;
using System.Web.Optimization;

namespace Webadel7 {
    public class BundleConfig {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles) {
            PublicBundles(bundles);
        }

        private static void PublicBundles(BundleCollection bundles) {
            ScriptBundle publicScriptsBundle = new ScriptBundle("~/public/scripts") { Orderer = new AsIsBundleOrderer() };
            publicScriptsBundle
                .Include("~/Scripts/jquery-3.6.1.min.js")
                .Include("~/Scripts/jquery-ui.js")              // core, widget, sortable - mostly so fileupload doesn't throw $.widget error

                .Include("~/Scripts/bootstrap.min.js")

                .Include("~/Scripts/jquery.cookie.js")
                .Include("~/Scripts/mousetrap.min.js")
                .Include("~/Scripts/autosize.js")
                .Include("~/Scripts/iframe-transport.js") // is this necessary?
                .Include("~/Scripts/jquery.fileupload.js")
                .Include("~/Scripts/jquery-textrange.js")
                .Include("~/Scripts/jquery.touchSwipe.min.js")
                .Include("~/Scripts/jquery.tablesorter.min.js")
                .Include("~/Scripts/main.js")
                ;
            bundles.Add(publicScriptsBundle);

            bundles.Add(new StyleBundle("~/Content/styles").Include(
                "~/Content/bootstrap.min.css",
                "~/Content/webadel.css"));

            bundles.Add(new ScriptBundle("~/public/roomscripts").Include(
                "~/Scripts/room.js",
                "~/Scripts/room_messageControls.js",
                "~/Scripts/room_userSettings.js",
                "~/Scripts/room_roomStuff.js",
                "~/Scripts/room_postbox.js",
                "~/Scripts/room_predictive.js",
                "~/Scripts/badges.js"
                ));
        }
    }

    /* attempt to enforce the order of bundled scripts // from: http://stackoverflow.com/a/11981271/3140552 */
    public class AsIsBundleOrderer : IBundleOrderer {
        public IEnumerable<BundleFile> OrderFiles(BundleContext context, IEnumerable<BundleFile> files) => files;
    }
}
