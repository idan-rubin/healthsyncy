using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HealthSyncy.Admin.EntryPoint.Pages;

public sealed class DeniedModel : PageModel
{
    public string Heading { get; private set; } = "";

    public string Explanation { get; private set; } = "";

    public void OnGet(bool failed)
    {
        if (failed)
        {
            Heading = "Sign-in did not finish";
            Explanation = "The identity provider did not complete the sign-in.";
            return;
        }
        Heading = "Access denied";
        Explanation = "This account is not the HealthSyncy administrator. "
            + "Sign out of the identity provider, then sign in with the administrator account.";
        Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}
