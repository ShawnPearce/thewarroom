using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using TheWarRoomApp.Infrastructure;

namespace TheWarRoomApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews(options =>
            {
                // Every page needs a signed-in user unless its action is marked [AllowAnonymous]
                options.Filters.Add(new AuthorizeFilter());

                // A user with a temporary password is sent to the change-password page first
                options.Filters.Add<MustChangePasswordFilter>();

                // Shows a clear page when the API project is not running
                options.Filters.Add<ApiUnavailableFilter>();
            });

            // Signing in creates a cookie. It expires after 30 minutes without activity.
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "TheWarRoom.Auth";
                    options.LoginPath = "/Account/Login";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                    options.SlidingExpiration = true;
                });

            // The API is called through this named client.
            // ApiUserHeaderHandler adds the signed-in user's id to every call.
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddTransient<ApiUserHeaderHandler>();
            builder.Services.AddHttpClient("WarRoomAPI", client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"]!);
            }).AddHttpMessageHandler<ApiUserHeaderHandler>();

            // Video files pass between the browser and the API through a client of their own.
            // A long video takes more than the usual 100 seconds, and when the API answers with a signed link
            // to film storage, that answer is handed to the browser as it is instead of being followed here.
            builder.Services.AddHttpClient("WarRoomAPIFiles", client =>
                {
                    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"]!);
                    client.Timeout = Timeout.InfiniteTimeSpan;
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
                .AddHttpMessageHandler<ApiUserHeaderHandler>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();

            app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
