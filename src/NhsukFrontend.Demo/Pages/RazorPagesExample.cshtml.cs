using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NhsukFrontend.Demo.Pages;

public partial class RazorPagesExampleModel : PageModel
{
    [BindProperty] public string? Postcode { get; set; }

    public bool Searched { get; private set; }

    public string? PostcodeError => ModelState[nameof(Postcode)]?.Errors.FirstOrDefault()?.ErrorMessage;

    public void OnPost()
    {
        Searched = true;
        if (string.IsNullOrWhiteSpace(Postcode))
            ModelState.AddModelError(nameof(Postcode), "Enter a postcode");
        else if (!PostcodePattern().IsMatch(Postcode.Trim()))
            ModelState.AddModelError(nameof(Postcode), "Enter a full UK postcode, like LS1 4AP");
    }

    [GeneratedRegex(@"^[A-Za-z]{1,2}\d[A-Za-z\d]?\s*\d[A-Za-z]{2}$")]
    private static partial Regex PostcodePattern();
}
