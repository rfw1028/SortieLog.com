using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlightLogs.Pages
{
    [Authorize]
    public class ViewFlightsModel : PageModel
    {
        public void OnGet()
        {

        }
    }



}
