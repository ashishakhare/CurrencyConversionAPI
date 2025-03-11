using System.Security.Claims;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

    // Define JWT security scheme
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token."
    });

    // Require authentication globally in Swagger
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Configure IdentityServer (Acts as Token Provider)
builder.Services.AddIdentityServer()
    .AddDeveloperSigningCredential()
    .AddInMemoryApiScopes(new List<ApiScope>
    {
        new ApiScope("currency-api") // Define your API scope
    })
    .AddInMemoryApiResources(new List<ApiResource>
    {
        new ApiResource("currency-api")
        {
            Scopes = { "currency-api" }
        }
    })
    .AddInMemoryClients(new List<Client>
    {
        new Client
        {
            ClientId = "admin-client",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("admin".Sha256()) },
            AllowedScopes = { "currency-api" },
            Claims =
            {
                new ClientClaim(ClaimTypes.Role, "Admin")
            },
            AlwaysSendClientClaims = true,
            AlwaysIncludeUserClaimsInIdToken = true
        },
        new Client
        {
            ClientId = "user-client",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("user".Sha256()) },
            AllowedScopes = { "currency-api" },
            Claims =
            {
                new ClientClaim(ClaimTypes.Role, "User")
            },
            AlwaysSendClientClaims = true,
            AlwaysIncludeUserClaimsInIdToken = true
        }
    })
    .AddInMemoryIdentityResources(new List<IdentityResource>
    {
        new IdentityResources.OpenId(),
        new IdentityResource("roles", new[] { ClaimTypes.Role })
    });

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://localhost:7165";
        options.Audience = "currency-api";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User"));
});

var app = builder.Build();

// Enable Swagger in both Development and Production
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Currency Conversion API v1");
    options.RoutePrefix = string.Empty; // Set Swagger as the default landing page
});

app.UseHttpsRedirection();

// Enable IdentityServer Middleware
app.UseIdentityServer();

// Enable Authentication & Authorization Middleware (should be before MapControllers)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
