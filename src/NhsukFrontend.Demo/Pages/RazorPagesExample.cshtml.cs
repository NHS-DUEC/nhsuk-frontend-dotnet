using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NhsukFrontend.Demo.Models;

namespace NhsukFrontend.Demo.Pages;

public sealed class RazorPagesExampleModel : PageModel
{
    [BindProperty] public AppointmentRequest Details { get; set; } = new();

    public bool Submitted { get; private set; }

    public void OnPost() => Submitted = ModelState.IsValid;
}
