using System.ComponentModel.DataAnnotations;

namespace SmartHotel.ViewModels;

public static class PasswordRules
{
    public const string Pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,100}$";
    public const string Message = "Password must be at least 8 characters and contain upper-case, lower-case letters and a digit.";
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your username or email.")]
    [Display(Name = "Username or Email"), StringLength(150)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password."), DataType(DataType.Password), StringLength(100)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "New password")]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public int StatusCode { get; set; } = 500;
}
