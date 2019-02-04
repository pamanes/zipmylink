using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace urlsh.uiweb.Controllers
{
    public class PrivController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}