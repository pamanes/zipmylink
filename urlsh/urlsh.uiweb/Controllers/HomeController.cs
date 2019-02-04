using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.Net;
using urlsh.manager;
using urlsh.Models;

namespace urlsh.Controllers
{
    public class HomeController : Controller
    { 
        private IConfiguration _configuration;

        public HomeController(IConfiguration Configuration)
        {
            _configuration = Configuration;
        }
    
        public IActionResult Index(string id)
        {
            string shortUrl = string.Empty;
            string result = string.Empty;
            Shortener shortener = new Shortener(_configuration["connectionString"], _configuration["salt"]);
            if (string.IsNullOrEmpty(id))
            {
                //here we just show the Index
                try
                {
                    shortener.TestConnect();
                    result = "OK";
                }
                catch (Exception ex)
                {
                    result = ex.Message;
                }
                ViewData["host"] = "https://" + Request.Host;
                ViewData["result"] = result;
                ViewData["connectionString"] = _configuration["connectionString"];
                return View();
            }
            else
            {
                try
                {
                    if (id.Length <= 12 && !string.IsNullOrEmpty(shortUrl = shortener.GetUrl(id)))
                        return Redirect(shortUrl);
                }
                catch { }
            }
            Response.StatusCode = 404;
            //ViewData["id"] = Uri.EscapeDataString(id);
            return View("NotFound");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Zip(string url)
        {
            Uri uri = null;
            string result = string.Empty;
            if (string.IsNullOrEmpty(url) || url.Length > 4000 || !Uri.TryCreate(url, UriKind.Absolute, out uri) || null == uri || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return new JsonResult(null) { StatusCode = (int)HttpStatusCode.NotFound };
            }
            else
            {
                try
                {
                    Shortener sh = new Shortener(_configuration["connectionString"], _configuration["salt"]);
                    return new JsonResult("https" + Uri.SchemeDelimiter + Request.Host + "/" + sh.Shorten(uri.ToString()));
                }
                catch(Exception ex)
                {
                    return new JsonResult(ex.Message) { StatusCode = (int)HttpStatusCode.NotFound };
                }
            }                        
        }



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
