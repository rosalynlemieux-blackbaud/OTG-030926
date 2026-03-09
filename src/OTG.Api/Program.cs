using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OTG.Api.Authorization;
using OTG.Api.Options;
using OTG.Api.Services;
using OTG.Infrastructure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddOtgCosmos(builder.Configuration);
builder.Services.AddHostedService<CosmosBootstrapHostedService>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<BlackbaudOptions>(builder.Configuration.GetSection(BlackbaudOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<ISparkIdeaService, SparkIdeaService>();
builder.Services.AddSingleton<IBlackbaudStateStore, BlackbaudStateStore>();
builder.Services.AddHttpClient<IBlackbaudOAuthService, BlackbaudOAuthService>();
builder.Services.AddScoped<IAuthorizationHandler, NotBannedHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AssignedJudgeOrAdminHandler>();
builder.Services.AddScoped<IAuthorizationHandler, IdeaOwnerOrAdminHandler>();
builder.Services.AddScoped<IAuthorizationHandler, TeamLeaderOrAdminHandler>();
builder.Services.AddScoped<IAuthorizationHandler, CommentOwnerOrAdminHandler>();

var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey))
{
    throw new InvalidOperationException("Jwt:SigningKey must be configured.");
}
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("NotBanned", policy =>
        policy.RequireAuthenticatedUser().AddRequirements(new NotBannedRequirement()));
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");

app.Run();
