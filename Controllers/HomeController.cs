using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : ControllerBase
    {
        private readonly FeedbackDAL _feedbackDAL;

        // Replace this with the actual admin UserId from your database.
        private const string AdminUserId = "ADMIN_USER_ID";

        public HomeController(FeedbackDAL feedbackDAL)
        {
            _feedbackDAL = feedbackDAL;
        }

        // GET: /api/home/feedback
        // Public endpoint; no authentication token required.
        [HttpGet("feedback")]
        public async Task<IActionResult> GetHomeFeedback()
        {
            var adminFeedback =
                await _feedbackDAL.GetLatestAdminFeedbackAsync(AdminUserId);

            var otherFeedback =
                await _feedbackDAL.GetOtherFeedbackAsync(AdminUserId);

            return Ok(new
            {
                adminFeedback,
                otherFeedback
            });
        }

        //public IActionResult Create()
        //{
        //    return View();
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult Create(Employees emp)
        //{
        //    try
        //    {
        //        dal.AddEmployee(emp);
        //        return RedirectToAction("Index");
        //    }
        //    catch
        //    {
        //        return View();
        //    }
        //}

        //public IActionResult Edit(int id)
        //{
        //    Employees emp = dal.getEmployeeByID(id);
        //    return View(emp);
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult Edit(Employees emp)
        //{
        //    try
        //    {
        //        dal.UpdateEmployee(emp);
        //        return RedirectToAction("Index");
        //    }
        //    catch
        //    {
        //        return View();
        //    }
        //}

        //public IActionResult Details(int id)
        //{
        //    Employees emp = dal.getEmployeeByID(id);
        //    return View(emp);
        //}

        //public IActionResult Delete(int id)
        //{
        //    Employees emp = dal.getEmployeeByID(id);
        //    return View(emp);
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult Delete(Employees emp)
        //{
        //    try
        //    {
        //        dal.DeleteEmployee(emp.ID);
        //        return RedirectToAction("Index");
        //    }
        //    catch
        //    {
        //        return View();
        //    }
        //}

        //public IActionResult Privacy()
        //{
        //    return View();
        //}

        //[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        //public IActionResult Error()
        //{
        //    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        //}
    }
}
