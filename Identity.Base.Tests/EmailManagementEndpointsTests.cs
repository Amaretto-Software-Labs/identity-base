using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using Identity.Base.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Base.Tests;

public class EmailManagementEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public EmailManagementEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConfirmEmail_ConfirmsUserSuccessfully()
    {
        const string email = "confirm-email@example.com";
        const string password = "StrongPass!2345";

        var user = await SeedUserAsync(email, password, confirmEmail: false);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = Encode(token);

        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/confirm-email", new
        {
            userId = user.Id,
            token = encodedToken
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var refreshedUser = await userManager.FindByEmailAsync(email);
        refreshedUser.ShouldNotBeNull();
        refreshedUser!.EmailConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task ResendConfirmation_ForUnconfirmedUser_SendsEmail()
    {
        const string email = "resend-confirmation@example.com";
        const string password = "StrongPass!2345";
        await SeedUserAsync(email, password, confirmEmail: false);

        var emailSender = _factory.EmailSender;
        var beforeCount = emailSender.Sent.Count;

        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/resend-confirmation", new { email });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        emailSender.Sent.Count.ShouldBeGreaterThan(beforeCount);
        emailSender.Sent.Last().ToEmail.ShouldBe(email);
    }

    [Fact]
    public async Task ForgotPassword_ForConfirmedUser_SendsEmail()
    {
        const string email = "forgot-password@example.com";
        const string password = "StrongPass!2345";
        await SeedUserAsync(email, password, confirmEmail: true);

        var emailSender = _factory.EmailSender;
        var beforeCount = emailSender.Sent.Count;

        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/forgot-password", new { email });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        emailSender.Sent.Count.ShouldBeGreaterThan(beforeCount);
        emailSender.Sent.Last().ToEmail.ShouldBe(email);
    }

    [Fact]
    public async Task ResetPassword_UpdatesCredentials()
    {
        const string email = "reset-password@example.com";
        const string oldPassword = "StrongPass!2345";
        const string newPassword = "NewStrongPass!2345";

        var user = await SeedUserAsync(email, oldPassword, confirmEmail: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = Encode(token);

            using var client = CreateClient();
            var resetResponse = await client.PostAsJsonAsync("/auth/reset-password", new
            {
                userId = user.Id.ToString(),
                token = encodedToken,
                password = newPassword
            });

            resetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var loginClient = CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/auth/login", new
        {
            email,
            password = newPassword,
            clientId = "spa-client"
        });

        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        client.BaseAddress = new Uri("https://localhost");
        return client;
    }

    private async Task<ApplicationUser> SeedUserAsync(string email, string password, bool confirmEmail)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = confirmEmail,
                DisplayName = "Email Test User"
            };

            var result = await userManager.CreateAsync(user, password);
            result.Succeeded.ShouldBeTrue();
        }
        else if (confirmEmail && !user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        if (confirmEmail && !await userManager.IsEmailConfirmedAsync(user))
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        return user;
    }

    private static string Encode(string value)
        => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(value));
}
