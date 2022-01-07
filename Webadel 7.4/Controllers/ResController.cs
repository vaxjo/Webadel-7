using System;
using System.Linq;
using System.Web.Mvc;

namespace Webadel7.Controllers {
    public class ResController : WebadelController {

        [HttpGet]
        public ActionResult Index(string filename, int? width, float? aspectRatio) {
            Resource res = Resource.Load(filename);
            if (res == null) {
                return HttpNotFound(); // this doesn't seem to work
            }

            string cacheName = res.Id + (width.HasValue ? "_" + width.Value : "") + (aspectRatio.HasValue ? "_" + aspectRatio.Value : "");
            string cacheFullname = Resource.GetResourceDir() + @"\" + cacheName;

            // if not in cache, then:
            if (!System.IO.File.Exists(cacheFullname)) {
                byte[] data = res.GetData((width != null && res.IsImage ? width : null), (aspectRatio != null && res.IsImage ? aspectRatio : null));
                if (data.Length == 0) {
                    return HttpNotFound();
                }
                System.IO.File.WriteAllBytes(cacheFullname, data);
            }

            Response.AppendHeader("Content-Disposition", "inline; filename=\"" + filename ?? res.Filename + "\"");
            return File(cacheFullname, res.ContentType);
        }

        public FileResult Favicon() {
            // every five minutes return a new favicon from the pool
            string filename = (string)Myriads.Cache.Get("Favicon", "", delegate () {
                Random rnd = new Random();
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(Server.MapPath("/Content/favicons"));
                System.IO.FileInfo[] icos = di.GetFiles("*.ico");
                return icos[rnd.Next(icos.Length)].FullName;
            }, TimeSpan.FromMinutes(5));

            return File(System.IO.File.ReadAllBytes(filename), "image/x-icon");
        }

        // if this becomes useful later move it somewhere it better
        public void Log(string message) {
            System.IO.File.AppendAllText(Server.MapPath("~/App_Data/log.txt"), message);
        }
    }
}
