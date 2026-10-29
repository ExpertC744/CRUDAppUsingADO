
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Middleware;
using CRUDAppUsingADO.Services;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. SERILOG
// ==========================================
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File(
        "logs/log-.txt",
        rollingInterval: RollingInterval.Day
    )
);

// ==========================================
// 2. MVC + API
// ==========================================
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

// ==========================================
// 3. DATA ACCESS LAYER
// ==========================================
builder.Services.AddScoped<DBHelper>();
builder.Services.AddScoped<UserDAL>();
builder.Services.AddScoped<TokenDAL>();
builder.Services.AddScoped<FeedbackDAL>();

// ==========================================
// 4. SERVICES
// ==========================================
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddHttpContextAccessor();

// ==========================================
// 5. AUTHENTICATION
// JWT Bearer is the default for API requests.
// Google and Cookie authentication remain available.
// ==========================================
var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key is missing from configuration."
    );
}

builder.Services
    .AddAuthentication(options =>
    {
        // Authenticate incoming requests using JWT by default.
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        // Return 401 for unauthenticated API requests,
        // rather than redirecting them to Google.
        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId =
            builder.Configuration["Google:ClientId"]
            ?? throw new InvalidOperationException(
                "Google:ClientId is missing from configuration."
            );

        options.ClientSecret =
            builder.Configuration["Google:ClientSecret"]
            ?? throw new InvalidOperationException(
                "Google:ClientSecret is missing from configuration."
            );

        options.Events.OnRemoteFailure = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "text/plain";

            await context.Response.WriteAsync(
                "Google authentication failed. Check the server logs."
            );
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)
                ),

                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],

                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                   // Recognize the role and name claims in your JWT.
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = System.Security.Claims.ClaimTypes.Name
            };
    });

// ==========================================
// 6. AUTHORIZATION
// ==========================================
builder.Services.AddAuthorization();

// ==========================================
// 7. CORS
// ==========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        var frontendUrl = builder.Configuration["Frontend:Url"];

        if (string.IsNullOrWhiteSpace(frontendUrl))
        {
            throw new InvalidOperationException(
                "Frontend:Url is missing from configuration."
            );
        }

        policy
            .WithOrigins(frontendUrl)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ==========================================
// 8. SWAGGER
// ==========================================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CRUD API",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token."
    });

    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        }
    );
});

// ==========================================
// 9. BUILD APPLICATION
// ==========================================
var app = builder.Build();

// ==========================================
// 10. HTTP PIPELINE
// ==========================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowReactApp");

// Authentication must run before authorization.
// Register each middleware only once.
app.UseAuthentication();
app.UseAuthorization();

// Exception middleware
app.UseMiddleware<ExceptionMiddleware>();

// ==========================================
// 11. ROUTES
// ==========================================
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();
