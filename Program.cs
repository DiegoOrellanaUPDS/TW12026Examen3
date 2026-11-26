using System.Security.Claims;
using AspNet.Security.OpenId.Steam;
using Microsoft.AspNetCore.Authentication.Cookies;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    })
    .AddSteam(options =>
    {
        options.CallbackPath = "/signin-steam";
        options.ApplicationKey = builder.Configuration["Authentication:Steam:ApiKey"];
        options.Events.OnAuthenticated = context =>
        {
            // El middleware ya verificó la respuesta OpenID con Steam.
            var identity = (ClaimsIdentity)context.Ticket.Principal.Identity!;
            var identifier = identity.FindFirst(ClaimTypes.NameIdentifier)!;
            var uri = new Uri(identifier.Value);
            var steamId = uri.Segments.Last();
            if (uri.Host != "steamcommunity.com" || uri.AbsolutePath != $"/openid/id/{steamId}"
                || steamId.Length != 17 || !steamId.All(char.IsAsciiDigit))
                throw new InvalidOperationException("SteamID64 no válido.");
            identity.RemoveClaim(identifier);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, steamId));
            identity.AddClaim(new Claim("provider", "Steam"));
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy("SoloSteam", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("provider", "Steam")));
    
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
