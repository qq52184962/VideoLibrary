﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace PointVideoGallery.Controllers
{
    [CnsApiAuthorize]
    public class AccountController : Controller
    {
        // GET: Account
        public ActionResult Index()
        {
            return View();
        }

        // GET: /account/role
        public ActionResult Role()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult Signin()
        {
            return View();
        }
    }
}