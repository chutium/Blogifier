using Core.Data;
using Core.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace App.Pages.Account
{
    public class LoginModel : PageModel
    {
        [Required]
        [BindProperty]
        public string UserName { get; set; }

        [Required]
        [BindProperty]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [BindProperty]
        [DataType(DataType.Password)]
        public string PrivateKey { get; set; }

        [BindProperty]
        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }

        SignInManager<AppUser> _sm;
        UserManager<AppUser> _userManager;

        public LoginModel(SignInManager<AppUser> sm, UserManager<AppUser> userManager)
        {
            _sm = sm;
            _userManager = userManager;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                bool succeeded = false;
                ElastosAPI service = new ElastosAPI();
                string did = service.GetDID(PrivateKey);
                if (string.IsNullOrEmpty(did))
                    return Page();

                AppUser user = await _userManager.FindByNameAsync(UserName);
                if (user?.DID == did)
                {
                    var result = await _sm.PasswordSignInAsync(UserName, Password, RememberMe, lockoutOnFailure: false);
                    succeeded = result.Succeeded;
                }

                if (succeeded)
                {
                    return RedirectToLocal(returnUrl);
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();

            }
            return Page();
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return Redirect("~/");
            }
        }
    }
}