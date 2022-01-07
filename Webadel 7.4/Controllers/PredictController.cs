using System;
using System.Data.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Webadel7.Controllers {
    [WebadelAuthorize]
    public class PredictController : WebadelController {

        [ValidateInput(false)]
        public Myriads.JsonNetResult Index(string lastword) {
            return JsonNet(Predictor.GetWords(MvcApplication.CurrentUser.Id, lastword));
        }
    }
}

